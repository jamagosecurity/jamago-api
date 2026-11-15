using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using MailKit;
using MailKit.Net.Imap;
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
}
