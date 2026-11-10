using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Mail.Commands;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Tests;

/// <summary>Sending, replying and forwarding from the company mailboxes: who may, what is checked, what is logged.</summary>
public class MailSendTests
{
    private sealed class FakeUser : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => "Hasan";
        public string Role => Roles.Admin;
        public bool Has(string permission) => true;
        public bool IsSuperAdmin => true;
    }

    private sealed class FakeSender(MailSendResult result) : IMailSender
    {
        public MailSendRequest? Last { get; private set; }
        public Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(result);
        }
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"mail-{Guid.NewGuid()}").Options);

    private static IOptions<MailSettings> Settings(string password = "secret") => Microsoft.Extensions.Options.Options.Create(new MailSettings
    {
        Accounts = [new MailAccountSettings { Key = "info", Address = "info@jamago.qa", Host = "imap.example.com", Password = password }],
    });

    private static SendMailCommand Valid() => new("info", ["client@example.com"], null, "Quote", "Hello", null);

    [Fact]
    public void A_complete_message_is_valid()
    {
        Assert.True(new SendMailCommandValidator().Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a b@example.com")]
    [InlineData("<a@example.com>")]
    [InlineData("a@b")]
    [InlineData("a@@example.com")]
    [InlineData("a@example.com\r\nBcc: x@evil.com")]
    public void A_bad_recipient_is_refused(string address)
    {
        Assert.False(new SendMailCommandValidator().Validate(Valid() with { To = [address] }).IsValid);
    }

    [Fact]
    public void Recipients_subject_and_body_are_required_and_the_subject_is_one_line()
    {
        var validator = new SendMailCommandValidator();
        Assert.False(validator.Validate(Valid() with { To = [] }).IsValid);
        Assert.False(validator.Validate(Valid() with { Subject = " " }).IsValid);
        Assert.False(validator.Validate(Valid() with { Body = "" }).IsValid);
        Assert.False(validator.Validate(Valid() with { Subject = "Hi\r\nBcc: x@evil.com" }).IsValid);
        Assert.False(validator.Validate(Valid() with { AccountKey = "" }).IsValid);
    }

    [Fact]
    public async Task A_send_goes_out_and_is_logged_with_who_sent_it()
    {
        await using var context = NewContext();
        var sender = new FakeSender(new MailSendResult(true, null, 7, 42));
        var handler = new SendMailCommandHandler(context, sender, new FakeUser(), Settings());

        var result = await handler.Handle(Valid() with { To = [" Client@Example.com ", "client@example.com"], Cc = ["boss@example.com"] }, default);

        Assert.True(result.Succeeded);
        Assert.Equal(["Client@Example.com"], sender.Last!.To);            // trimmed and de-duplicated
        Assert.Equal(["boss@example.com"], sender.Last.Cc);
        var log = await context.MailSendLogs.SingleAsync();
        Assert.Equal("Hasan", log.SentByName);
        Assert.Equal("info@jamago.qa", log.FromAddress);
        Assert.True(log.CopySavedToSent);
        // It appears under Outgoing at once, with the UID the server gave it, so the next sync won't add it twice.
        var shown = await context.MailMessages.SingleAsync();
        Assert.Equal("Sent", shown.Folder);
        Assert.Equal(42, shown.Uid);
    }

    [Fact]
    public async Task A_reply_threads_under_the_original_message()
    {
        await using var context = NewContext();
        var original = new MailMessage { Id = Guid.NewGuid(), AccountKey = "info", AccountAddress = "info@jamago.qa", MessageId = "<abc@client.example>" };
        context.MailMessages.Add(original);
        await context.SaveChangesAsync();
        var sender = new FakeSender(new MailSendResult(true, null));
        var handler = new SendMailCommandHandler(context, sender, new FakeUser(), Settings());

        var result = await handler.Handle(Valid() with { ReplyToMessageId = original.Id }, default);

        Assert.True(result.Succeeded);
        Assert.Equal("<abc@client.example>", sender.Last!.InReplyToMessageId);
        Assert.False((await context.MailSendLogs.SingleAsync()).CopySavedToSent);
    }

    [Fact]
    public async Task A_failed_send_is_reported_and_not_logged()
    {
        await using var context = NewContext();
        var handler = new SendMailCommandHandler(context, new FakeSender(new MailSendResult(false, "Could not send: bad login")), new FakeUser(), Settings());

        var result = await handler.Handle(Valid(), default);

        Assert.False(result.Succeeded);
        Assert.Empty(context.MailSendLogs);
    }

    [Fact]
    public async Task A_mailbox_without_a_password_cannot_send()
    {
        await using var context = NewContext();
        var sender = new FakeSender(new MailSendResult(true, null));
        var handler = new SendMailCommandHandler(context, sender, new FakeUser(), Settings(password: ""));

        var result = await handler.Handle(Valid(), default);

        Assert.False(result.Succeeded);
        Assert.Null(sender.Last);
    }

    [Fact]
    public async Task An_unknown_mailbox_cannot_send()
    {
        await using var context = NewContext();
        var handler = new SendMailCommandHandler(context, new FakeSender(new MailSendResult(true, null)), new FakeUser(), Settings());
        Assert.False((await handler.Handle(Valid() with { AccountKey = "nope" }, default)).Succeeded);
    }

    [Theory]
    [InlineData("imap.hostinger.com", "smtp.hostinger.com")]
    [InlineData("imap.zoho.com", "smtp.zoho.com")]
    [InlineData("imap.gmail.com", "smtp.gmail.com")]
    public void The_outgoing_server_is_worked_out_from_the_incoming_one(string imap, string smtp)
    {
        Assert.Equal(smtp, new MailAccountSettings { Host = imap }.ResolvedSmtpHost);
        Assert.Equal("mail.custom.com", new MailAccountSettings { Host = imap, SmtpHost = "mail.custom.com" }.ResolvedSmtpHost);
    }
}
