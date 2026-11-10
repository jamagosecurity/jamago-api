using MailKit;
using MailKit.Net.Imap;

namespace Jama.Infrastructure.Mail;

/// <summary>Finding a mailbox's Sent folder, shared by the reader and the sender.</summary>
internal static class ImapFolders
{
    /// <summary>The Sent folder: the server's own marking if it has one, else the usual names.</summary>
    internal static async Task<IMailFolder?> FindSentAsync(ImapClient client, CancellationToken ct)
    {
        if ((client.Capabilities & ImapCapabilities.SpecialUse) != 0 || client.Capabilities.HasFlag(ImapCapabilities.XList))
        {
            var special = client.GetFolder(SpecialFolder.Sent);
            if (special is not null) return special;
        }
        var ns = client.PersonalNamespaces[0];
        var all = await client.GetFoldersAsync(ns, cancellationToken: ct);
        string[] names = ["Sent", "Sent Items", "Sent Mail", "INBOX.Sent", "[Gmail]/Sent Mail"];
        return all.FirstOrDefault(f => names.Contains(f.FullName, StringComparer.OrdinalIgnoreCase)
                                    || names.Contains(f.Name, StringComparer.OrdinalIgnoreCase));
    }
}
