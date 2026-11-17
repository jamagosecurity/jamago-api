using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Options;

namespace Jama.Infrastructure.Mail;

/// <summary>Sets the IMAP \\Seen mark on one message, so read / unread is the same in every mail app.</summary>
public sealed class MailFlagWriter(IOptions<MailSettings> options) : IMailFlagWriter
{
    public async Task SetSeenAsync(string accountKey, string folder, long uidValidity, long uid, bool seen, CancellationToken ct)
    {
        var account = options.Value.Accounts.FirstOrDefault(a => a.Key == accountKey && a.IsConfigured);
        if (account is null || uid <= 0) return;

        using var client = await ImapFolders.ConnectAsync(account, ct);

        var box = folder == "Sent" ? await ImapFolders.FindSentAsync(client, ct) : client.Inbox;
        if (box is null) return;
        await box.OpenAsync(FolderAccess.ReadWrite, ct);
        if ((long)box.UidValidity == uidValidity)      // only if the server has not renumbered the folder
        {
            var id = new UniqueId((uint)uid);
            if (seen) await box.AddFlagsAsync(id, MessageFlags.Seen, true, ct);
            else await box.RemoveFlagsAsync(id, MessageFlags.Seen, true, ct);
        }
        await client.DisconnectAsync(true, ct);
    }

    public async Task MoveToTrashAsync(string accountKey, string folder, long uidValidity, long uid, string? messageId, CancellationToken ct)
    {
        var account = options.Value.Accounts.FirstOrDefault(a => a.Key == accountKey && a.IsConfigured);
        if (account is null) return;

        using var client = await ImapFolders.ConnectAsync(account, ct);
        var box = folder == "Sent" ? await ImapFolders.FindSentAsync(client, ct) : client.Inbox;
        var trash = await ImapFolders.FindTrashAsync(client, ct);
        if (box is null) return;
        await box.OpenAsync(FolderAccess.ReadWrite, ct);

        IList<UniqueId> ids = [];
        if (uid > 0 && (long)box.UidValidity == uidValidity) ids = [new UniqueId((uint)uid)];
        else if (!string.IsNullOrWhiteSpace(messageId))
            ids = await box.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId.Trim('<', '>')), ct);
        if (ids.Count == 0) { await client.DisconnectAsync(true, ct); return; }

        if (trash is not null && trash.FullName != box.FullName) await box.MoveToAsync(ids, trash, ct);
        else await box.AddFlagsAsync(ids, MessageFlags.Deleted, true, ct);   // no Trash folder: mark deleted, the server's own clean-up removes it
        await client.DisconnectAsync(true, ct);
    }
}
