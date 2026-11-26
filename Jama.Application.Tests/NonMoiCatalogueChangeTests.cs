using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Maintenance.Commands.CreateMaintenanceCatalogueItem;
using Jama.Application.Maintenance.Commands.UpdateMaintenanceCatalogueItem;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// Non-MOI technicians keep the Non-MOI inventory up to date as well as the office: they add items (live at once)
/// and change prices. Every addition and change is kept with who made it and when.
/// </summary>
public class NonMoiCatalogueChangeTests
{
    private sealed class Person(string name, string permission) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => name;
        public string Role => Roles.Staff;
        public bool Has(string p) => p == permission;
        public bool IsSuperAdmin => false;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly Person Technician = new("Ali <ali@jamago.qa>", Permissions.NonMoiJobWork);
    private static readonly Person Office = new("Sara <sara@jamago.qa>", Permissions.NonMoiJobManage);

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"catalogue-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task A_technician_adds_an_item_and_it_is_live_with_who_added_it()
    {
        var db = NewContext();
        var clock = new Clock(new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero));

        var added = await new CreateMaintenanceCatalogueItemCommandHandler(db, Technician, clock).Handle(
            new CreateMaintenanceCatalogueItemCommand { Category = "Cabling", Name = "Cat6 point", Price = 45m }, default);

        Assert.True(added.Succeeded, string.Join(", ", added.Errors));
        Assert.Equal("Ali <ali@jamago.qa>", added.Data!.AddedByName);
        var item = await db.MaintenanceCatalogueItems.SingleAsync();
        Assert.Equal(Technician.UserId, item.AddedById);
        var log = await db.MaintenanceCatalogueChanges.SingleAsync();
        Assert.Equal(CatalogueChangeAction.Added, log.Action);
        Assert.Equal(45m, log.NewPrice);
        Assert.Equal("Ali <ali@jamago.qa>", log.ByName);
    }

    [Fact]
    public async Task A_price_change_keeps_the_old_and_new_price_with_who_and_when()
    {
        var db = NewContext();
        var clock = new Clock(new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero));
        var added = await new CreateMaintenanceCatalogueItemCommandHandler(db, Office, clock).Handle(
            new CreateMaintenanceCatalogueItemCommand { Category = "Cabling", Name = "Cat6 point", Price = 45m }, default);

        clock.Now = clock.Now.AddHours(3);
        var changed = await new UpdateMaintenanceCatalogueItemCommandHandler(db, Technician, clock).Handle(
            new UpdateMaintenanceCatalogueItemCommand { Id = added.Data!.Id, Category = "Cabling", Name = "Cat6 point", Price = 50m }, default);

        Assert.True(changed.Succeeded, string.Join(", ", changed.Errors));
        Assert.Equal(50m, changed.Data!.DefaultFee);
        Assert.Equal("Ali <ali@jamago.qa>", changed.Data.PriceChangedByName);
        Assert.Equal(clock.Now.UtcDateTime, changed.Data.PriceChangedAt);
        Assert.Equal("Sara <sara@jamago.qa>", changed.Data.AddedByName);   // who added it does not change

        var log = await db.MaintenanceCatalogueChanges.OrderBy(c => c.CreatedAt).ToListAsync();
        Assert.Equal([CatalogueChangeAction.Added, CatalogueChangeAction.PriceChanged], log.Select(c => c.Action));
        Assert.Equal(45m, log[1].OldPrice);
        Assert.Equal(50m, log[1].NewPrice);
        Assert.Equal(Technician.UserId, log[1].ById);
    }

    [Fact]
    public async Task Renaming_is_an_edit_not_a_price_change_and_saving_nothing_new_logs_nothing()
    {
        var db = NewContext();
        var clock = new Clock(new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero));
        var added = await new CreateMaintenanceCatalogueItemCommandHandler(db, Office, clock).Handle(
            new CreateMaintenanceCatalogueItemCommand { Category = "Cabling", Name = "Cat6 point", Price = 45m }, default);
        var update = new UpdateMaintenanceCatalogueItemCommandHandler(db, Technician, clock);

        await update.Handle(new UpdateMaintenanceCatalogueItemCommand { Id = added.Data!.Id, Category = "Cabling", Name = "Cat6 data point", Price = 45m }, default);
        await update.Handle(new UpdateMaintenanceCatalogueItemCommand { Id = added.Data.Id, Category = "Cabling", Name = "Cat6 data point", Price = 45m }, default);

        var log = await db.MaintenanceCatalogueChanges.OrderBy(c => c.CreatedAt).ThenBy(c => c.Action).ToListAsync();
        Assert.Equal([CatalogueChangeAction.Added, CatalogueChangeAction.Edited], log.Select(c => c.Action));
        var item = await db.MaintenanceCatalogueItems.SingleAsync();
        Assert.Null(item.PriceChangedByName);
    }
}
