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
            using var client = await ImapFolders.ConnectAsync(account, ct);

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
            await RecordErrorAsync(account.Key, Plain(ex));
        }
    }

    /// <summary>What went wrong, in words the admin can act on.</summary>
    private static string Plain(Exception ex) => ex switch
    {
        MailKit.Security.AuthenticationException => "Password not accepted — it may have been changed. Update it to reconnect.",
        MailKit.Security.SslHandshakeException or System.Net.Sockets.SocketException or TimeoutException or IOException
            => "Could not reach the mail server (network). Trying again in a few minutes.",
        _ => ex.Message,
    };

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

        // Only the outline of each message (envelope + structure), then only its text and HTML parts: attachments are
        // never downloaded, so a mailbox full of large files syncs as fast as one with plain notes.
        // The copy mailbox also needs the headers, to see which of our mailboxes each email was really sent to.
        var isArchive = folderName == "Inbox" && !string.IsNullOrWhiteSpace(_settings.ArchiveKey)
                        && string.Equals(account.Key, _settings.ArchiveKey, StringComparison.OrdinalIgnoreCase);
        var items = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure
                    | MessageSummaryItems.InternalDate | MessageSummaryItems.Flags;
        if (isArchive) items |= MessageSummaryItems.Headers;
        var summaries = uids.Count == 0 ? [] : await folder.FetchAsync(uids, items, ct);

        var added = 0;
        foreach (var summary in summaries.OrderBy(x => x.UniqueId.Id))
        {
            ct.ThrowIfCancellationRequested();
            var uidValue = (long)summary.UniqueId.Id;
            if (await context.MailMessages.AnyAsync(
                    m => m.SourceKey == account.Key && m.Folder == folderName
                         && m.UidValidity == validity && m.Uid == uidValue, ct))
            {
                state.LastUid = Math.Max(state.LastUid, uidValue);
                continue;
            }

            // Whose mail it is: the mailbox itself, or — read from the copy mailbox — the person it was sent to.
            var owner = isArchive ? RecipientMailbox(summary) ?? account : account;
            // The same email can arrive twice (directly and through the copy mailbox): keep the first.
            var messageId = summary.Envelope?.MessageId;
            if (!string.IsNullOrEmpty(messageId) && await context.MailMessages.AnyAsync(
                    m => m.AccountKey == owner.Key && m.Folder == folderName && m.MessageId == messageId, ct))
            {
                state.LastUid = Math.Max(state.LastUid, uidValue);
                continue;
            }

            var text = await PartTextAsync(folder, summary, summary.TextBody, ct);
            var html = await PartTextAsync(folder, summary, summary.HtmlBody, ct);
            var entity = ToEntity(account, folderName, validity, uidValue, summary, text, html);
            entity.AccountKey = owner.Key;
            entity.AccountAddress = owner.Address;
            entity.SourceKey = account.Key;
            entity.IsRead = folderName == "Sent" || (summary.Flags?.HasFlag(MessageFlags.Seen) ?? true);
            context.MailMessages.Add(entity);
            state.LastUid = Math.Max(state.LastUid, uidValue);
            added++;

            if (added % 10 == 0) await context.SaveChangesAsync(ct);
        }

        // Read marks change outside the app too (Outlook, webmail, a phone): bring ours in line with the server's.
        if (folderName == "Inbox") await RefreshReadMarksAsync(context, folder, account.Key, validity, ct);

        state.LastSyncAt = DateTime.UtcNow;
        state.LastError = null;
        await context.SaveChangesAsync(ct);

        if (added > 0) logger.LogInformation("Mail sync {Key}/{Folder}: {Added} new message(s).", account.Key, folderName, added);
    }

    /// <summary>
    /// For mail in the copy mailbox: which of our mailboxes it was sent to. The forwarding server's delivery headers
    /// name the original mailbox even for Bcc; otherwise To, then Cc. Null when it was sent to the copy mailbox itself.
    /// </summary>
    private MailAccountSettings? RecipientMailbox(IMessageSummary summary)
    {
        var mailboxes = _settings.Accounts
            .Where(a => !string.Equals(a.Key, _settings.ArchiveKey, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(a => a.Address.Trim().ToLowerInvariant(), a => a);

        IEnumerable<string> Addresses()
        {
            string[] delivery = ["X-Original-To", "Delivered-To", "X-Forwarded-To", "X-Forwarded-For", "Envelope-To"];
            if (summary.Headers is { } headers)
                foreach (var header in headers)
                    if (delivery.Contains(header.Field, StringComparer.OrdinalIgnoreCase))
                        foreach (var part in header.Value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                            yield return part.Trim('<', '>', '"', ' ');
            foreach (var m in summary.Envelope?.To.Mailboxes ?? []) yield return m.Address;
            foreach (var m in summary.Envelope?.Cc.Mailboxes ?? []) yield return m.Address;
        }

        foreach (var address in Addresses())
            if (mailboxes.TryGetValue(address.Trim().ToLowerInvariant(), out var hit)) return hit;
        return null;
    }

    /// <summary>Copies the server's "seen" mark onto the messages we hold. One small request (flags only) per pass.</summary>
    private static async Task RefreshReadMarksAsync(IApplicationDbContext context, IMailFolder folder, string key, long validity, CancellationToken ct)
    {
        if (folder.Count == 0) return;
        var flags = await folder.FetchAsync(0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags, ct);
        var seen = flags.ToDictionary(f => (long)f.UniqueId.Id, f => f.Flags?.HasFlag(MessageFlags.Seen) ?? true);

        var held = await context.MailMessages.AsNoTracking()
            .Where(m => m.SourceKey == key && m.Folder == "Inbox" && m.UidValidity == validity)
            .Select(m => new { m.Id, m.Uid, m.IsRead })
            .ToListAsync(ct);
        var nowRead = held.Where(m => !m.IsRead && seen.TryGetValue(m.Uid, out var r) && r).Select(m => m.Id).ToList();
        var nowUnread = held.Where(m => m.IsRead && seen.TryGetValue(m.Uid, out var r) && !r).Select(m => m.Id).ToList();
        if (nowRead.Count > 0)
            await context.MailMessages.Where(m => nowRead.Contains(m.Id)).ExecuteUpdateAsync(u => u.SetProperty(m => m.IsRead, true), ct);
        if (nowUnread.Count > 0)
            await context.MailMessages.Where(m => nowUnread.Contains(m.Id)).ExecuteUpdateAsync(u => u.SetProperty(m => m.IsRead, false), ct);
    }

    /// <summary>One text part of a message (plain or HTML), downloaded on its own; empty when the message has none.</summary>
    private static async Task<string?> PartTextAsync(IMailFolder folder, IMessageSummary summary, BodyPartText? part, CancellationToken ct)
    {
        if (part is null) return null;
        try
        {
            var entity = await folder.GetBodyPartAsync(summary.UniqueId, part, ct);
            return entity is TextPart tp ? tp.Text : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;   // one unreadable part never stops the rest of the mailbox
        }
    }

    private static MailMessage ToEntity(MailAccountSettings account, string folder, long validity, long uid,
        IMessageSummary s, string? textBody, string? htmlBody)
    {
        var env = s.Envelope;
        var from = env?.From.Mailboxes.FirstOrDefault();
        var text = (textBody ?? string.Empty).Replace("\r", string.Empty);
        if (text.Length == 0 && htmlBody is not null)
            text = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(
                System.Text.RegularExpressions.Regex.Replace(htmlBody, "<(style|script)[^>]*>.*?</\\1>", " ", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                "<[^>]+>", " "));
        var attachments = s.Attachments.Select(a => a.FileName ?? a.ContentType.Name ?? "attachment").ToList();
        var date = env?.Date ?? s.InternalDate;

        return new MailMessage
        {
            AccountKey = account.Key,
            AccountAddress = account.Address,
            Folder = folder,
            UidValidity = validity,
            Uid = uid,
            MessageId = Clip(env?.MessageId, 500) is { Length: > 0 } mid ? mid : null,
            FromName = Clip(from?.Name, 200),
            FromAddress = Clip(from?.Address, 320),
            ToAddresses = Clip(string.Join(", ", env?.To.Mailboxes.Select(x => x.Address) ?? []), 1000),
            Subject = Clip(env?.Subject, 500),
            SentAt = date is { } d && d != DateTimeOffset.MinValue ? d.UtcDateTime : DateTime.UtcNow,
            Snippet = Clip(string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), 240),
            BodyText = Clip(text, 200_000),
            BodyHtml = htmlBody is { } html ? Clip(html, 400_000) : null,
            HasAttachments = attachments.Count > 0,
            AttachmentNames = Clip(string.Join(", ", attachments), 1000),
        };
    }

    private static string Clip(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
