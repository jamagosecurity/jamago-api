using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Jama.Domain.Entities;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Jama.Infrastructure.Mail;

/// <summary>
/// Copies new mail from each configured mailbox into the database so the admin
/// panel can show all of it in one place.
///
/// READ-ONLY, deliberately: the INBOX is opened with <see cref="FolderAccess.ReadOnly"/>
/// and messages are fetched without marking them read, so the mailboxes look
/// untouched to the people who use them. Nothing here deletes, moves or sends.
/// A failing mailbox (bad password, server down) is recorded against that mailbox
/// and the others carry on.
/// </summary>
public sealed class MailSyncService(
    IServiceScopeFactory scopes,
    IOptions<MailSettings> options,
    ILogger<MailSyncService> logger) : BackgroundService
{
    private readonly MailSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configured = _settings.Accounts.Where(a => a.IsConfigured).ToList();
        if (configured.Count == 0)
        {
            logger.LogInformation("Mail sync idle: no mailbox has a password configured.");
            return;
        }

        // Let the app finish starting before the first (possibly slow) pass.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.PollMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var account in configured)
            {
                if (stoppingToken.IsCancellationRequested) break;
                await SyncAccountAsync(account, stoppingToken);
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task SyncAccountAsync(MailAccountSettings account, CancellationToken ct)
    {
        try
        {
            using var client = new ImapClient { Timeout = 30_000 };
            await client.ConnectAsync(account.Host, account.Port, account.UseSsl
                ? MailKit.Security.SecureSocketOptions.SslOnConnect
                : MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable, ct);
            await client.AuthenticateAsync(account.Login, account.Password, ct);

            await SyncFolderAsync(client, account, "Inbox", client.Inbox, ct);

            var sent = await ImapFolders.FindSentAsync(client, ct);
            if (sent is not null) await SyncFolderAsync(client, account, "Sent", sent, ct);

            await client.DisconnectAsync(true, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // Never log the password; the message alone says what failed.
            logger.LogWarning("Mail sync {Key} failed: {Message}", account.Key, ex.Message);
            await RecordErrorAsync(account.Key, ex.Message);
        }
    }

    private async Task RecordErrorAsync(string key, string message)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var state = await context.MailAccountStates.FirstOrDefaultAsync(s => s.AccountKey == key && s.Folder == "Inbox");
            if (state is null)
            {
                state = new MailAccountState { AccountKey = key, Folder = "Inbox" };
                context.MailAccountStates.Add(state);
            }
            state.LastSyncAt = DateTime.UtcNow;
            state.LastError = message.Length > 300 ? message[..300] : message;
            await context.SaveChangesAsync();
        }
        catch { /* keep going */ }
    }

    private async Task SyncFolderAsync(
        ImapClient client, MailAccountSettings account, string folderName, IMailFolder folder, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var state = await context.MailAccountStates
            .FirstOrDefaultAsync(s => s.AccountKey == account.Key && s.Folder == folderName, ct);
        if (state is null)
        {
            state = new MailAccountState { AccountKey = account.Key, Folder = folderName };
            context.MailAccountStates.Add(state);
        }

        await folder.OpenAsync(FolderAccess.ReadOnly, ct);

        // A changed UIDVALIDITY means the server renumbered the folder: old UIDs
        // no longer point at the same messages, so read it again from scratch.
        var validity = (long)folder.UidValidity;
        if (state.UidValidity != validity)
        {
            state.UidValidity = validity;
            state.LastUid = 0;
        }

        IList<UniqueId> uids;
        if (state.LastUid == 0)
        {
            var all = await folder.SearchAsync(SearchQuery.All, ct);
            uids = all.Skip(Math.Max(0, all.Count - _settings.FirstSyncMessages)).ToList();
        }
        else
        {
            var range = new UniqueIdRange(new UniqueId((uint)state.LastUid + 1), UniqueId.MaxValue);
            uids = await folder.SearchAsync(range, SearchQuery.All, ct);
        }

        var added = 0;
        foreach (var uid in uids.OrderBy(u => u.Id))
        {
            ct.ThrowIfCancellationRequested();
            var uidValue = (long)uid.Id;
            if (uidValue <= state.LastUid && state.LastUid != 0 && uids.Count == 0) continue;
            if (await context.MailMessages.AnyAsync(
                    m => m.AccountKey == account.Key && m.Folder == folderName
                         && m.UidValidity == validity && m.Uid == uidValue, ct))
            {
                state.LastUid = Math.Max(state.LastUid, uidValue);
                continue;
            }

            var message = await folder.GetMessageAsync(uid, ct);   // read-only folder: not marked read
            context.MailMessages.Add(ToEntity(account, folderName, validity, uidValue, message));
            state.LastUid = Math.Max(state.LastUid, uidValue);
            added++;

            if (added % 25 == 0) await context.SaveChangesAsync(ct);
        }

        state.LastSyncAt = DateTime.UtcNow;
        state.LastError = null;
        await context.SaveChangesAsync(ct);

        if (added > 0) logger.LogInformation("Mail sync {Key}/{Folder}: {Added} new message(s).", account.Key, folderName, added);
    }

    private static MailMessage ToEntity(MailAccountSettings account, string folder, long validity, long uid, MimeMessage m)
    {
        var from = m.From.Mailboxes.FirstOrDefault();
        var text = (m.TextBody ?? string.Empty).Replace("\r", string.Empty);
        var attachments = m.Attachments.Select(a => a.ContentDisposition?.FileName ?? a.ContentType.Name ?? "attachment").ToList();

        return new MailMessage
        {
            AccountKey = account.Key,
            AccountAddress = account.Address,
            Folder = folder,
            UidValidity = validity,
            Uid = uid,
            MessageId = Clip(m.MessageId, 500) is { Length: > 0 } mid ? mid : null,            FromName = Clip(from?.Name, 200),
            FromAddress = Clip(from?.Address, 320),
            ToAddresses = Clip(string.Join(", ", m.To.Mailboxes.Select(x => x.Address)), 1000),
            Subject = Clip(m.Subject, 500),
            SentAt = m.Date == DateTimeOffset.MinValue ? DateTime.UtcNow : m.Date.UtcDateTime,
            Snippet = Clip(string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), 240),
            BodyText = Clip(text, 200_000),
            BodyHtml = m.HtmlBody is { } html ? Clip(html, 400_000) : null,
            HasAttachments = attachments.Count > 0,
            AttachmentNames = Clip(string.Join(", ", attachments), 1000),
        };
    }

    private static string Clip(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
