using MailKit;
using MailKit.Net.Imap;

namespace Jama.Infrastructure.Mail;

/// <summary>Finding a mailbox's Sent folder, shared by the reader and the sender.</summary>
internal static class ImapFolders
{
    /// <summary>
    /// Connects and signs in, trying again a few times when the network hiccups (a dropped TLS handshake, a timeout),
    /// so a moment's glitch is never reported as a broken mailbox. A wrong password is not retried: it fails at once.
    /// </summary>
    internal static async Task<ImapClient> ConnectAsync(Jama.Application.Options.MailAccountSettings account, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var client = new ImapClient { Timeout = 30_000 };
            try
            {
                await client.ConnectAsync(account.Host, account.Port, account.UseSsl
                    ? MailKit.Security.SecureSocketOptions.SslOnConnect
                    : MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable, ct);
                await client.AuthenticateAsync(account.Login, account.Password, ct);
                return client;
            }
            catch (MailKit.Security.AuthenticationException)
            {
                client.Dispose();
                throw;
            }
            catch (Exception) when (attempt < 3 && !ct.IsCancellationRequested)
            {
                client.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt), ct);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
    }

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
