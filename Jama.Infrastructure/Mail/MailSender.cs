using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Jama.Infrastructure.Mail;

/// <summary>
/// Sends through the company mailbox's own outgoing server, as that mailbox, then saves a copy in
/// its Sent folder so the owner sees it there too. The one place the app writes to a mailbox.
/// </summary>
public sealed class MailSender(IOptions<MailSettings> options, ILogger<MailSender> logger) : IMailSender
{
    public async Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken)
    {
        var account = options.Value.Accounts.FirstOrDefault(a =>
            string.Equals(a.Key, request.AccountKey, StringComparison.OrdinalIgnoreCase) && a.IsConfigured);
        if (account is null)
            return new MailSendResult(false, "That mailbox is not set up for sending.");

        MimeMessage message;
        try
        {
            message = Build(account, request);
        }
        catch (FormatException)
        {
            return new MailSendResult(false, "One of the email addresses is not valid.");
        }

        try
        {
            // Introduce ourselves as the company's domain, not this computer's name: a ".local" host name in the
            // greeting is a classic spam signal and Gmail files such mail under Spam.
            using var smtp = new SmtpClient { Timeout = 30_000, LocalDomain = DomainOf(account.Address) };
            await smtp.ConnectAsync(account.ResolvedSmtpHost, account.SmtpPort,
                account.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
            await smtp.AuthenticateAsync(account.Login, account.Password, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // The message names the cause (bad login, refused recipient); it never contains the password.
            logger.LogWarning("Mail send from {Key} failed: {Message}", account.Key, ex.Message);
            return new MailSendResult(false, "Could not send: " + ex.Message);
        }

        // The message is already gone. Saving the Sent copy is best-effort: failing here must not look like a failed send.
        try
        {
            using var imap = new ImapClient { Timeout = 30_000 };
            await imap.ConnectAsync(account.Host, account.Port, account.UseSsl
                ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            await imap.AuthenticateAsync(account.Login, account.Password, cancellationToken);
            var sent = await ImapFolders.FindSentAsync(imap, cancellationToken);
            if (sent is null) return new MailSendResult(true, null);

            var uid = await sent.AppendAsync(message, MessageFlags.Seen, cancellationToken);
            await sent.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
            await imap.DisconnectAsync(true, cancellationToken);
            return uid is { } u
                ? new MailSendResult(true, null, (long)sent.UidValidity, (long)u.Id)
                : new MailSendResult(true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Mail sent from {Key} but the Sent copy could not be saved: {Message}", account.Key, ex.Message);
            return new MailSendResult(true, null);
        }
    }

    private static string DomainOf(string address) =>
        address.Contains('@') ? address[(address.LastIndexOf('@') + 1)..] : "localhost";

    private static MimeMessage Build(MailAccountSettings account, MailSendRequest r)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(r.FromDisplayName, account.Address));
        foreach (var to in r.To) message.To.Add(MailboxAddress.Parse(to));
        foreach (var cc in r.Cc) message.Cc.Add(MailboxAddress.Parse(cc));
        message.Subject = r.Subject;
        message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId(DomainOf(account.Address));
        message.Date = DateTimeOffset.Now;
        if (!string.IsNullOrWhiteSpace(r.InReplyToMessageId))
        {
            // So the recipient's mail app files the reply under the original conversation.
            var id = r.InReplyToMessageId.Trim('<', '>', ' ');
            message.InReplyTo = id;
            message.References.Add(id);
        }
        var body = new BodyBuilder { TextBody = r.Body, HtmlBody = r.HtmlBody };
        foreach (var file in r.Attachments ?? [])
            body.Attachments.Add(file.FileName, file.Content, ContentType.Parse(file.ContentType));
        message.Body = body.ToMessageBody();
        return message;
    }
}
