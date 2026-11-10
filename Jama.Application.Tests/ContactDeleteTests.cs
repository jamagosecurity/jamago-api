using Jama.Application.Contacts.Commands.DeleteContactSubmission;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

public class ContactDeleteTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"contacts-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task Deleting_an_enquiry_removes_only_that_enquiry()
    {
        var context = NewContext();
        var keep = new ContactSubmission { Id = Guid.NewGuid(), FullName = "Keep", Email = "k@example.test", Message = "hi", Service = "CCTV" };
        var drop = new ContactSubmission { Id = Guid.NewGuid(), FullName = "Drop", Email = "d@example.test", Message = "hi", Service = "CCTV" };
        context.ContactSubmissions.AddRange(keep, drop);
        await context.SaveChangesAsync();

        var result = await new DeleteContactSubmissionCommandHandler(context).Handle(
            new DeleteContactSubmissionCommand(drop.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(keep.Id, Assert.Single(context.ContactSubmissions).Id);
    }

    [Fact]
    public async Task Deleting_an_unknown_enquiry_fails_cleanly()
    {
        var result = await new DeleteContactSubmissionCommandHandler(NewContext()).Handle(
            new DeleteContactSubmissionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.Succeeded);
    }
}
