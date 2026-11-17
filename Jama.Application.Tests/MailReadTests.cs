using Jama.Application.Common.Interfaces;
using Jama.Application.Mail.Commands;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>Read / unread: set here at once, and passed to the mail server so every mail app agrees.</summary>
public class MailReadTests
{
    private sealed class FakeFlags : IMailFlagWriter
    {
        public List<(string Key, long Uid, bool Seen)> Calls { get; } = [];
        public Task SetSeenAsync(string accountKey, string folder, long uidValidity, long uid, bool seen, CancellationToken ct)
        {
            Calls.Add((accountKey, uid, seen));
            return Task.CompletedTask;
        }

        public List<(string Key, string Folder, long Uid, string? MessageId)> Trashed { get; } = [];
        public Task MoveToTrashAsync(string accountKey, string folder, long uidValidity, long uid, string? messageId, CancellationToken ct)
        {
            Trashed.Add((accountKey, folder, uid, messageId));
            return Task.CompletedTask;
        }
    }

    private sealed class InlineWork : IBackgroundWork
    {
        public void Enqueue(Func<CancellationToken, Task> work) => work(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Opening_marks_it_read_here_and_on_the_server_and_it_can_be_marked_unread_again()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"read-{Guid.NewGuid()}").Options);
        var message = new MailMessage { Id = Guid.NewGuid(), AccountKey = "info", Folder = "Inbox", UidValidity = 5, Uid = 77, IsRead = false };
        context.MailMessages.Add(message);
        await context.SaveChangesAsync();
        var flags = new FakeFlags();
        var handler = new SetMailReadCommandHandler(context, flags, new InlineWork(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SetMailReadCommandHandler>.Instance);

        Assert.True((await handler.Handle(new SetMailReadCommand(message.Id, true), default)).Succeeded);
        Assert.True((await context.MailMessages.SingleAsync()).IsRead);
        Assert.Equal(("info", 77L, true), flags.Calls.Single());

        await handler.Handle(new SetMailReadCommand(message.Id, false), default);
        Assert.False((await context.MailMessages.SingleAsync()).IsRead);
        Assert.Equal(("info", 77L, false), flags.Calls[1]);

        await handler.Handle(new SetMailReadCommand(message.Id, false), default);   // no change, nothing sent to the server
        Assert.Equal(2, flags.Calls.Count);
    }

    [Fact]
    public async Task Mail_that_came_through_the_copy_mailbox_is_marked_there()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"read-{Guid.NewGuid()}").Options);
        var message = new MailMessage { Id = Guid.NewGuid(), AccountKey = "youssef", SourceKey = "accounts", Folder = "Inbox", UidValidity = 9, Uid = 12, IsRead = false };
        context.MailMessages.Add(message);
        await context.SaveChangesAsync();
        var flags = new FakeFlags();

        await new SetMailReadCommandHandler(context, flags, new InlineWork(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SetMailReadCommandHandler>.Instance)
            .Handle(new SetMailReadCommand(message.Id, true), default);

        Assert.Equal(("accounts", 12L, true), flags.Calls.Single());   // the copy mailbox holds that UID, not Youssef's
    }

    [Fact]
    public async Task Deleting_removes_it_here_and_moves_it_to_Trash_on_the_right_mailbox()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"del-{Guid.NewGuid()}").Options);
        var inbox = new MailMessage { Id = Guid.NewGuid(), AccountKey = "youssef", SourceKey = "accounts", Folder = "Inbox", UidValidity = 9, Uid = 12 };
        var justSent = new MailMessage { Id = Guid.NewGuid(), AccountKey = "info", SourceKey = "info", Folder = "Sent", Uid = -5, MessageId = "abc@jamago.qa" };
        var kept = new MailMessage { Id = Guid.NewGuid(), AccountKey = "info", Folder = "Inbox", Uid = 3 };
        context.MailMessages.AddRange(inbox, justSent, kept);
        await context.SaveChangesAsync();
        var flags = new FakeFlags();

        var r = await new Jama.Application.Mail.Commands.DeleteMailCommandHandler(context, flags, new InlineWork(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Jama.Application.Mail.Commands.DeleteMailCommandHandler>.Instance)
            .Handle(new Jama.Application.Mail.Commands.DeleteMailCommand([inbox.Id, justSent.Id]), default);

        Assert.Equal(2, r.Data);
        Assert.Equal(kept.Id, (await context.MailMessages.SingleAsync()).Id);
        Assert.Contains(("accounts", "Inbox", 12L, (string?)null), flags.Trashed);        // the copy mailbox holds that UID
        Assert.Contains(("info", "Sent", -5L, (string?)"abc@jamago.qa"), flags.Trashed); // found by Message-ID on the server
    }
}
