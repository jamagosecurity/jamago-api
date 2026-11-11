using Jama.Application.Boqs;
using Jama.Application.Boqs.Commands.DuplicateBoq;
using Jama.Application.Boqs.Commands.UpdateBoq;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>Duplicating a quotation: a full, independent copy as a new Draft; the original is never touched.</summary>
public class DuplicateBoqTests
{
    private static readonly Guid Author = Guid.NewGuid();

    private sealed class FakeUser(Guid id, string name = "Sara", bool canApprove = false) : ICurrentUser
    {
        public Guid UserId { get; } = id;
        public string DisplayName { get; } = name;
        public string Role => Roles.Staff;
        public bool Has(string permission) => permission == Permissions.BoqManage || (canApprove && permission == Permissions.BoqApprove);
        public bool IsSuperAdmin => false;
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"dup-{Guid.NewGuid()}").Options);

    private static async Task<(ApplicationDbContext Context, Boq Original)> SeedAsync(BoqStatus status = BoqStatus.Approved)
    {
        var context = NewContext();
        var boq = new Boq
        {
            Id = Guid.NewGuid(), BoqNumber = "JMG-00010", ProjectName = "Pearl Tower", SiteLocation = "West Bay", ClientName = "Al Noor",
            ContactNumber = "+974 5550 1234", IssueDate = new DateOnly(2026, 1, 5), Status = status, Notes = "Lobby", TermsAndConditions = "Custom terms",
            PreparedById = Author, PreparedByName = "Original Author", Total = 3000m, SpecialDiscount = 100m, GrandTotal = 2900m,
            RevisionNumber = 4, ContractText = "agreed wording", ApprovedAt = DateTime.UtcNow, ApprovedById = Guid.NewGuid(), ApprovedByName = "Boss",
            Sections =
            [
                new BoqSection
                {
                    Id = Guid.NewGuid(), Title = "Main CCTV System", SortOrder = 0,
                    Lines =
                    [
                        new BoqLine { Id = Guid.NewGuid(), CameraId = Guid.NewGuid(), ItemName = "NVR 32", Brand = "Hikvision", ModelNo = "DS-7732", Uom = UnitOfMeasurement.Piece, Quantity = 2, UnitRate = 1200m, CatalogueRate = 1300m, LineTotal = 2400m, SortOrder = 0 },
                        new BoqLine { Id = Guid.NewGuid(), CameraId = Guid.NewGuid(), ItemName = "Cable roll", Uom = UnitOfMeasurement.Piece, Quantity = 4, UnitRate = 150m, CatalogueRate = 150m, LineTotal = 600m, SortOrder = 1 },
                    ],
                },
            ],
        };
        context.Boqs.Add(boq);
        await context.SaveChangesAsync();
        return (context, boq);
    }

    private static Task<Jama.Application.Common.Models.ApiResult<BoqDto>> Duplicate(ApplicationDbContext context, Guid id, ICurrentUser user) =>
        new DuplicateBoqCommandHandler(context, user, TimeProvider.System).Handle(new DuplicateBoqCommand(id), default);

    [Fact]
    public async Task A_duplicate_is_a_new_draft_with_its_own_number_and_the_same_content()
    {
        var (context, original) = await SeedAsync();
        var me = new FakeUser(Guid.NewGuid(), "Fatima");

        var result = await Duplicate(context, original.Id, me);

        Assert.True(result.Succeeded);
        var copy = await context.Boqs.Include(b => b.Sections).ThenInclude(s => s.Lines).SingleAsync(b => b.Id == result.Data!.Id);
        Assert.NotEqual(original.Id, copy.Id);
        Assert.NotEqual("JMG-00010", copy.BoqNumber);
        Assert.Equal(BoqStatus.Draft, copy.Status);
        Assert.Equal("Fatima", copy.PreparedByName);                // whoever pressed the button owns the copy
        Assert.Equal(me.UserId, copy.PreparedById);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), copy.IssueDate);
        Assert.Equal(("Pearl Tower", "West Bay", "Al Noor", "+974 5550 1234", "Lobby", "Custom terms"),
            (copy.ProjectName, copy.SiteLocation, copy.ClientName, copy.ContactNumber, copy.Notes, copy.TermsAndConditions));
        Assert.Equal((3000m, 100m, 2900m), (copy.Total, copy.SpecialDiscount, copy.GrandTotal));

        var lines = copy.Sections.Single().Lines.OrderBy(l => l.SortOrder).ToList();
        Assert.Equal(["NVR 32", "Cable roll"], lines.Select(l => l.ItemName));
        Assert.Equal([2m, 4m], lines.Select(l => l.Quantity));
        Assert.Equal([1200m, 150m], lines.Select(l => l.UnitRate));      // a negotiated rate comes across…
        Assert.Equal([1300m, 150m], lines.Select(l => l.CatalogueRate)); // …along with the list price it was negotiated from
    }

    [Fact]
    public async Task What_describes_the_originals_life_is_not_copied()
    {
        var (context, original) = await SeedAsync();

        var copyId = (await Duplicate(context, original.Id, new FakeUser(Guid.NewGuid()))).Data!.Id;
        var copy = await context.Boqs.SingleAsync(b => b.Id == copyId);

        Assert.Equal(0, copy.RevisionNumber);
        Assert.Null(copy.ContractText);
        Assert.Null(copy.ApprovedAt);
        Assert.Null(copy.ApprovedById);
        var trail = await context.BoqApprovalEvents.Where(e => e.BoqId == copy.Id).ToListAsync();
        var created = Assert.Single(trail);
        Assert.Equal(BoqApprovalAction.Created, created.Action);
        Assert.Contains("JMG-00010", created.Reason);
    }

    [Fact]
    public async Task Changing_the_copy_never_changes_the_original()
    {
        var (context, original) = await SeedAsync();
        var me = new FakeUser(Guid.NewGuid());
        var copyId = (await Duplicate(context, original.Id, me)).Data!.Id;

        // Rename the copy and drop its sections entirely.
        var edit = await new UpdateBoqCommandHandler(context, me, TimeProvider.System).Handle(
            new UpdateBoqCommand { Id = copyId, ProjectName = "Pearl Tower — Phase 2", Sections = [] }, default);
        Assert.True(edit.Succeeded);

        var original2 = await context.Boqs.Include(b => b.Sections).ThenInclude(s => s.Lines).SingleAsync(b => b.Id == original.Id);
        Assert.Equal("Pearl Tower", original2.ProjectName);
        Assert.Equal(BoqStatus.Approved, original2.Status);
        Assert.Equal(2, original2.Sections.Single().Lines.Count);
        Assert.Equal(2900m, original2.GrandTotal);
        // And the two share no rows at all.
        var all = await context.BoqLines.Select(l => l.Id).ToListAsync();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public async Task Duplicating_twice_gives_two_separate_quotations()
    {
        var (context, original) = await SeedAsync();
        var me = new FakeUser(Guid.NewGuid());

        var a = (await Duplicate(context, original.Id, me)).Data!;
        var b = (await Duplicate(context, original.Id, me)).Data!;

        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEqual(a.BoqNumber, b.BoqNumber);
        Assert.Equal(3, await context.Boqs.CountAsync());
    }

    [Fact]
    public async Task Someone_who_cannot_see_the_original_cannot_copy_it()
    {
        var (context, draft) = await SeedAsync(BoqStatus.Draft);                 // someone else's draft is private
        var stranger = new FakeUser(Guid.NewGuid());

        var result = await Duplicate(context, draft.Id, stranger);

        Assert.False(result.Succeeded);
        Assert.Equal(1, await context.Boqs.CountAsync());                        // nothing was created
        Assert.False((await Duplicate(context, Guid.NewGuid(), stranger)).Succeeded);   // a missing one answers the same way
    }

    [Fact]
    public async Task Any_status_can_be_copied_by_someone_who_can_see_it_including_a_rejected_one()
    {
        foreach (var status in new[] { BoqStatus.Approved, BoqStatus.Submitted })
        {
            var (context, original) = await SeedAsync(status);
            var asAuthor = new FakeUser(Author);
            Assert.True((await Duplicate(context, original.Id, asAuthor)).Succeeded);
        }
    }
}
