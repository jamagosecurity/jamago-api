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
public sealed class MailSender(IOptions<MailSettings> options, ILogger<MailSender> logger, IBackgroundWork background) : IMailSender
{
    /// <summary>What an email's HTML writes (as <c>cid:jamago-logo</c>) to show the company logo.</summary>
    public const string LogoContentId = "jamago-logo";

    /// <summary>What an email's HTML writes (as <c>cid:jamago-moi</c>) to show the MOI-SSD approval badge.</summary>
    public const string MoiBadgeContentId = "jamago-moi";

    private static readonly byte[] EmailLogo = Load("email-logo.png");
    private static readonly byte[] MoiBadge = Load("moi-ssd.jpg");

    private static byte[] Load(string name)
    {
        using var stream = typeof(MailSender).Assembly.GetManifestResourceStream("Jama.Infrastructure.Mail.Assets." + name);
        if (stream is null) return [];
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

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
        if (request.SaveCopyInBackground)
        {
            // The message has gone out; filing the copy in Sent can happen after we answer.
            background.Enqueue(ct => SaveCopyAsync(account, message, ct));
            return new MailSendResult(true, null);
        }
        return await SaveCopyAsync(account, message, cancellationToken);
    }

    private async Task<MailSendResult> SaveCopyAsync(MailAccountSettings account, MimeMessage message, CancellationToken cancellationToken)
    {
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
        foreach (var bcc in r.Bcc ?? []) message.Bcc.Add(MailboxAddress.Parse(bcc));
        message.Subject = r.Subject;
        message.MessageId = string.IsNullOrWhiteSpace(r.MessageId) ? MimeKit.Utils.MimeUtils.GenerateMessageId(DomainOf(account.Address)) : r.MessageId.Trim('<', '>', ' ');
        message.Date = DateTimeOffset.Now;
        if (!string.IsNullOrWhiteSpace(r.InReplyToMessageId))
        {
            // So the recipient's mail app files the reply under the original conversation.
            var id = r.InReplyToMessageId.Trim('<', '>', ' ');
            message.InReplyTo = id;
            message.References.Add(id);
        }
        var body = new BodyBuilder { TextBody = r.Body, HtmlBody = r.HtmlBody };
        // HTML that asks for the company logo gets it inside the message, so it shows even where the mail app blocks
        // downloaded pictures.
        if (r.HtmlBody?.Contains("cid:" + LogoContentId, StringComparison.Ordinal) == true && EmailLogo.Length > 0)
        {
            var logo = body.LinkedResources.Add("jamago-logo.png", EmailLogo, new ContentType("image", "png"));
            logo.ContentId = LogoContentId;
        }
        if (r.HtmlBody?.Contains("cid:" + MoiBadgeContentId, StringComparison.Ordinal) == true && MoiBadge.Length > 0)
        {
            var badge = body.LinkedResources.Add("moi-ssd.jpg", MoiBadge, new ContentType("image", "jpeg"));
            badge.ContentId = MoiBadgeContentId;
        }
        foreach (var file in r.Attachments ?? [])
            body.Attachments.Add(file.FileName, file.Content, ContentType.Parse(file.ContentType));
        message.Body = body.ToMessageBody();
        return message;
    }
}
