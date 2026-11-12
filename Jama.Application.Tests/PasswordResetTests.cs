using Jama.Application.Auth;
using Jama.Application.Auth.Commands.PasswordReset;
using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Tests;

/// <summary>Forgot password: a one-time, expiring emailed link; never reveals whether an address has an account.</summary>
public class PasswordResetTests
{
    private sealed class FakeMail : IMailSender
    {
        public List<MailSendRequest> Sent { get; } = [];
        public Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken) { Sent.Add(request); return Task.FromResult(new MailSendResult(true, null)); }
    }
    private sealed class RunNow : IBackgroundWork { public void Enqueue(Func<CancellationToken, Task> work) => work(default).GetAwaiter().GetResult(); }
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(AdminUser user, string password) => "H:" + password;
        public bool Verify(AdminUser user, string password) => user.PasswordHash == "H:" + password;
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }

    private static IOptions<MailSettings> Mail(bool configured = true) => Microsoft.Extensions.Options.Options.Create(new MailSettings
    {
        PublicSiteUrl = "https://jamago.qa",
        Accounts = configured ? [new MailAccountSettings { Key = "info", Address = "info@jamago.qa", Host = "smtp.example", Password = "x" }] : [],
    });

    private static async Task<(ApplicationDbContext Context, AdminUser User)> SeedAsync()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"pr-{Guid.NewGuid()}").Options);
        var user = new AdminUser { Id = Guid.NewGuid(), Email = "aisha@jamago.qa", FullName = "Aisha", Role = "Staff", PasswordHash = "H:OldPass123", IsActive = true };
        context.AdminUsers.Add(user);
        await context.SaveChangesAsync();
        return (context, user);
    }

    private static string TokenFrom(MailSendRequest mail) => mail.Body.Split("token=")[1].Split('\n')[0].Trim();

    [Fact]
    public async Task A_known_address_gets_a_link_and_only_a_hash_of_the_code_is_stored()
    {
        var (context, user) = await SeedAsync();
        var mail = new FakeMail();
        var result = await new RequestPasswordResetCommandHandler(context, mail, new RunNow(), Mail(), TimeProvider.System)
            .Handle(new RequestPasswordResetCommand("  AISHA@jamago.qa "), default);

        Assert.True(result.Succeeded);
        var sent = Assert.Single(mail.Sent);
        Assert.Equal("aisha@jamago.qa", Assert.Single(sent.To));
        Assert.Contains("https://jamago.qa/admin/reset-password?email=aisha%40jamago.qa&token=", sent.Body);
        var token = TokenFrom(sent);
        var stored = (await context.AdminUsers.SingleAsync()).PasswordResetTokenHash;
        Assert.NotEqual(token, stored);                                  // never the code itself
        Assert.Equal(PasswordResetRules.Hash(token), stored);
    }

    [Fact]
    public async Task An_unknown_address_gets_the_same_answer_and_no_email()
    {
        var (context, _) = await SeedAsync();
        var mail = new FakeMail();
        var result = await new RequestPasswordResetCommandHandler(context, mail, new RunNow(), Mail(), TimeProvider.System)
            .Handle(new RequestPasswordResetCommand("nobody@example.com"), default);

        Assert.True(result.Succeeded);
        Assert.Equal(PasswordResetRules.Sent, result.Data);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task The_link_sets_a_new_password_once()
    {
        var (context, _) = await SeedAsync();
        var mail = new FakeMail();
        await new RequestPasswordResetCommandHandler(context, mail, new RunNow(), Mail(), TimeProvider.System).Handle(new RequestPasswordResetCommand("aisha@jamago.qa"), default);
        var token = TokenFrom(mail.Sent[0]);
        var reset = new ResetPasswordCommandHandler(context, new FakeHasher(), TimeProvider.System);

        var first = await reset.Handle(new ResetPasswordCommand("aisha@jamago.qa", token, "NewPass456"), default);
        Assert.True(first.Succeeded);
        Assert.Equal("H:NewPass456", (await context.AdminUsers.SingleAsync()).PasswordHash);

        var again = await reset.Handle(new ResetPasswordCommand("aisha@jamago.qa", token, "Other789Pass"), default);
        Assert.False(again.Succeeded);                                   // one use only
        Assert.Equal("H:NewPass456", (await context.AdminUsers.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task A_wrong_or_expired_code_changes_nothing()
    {
        var (context, _) = await SeedAsync();
        var mail = new FakeMail();
        var clock = new Clock(DateTimeOffset.UtcNow);
        await new RequestPasswordResetCommandHandler(context, mail, new RunNow(), Mail(), clock).Handle(new RequestPasswordResetCommand("aisha@jamago.qa"), default);
        var token = TokenFrom(mail.Sent[0]);

        Assert.False((await new ResetPasswordCommandHandler(context, new FakeHasher(), clock).Handle(new ResetPasswordCommand("aisha@jamago.qa", "abc123", "NewPass456"), default)).Succeeded);

        clock.Now = clock.Now.AddMinutes(31);
        Assert.False((await new ResetPasswordCommandHandler(context, new FakeHasher(), clock).Handle(new ResetPasswordCommand("aisha@jamago.qa", token, "NewPass456"), default)).Succeeded);
        Assert.Equal("H:OldPass123", (await context.AdminUsers.SingleAsync()).PasswordHash);
    }

    [Fact]
    public void The_new_password_must_be_strong()
    {
        var v = new ResetPasswordCommandValidator();
        Assert.False(v.Validate(new ResetPasswordCommand("a@b.c", "t", "weak")).IsValid);
        Assert.False(v.Validate(new ResetPasswordCommand("a@b.c", "t", "alllowercase1")).IsValid);
        Assert.True(v.Validate(new ResetPasswordCommand("a@b.c", "t", "Strong123")).IsValid);
    }

    [Fact]
    public async Task Without_a_mailbox_it_says_to_ask_an_administrator()
    {
        var (context, _) = await SeedAsync();
        var result = await new RequestPasswordResetCommandHandler(context, new FakeMail(), new RunNow(), Mail(configured: false), TimeProvider.System)
            .Handle(new RequestPasswordResetCommand("aisha@jamago.qa"), default);
        Assert.False(result.Succeeded);
        Assert.Contains("administrator", result.Errors.Single());
    }
}
