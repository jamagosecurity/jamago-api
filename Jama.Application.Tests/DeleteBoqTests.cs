using Jama.Application.Boqs.Commands.DeleteBoq;
using Jama.Application.Boqs.Commands.DuplicateBoq;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>Deleting a quotation is soft: hidden everywhere, kept in the database, number never reused.</summary>
public class DeleteBoqTests
{
    private sealed class FakeUser(Guid id) : ICurrentUser
    {
        public Guid UserId { get; } = id;
        public string DisplayName => "Sara";
        public string Role => Roles.Staff;
        public bool Has(string permission) => true;
        public bool IsSuperAdmin => false;
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"del-{Guid.NewGuid()}").Options);

    private static Boq Quote(string number, Guid by) => new()
    {
        Id = Guid.NewGuid(), BoqNumber = number, ProjectName = "P", PreparedById = by, Status = BoqStatus.Draft,
        Sections = [new BoqSection { Id = Guid.NewGuid(), Title = "Main CCTV System", Lines = [new BoqLine { Id = Guid.NewGuid(), ItemName = "NVR", Uom = UnitOfMeasurement.Piece, Quantity = 1 }] }],
    };

    [Fact]
    public async Task Deleting_hides_the_quotation_but_keeps_the_row_and_who_deleted_it()
    {
        var me = new FakeUser(Guid.NewGuid());
        var context = NewContext();
        var boq = Quote("JMG-00001", me.UserId);
        context.Boqs.Add(boq);
        await context.SaveChangesAsync();

        var result = await new DeleteBoqCommandHandler(context, me, TimeProvider.System).Handle(new DeleteBoqCommand(boq.Id), default);

        Assert.True(result.Succeeded);
        Assert.Empty(await context.Boqs.ToListAsync());                       // hidden from every normal query
        Assert.Empty(await context.BoqLines.ToListAsync());                   // and so are its lines
        var kept = await context.Boqs.IgnoreQueryFilters().Include(b => b.Sections).ThenInclude(s => s.Lines).SingleAsync();
        Assert.True(kept.IsDeleted);
        Assert.Equal(me.UserId, kept.DeletedById);
        Assert.NotNull(kept.DeletedAt);
        Assert.Single(kept.Sections.Single().Lines);                           // nothing was destroyed
    }

    [Fact]
    public async Task A_deleted_quotation_cannot_be_deleted_again_or_found()
    {
        var me = new FakeUser(Guid.NewGuid());
        var context = NewContext();
        var boq = Quote("JMG-00001", me.UserId);
        context.Boqs.Add(boq);
        await context.SaveChangesAsync();
        var handler = new DeleteBoqCommandHandler(context, me, TimeProvider.System);
        await handler.Handle(new DeleteBoqCommand(boq.Id), default);

        var again = await handler.Handle(new DeleteBoqCommand(boq.Id), default);
        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task A_number_is_never_issued_again_after_its_quotation_is_deleted()
    {
        var me = new FakeUser(Guid.NewGuid());
        var context = NewContext();
        var boq = Quote("JMG-00007", me.UserId);
        boq.Status = BoqStatus.Approved;
        context.Boqs.Add(boq);
        await context.SaveChangesAsync();

        var first = await new DuplicateBoqCommandHandler(context, me, TimeProvider.System).Handle(new DuplicateBoqCommand(boq.Id), default);
        Assert.Equal("JMG-00008", first.Data!.BoqNumber);
        await new DeleteBoqCommandHandler(context, me, TimeProvider.System).Handle(new DeleteBoqCommand(first.Data.Id), default);

        var second = await new DuplicateBoqCommandHandler(context, me, TimeProvider.System).Handle(new DuplicateBoqCommand(boq.Id), default);
        Assert.Equal("JMG-00009", second.Data!.BoqNumber);                     // 00008 stays retired
    }

    [Fact]
    public async Task A_deleted_quotation_cannot_be_duplicated()
    {
        var me = new FakeUser(Guid.NewGuid());
        var context = NewContext();
        var boq = Quote("JMG-00001", me.UserId);
        context.Boqs.Add(boq);
        await context.SaveChangesAsync();
        await new DeleteBoqCommandHandler(context, me, TimeProvider.System).Handle(new DeleteBoqCommand(boq.Id), default);

        var result = await new DuplicateBoqCommandHandler(context, me, TimeProvider.System).Handle(new DuplicateBoqCommand(boq.Id), default);
        Assert.False(result.Succeeded);
    }
}
