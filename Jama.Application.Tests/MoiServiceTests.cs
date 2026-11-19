using Jama.Application.MoiJobs.Commands;
using Jama.Application.MoiJobs.Services;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>The MOI price list is its own list: separate from the Non-MOI services and used for MOI jobs only.</summary>
public class MoiServiceTests
{
    private sealed class Technician(Guid id) : ICurrentUser
    {
        public Guid UserId { get; } = id;
        public string DisplayName => "Ali";
        public string Role => "Technician";
        public bool Has(string permission) => true;
        public bool IsSuperAdmin => false;
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"moi-svc-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task Services_are_added_to_their_own_list_with_free_categories_that_can_be_renamed()
    {
        await using var db = NewContext();
        var create = new CreateMoiServiceItemCommandHandler(db);

        var a = await create.Handle(new CreateMoiServiceItemCommand { Category = "  DIA   renewal ", Name = "Quarterly visit", Price = 1500 }, default);
        var b = await create.Handle(new CreateMoiServiceItemCommand { Category = "dia renewal", Name = "MOI submission", Price = 800 }, default);
        Assert.True(a.Succeeded && b.Succeeded, string.Join(",", a.Errors.Concat(b.Errors)));
        Assert.Equal("DIA renewal", a.Data!.Category);                     // spacing tidied
        Assert.Equal("DIA renewal", b.Data!.Category);                     // same category reused, whatever the case

        Assert.False((await create.Handle(new CreateMoiServiceItemCommand { Category = "DIA renewal", Name = "quarterly VISIT", Price = 1 }, default)).Succeeded);
        Assert.Empty(db.MaintenanceCatalogueItems);                        // nothing lands on the Non-MOI list

        var renamed = await new RenameMoiServiceCategoryCommandHandler(db).Handle(new RenameMoiServiceCategoryCommand { From = "DIA renewal", To = "MOI renewal" }, default);
        Assert.True(renamed.Succeeded, string.Join(",", renamed.Errors));
        var list = (await new GetMoiServiceCatalogueQueryHandler(db).Handle(new GetMoiServiceCatalogueQuery(), default)).Data!;
        Assert.All(list, i => Assert.Equal("MOI renewal", i.Category));
    }

    [Fact]
    public async Task An_MOI_job_is_priced_from_the_MOI_list_and_never_the_Non_MOI_one()
    {
        await using var db = NewContext();
        var tech = Guid.NewGuid();
        db.MoiServiceItems.Add(new MoiServiceItem { Id = Guid.NewGuid(), Category = "SLA", Name = "Camera alignment", Price = 300 });
        db.MaintenanceCatalogueItems.Add(new MaintenanceCatalogueItem { Id = Guid.NewGuid(), Category = "Camera", Name = "Camera replacement", Price = 500 });
        var job = new MoiJob { Id = Guid.NewGuid(), JobNumber = "MOJ-00001", CustomerName = "Msheireb", AssignedToId = tech, Status = "Assigned" };
        db.MoiJobs.Add(job);
        await db.SaveChangesAsync();
        var handler = new CompleteMoiJobCommandHandler(db, new Technician(tech));

        var wrongList = await handler.Handle(new CompleteMoiJobCommand(job.Id, [new CompleteJobLine("Camera", "Camera replacement", 1)], null), default);
        Assert.False(wrongList.Succeeded);                                 // a Non-MOI service cannot be ticked on an MOI job

        var done = await handler.Handle(new CompleteMoiJobCommand(job.Id, [new CompleteJobLine("SLA", "Camera alignment", 4)], "4 cameras"), default);
        Assert.True(done.Succeeded, string.Join(",", done.Errors));
        Assert.Equal(1200m, done.Data!.WorkTotal);
        Assert.Equal("Completed", done.Data.Status);
    }
}
