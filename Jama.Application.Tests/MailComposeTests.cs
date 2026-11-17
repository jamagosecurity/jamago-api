using Jama.Application.Common.Interfaces;
using Jama.Application.Mail;
using Jama.Application.Mail.Commands;
using Jama.Application.Options;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>The mail editor: formatting is kept but cleaned, Bcc stays hidden, attachments go out with the message.</summary>
public class MailComposeTests
{
    private sealed class FakeUser : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => "Hasan";
        public string Role => "Admin";
        public bool Has(string permission) => true;
        public bool IsSuperAdmin => true;
    }

    private sealed class FakeSender : IMailSender
    {
        public MailSendRequest? Last { get; private set; }
        public Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new MailSendResult(true, null, 7, 99));
        }
    }

    private static (SendMailCommandHandler Handler, FakeSender Sender, ApplicationDbContext Db) Make()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"compose-{Guid.NewGuid()}").Options);
        var sender = new FakeSender();
        var settings = Microsoft.Extensions.Options.Options.Create(new MailSettings
        {
            Accounts = [new MailAccountSettings { Key = "info", Address = "info@jamago.qa", Host = "imap.example.com", Password = "x" }],
        });
        return (new SendMailCommandHandler(db, sender, new FakeUser(), settings), sender, db);
    }

    [Theory]
    [InlineData("<b>Hello</b> <i>there</i>", "<b>Hello</b> <i>there</i>")]
    [InlineData("<strong>a</strong><em>b</em><u>c</u>", "<b>a</b><i>b</i><u>c</u>")]
    [InlineData("<ul><li>one</li><li>two</li></ul>", "<ul><li>one</li><li>two</li></ul>")]
    [InlineData("<script>alert(1)</script>Hi", "Hi")]
    [InlineData("<img src=x onerror=alert(1)>Hi", "Hi")]
    [InlineData("<b onclick=\"x()\" style=\"color:red\">Hi</b>", "<b>Hi</b>")]
    [InlineData("<a href=\"javascript:alert(1)\">click</a>", "click")]
    [InlineData("<a href=\"https://jamago.qa\" onclick=\"x\">site</a>", "<a href=\"https://jamago.qa\" style=\"color:#1f6fb8\">site</a>")]
    [InlineData("<b>never closed", "<b>never closed</b>")]
    [InlineData("</div></body><b>x</b>", "<b>x</b>")]
    [InlineData("1 < 2 &amp; 3 > 2", "1 &lt; 2 &amp; 3 &gt; 2")]
    [InlineData("<style>body{display:none}</style><p>ok</p>", "<p>ok</p>")]
    public void The_editor_html_keeps_only_safe_formatting(string input, string expected) =>
        Assert.Equal(expected, MailHtml.Clean(input));

    [Fact]
    public async Task Bcc_and_attachments_reach_the_sender_and_formatting_is_cleaned()
    {
        var (handler, sender, db) = Make();
        var pdf = Convert.ToBase64String("%PDF-1.4 test"u8.ToArray());

        var r = await handler.Handle(new SendMailCommand("info", ["client@example.com"], ["boss@example.com"], "Quote", "Hello team", null,
            Bcc: ["hidden@example.com", "client@example.com"],
            BodyHtml: "<b>Hello</b> team<script>x</script>",
            Attachments: [new SendMailAttachment("Quote.pdf", "application/pdf", pdf)]), default);

        Assert.True(r.Succeeded, string.Join(",", r.Errors));
        Assert.Equal(["hidden@example.com"], sender.Last!.Bcc);              // someone already in To is not repeated in Bcc
        Assert.Equal("Quote.pdf", sender.Last.Attachments!.Single().FileName);
        Assert.Equal("%PDF-1.4 test"u8.ToArray(), sender.Last.Attachments!.Single().Content);
        Assert.Contains("<b>Hello</b> team", sender.Last.HtmlBody);
        Assert.DoesNotContain("<script", sender.Last.HtmlBody);
        var saved = await db.MailMessages.SingleAsync();
        Assert.True(saved.HasAttachments);
        Assert.Equal("Quote.pdf", saved.AttachmentNames);
    }

    [Fact]
    public void Programs_too_many_files_and_bad_bcc_addresses_are_refused()
    {
        var v = new SendMailCommandValidator();
        var ok = new SendMailCommand("info", ["a@b.co"], null, "S", "B", null);
        Assert.True(v.Validate(ok).IsValid);
        Assert.False(v.Validate(ok with { Bcc = ["not an address"] }).IsValid);
        Assert.False(v.Validate(ok with { Attachments = [new SendMailAttachment("setup.exe", "application/octet-stream", "QUJD")] }).IsValid);
        Assert.False(v.Validate(ok with { Attachments = Enumerable.Range(0, 11).Select(i => new SendMailAttachment($"f{i}.pdf", "application/pdf", "QUJD")).ToList() }).IsValid);
        var big = new string('A', 28 * 1024 * 1024);                        // ~21 MB once decoded
        Assert.False(v.Validate(ok with { Attachments = [new SendMailAttachment("big.pdf", "application/pdf", big)] }).IsValid);
    }
}
