using Jama.Application.Dia.Commands.DeleteDiaInspection;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// Permanent delete is the one DIA action with nothing to undo it, so these cover
/// the refusals rather than the happy path: each guard is the only thing standing
/// between a stray click and lost inspection evidence.
/// </summary>
public sealed class DiaPermanentDeleteTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"dia-delete-{Guid.NewGuid()}")
            .Options);

    private static DiaInspection NewDia(bool archived) => new()
    {
        Id = Guid.NewGuid(),
        DiaNumber = "DIA-1",
        NormalizedDiaNumber = "DIA-1",
        ClientNumber = "C-1",
        ClientName = "Client",
        ClientLocation = "Doha",
        CreatedById = Guid.NewGuid(),
        IsArchived = archived,
    };

    [Fact]
    public async Task Refuses_a_record_that_has_not_been_archived()
    {
        await using var context = NewContext();
        var dia = NewDia(archived: false);
        context.DiaInspections.Add(dia);
        await context.SaveChangesAsync();

        var result = await new DeleteDiaInspectionHandler(context)
            .Handle(new(dia.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Archive this DIA record", result.Errors.Single());
        Assert.True(await context.DiaInspections.AnyAsync(x => x.Id == dia.Id));
    }

    [Fact]
    public async Task Refuses_an_archived_record_that_has_submitted_inspections()
    {
        await using var context = NewContext();
        var dia = NewDia(archived: true);
        context.DiaInspections.Add(dia);
        context.TechnicianInspections.Add(new TechnicianInspection
        {
            Id = Guid.NewGuid(),
            DiaInspectionId = dia.Id,
            Quarter = 1,
            TechnicianId = Guid.NewGuid(),
            Status = TechnicianInspectionStatus.Submitted,
        });
        await context.SaveChangesAsync();

        var result = await new DeleteDiaInspectionHandler(context)
            .Handle(new(dia.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("submitted inspection", result.Errors.Single());
        Assert.True(await context.DiaInspections.AnyAsync(x => x.Id == dia.Id));
    }

    [Fact]
    public async Task Counts_a_soft_deleted_inspection_as_a_reason_to_refuse()
    {
        // IsDeleted hides a submission from the technician's list; it does not
        // unmake the fact that an inspection happened against this site.
        await using var context = NewContext();
        var dia = NewDia(archived: true);
        context.DiaInspections.Add(dia);
        context.TechnicianInspections.Add(new TechnicianInspection
        {
            Id = Guid.NewGuid(),
            DiaInspectionId = dia.Id,
            Quarter = 1,
            TechnicianId = Guid.NewGuid(),
            Status = TechnicianInspectionStatus.Submitted,
            IsDeleted = true,
        });
        await context.SaveChangesAsync();

        var result = await new DeleteDiaInspectionHandler(context)
            .Handle(new(dia.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(await context.DiaInspections.AnyAsync(x => x.Id == dia.Id));
    }

    [Fact]
    public async Task Refuses_an_archived_record_that_has_invoices()
    {
        await using var context = NewContext();
        var dia = NewDia(archived: true);
        context.DiaInspections.Add(dia);
        context.InspectionInvoices.Add(new InspectionInvoice
        {
            Id = Guid.NewGuid(),
            DiaInspectionId = dia.Id,
            TechnicianInspectionId = Guid.NewGuid(),
            Quarter = 1,
            InvoiceNumber = "INV-1",
            GeneratedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var result = await new DeleteDiaInspectionHandler(context)
            .Handle(new(dia.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("invoice", result.Errors.Single());
        Assert.True(await context.DiaInspections.AnyAsync(x => x.Id == dia.Id));
    }

    [Fact]
    public async Task Audit_rows_stay_immutable_for_a_record_that_is_not_being_deleted()
    {
        // The delete path had to carve an exception into the append-only audit
        // rule. This pins the edge of that exception: removing history on its own,
        // without its parent, is still refused.
        await using var context = NewContext();
        var dia = NewDia(archived: true);
        context.DiaInspections.Add(dia);
        var entry = new DiaInspectionHistory
        {
            Id = Guid.NewGuid(),
            DiaInspectionId = dia.Id,
            Action = DiaInspectionAction.Archive,
            ActorId = Guid.NewGuid(),
        };
        context.DiaInspectionHistory.Add(entry);
        await context.SaveChangesAsync();

        context.DiaInspectionHistory.Remove(entry);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Reports_a_missing_record_distinctly_so_the_endpoint_can_answer_404()
    {
        await using var context = NewContext();

        var result = await new DeleteDiaInspectionHandler(context)
            .Handle(new(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(DeleteDiaInspectionCommand.NotFoundError, result.Errors.Single());
    }

    [Fact]
    public async Task Deletes_an_archived_record_and_its_history_when_nothing_is_attached()
    {
        await using var context = NewContext();
        var dia = NewDia(archived: true);
        context.DiaInspections.Add(dia);
        context.DiaInspectionHistory.Add(new DiaInspectionHistory
        {
            Id = Guid.NewGuid(),
            DiaInspectionId = dia.Id,
            Action = DiaInspectionAction.Archive,
            ActorId = Guid.NewGuid(),
        });
        // A second record's history must survive: the delete has to scope its
        // history sweep by id rather than clearing the table.
        var other = NewDia(archived: true);
        context.DiaInspections.Add(other);
        context.DiaInspectionHistory.Add(new DiaInspectionHistory
        {
            Id = Guid.NewGuid(),
            DiaInspectionId = other.Id,
            Action = DiaInspectionAction.Archive,
            ActorId = Guid.NewGuid(),
        });
        await context.SaveChangesAsync();

        var result = await new DeleteDiaInspectionHandler(context)
            .Handle(new(dia.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(dia.Id, result.Data);
        Assert.False(await context.DiaInspections.AnyAsync(x => x.Id == dia.Id));
        Assert.False(await context.DiaInspectionHistory.AnyAsync(x => x.DiaInspectionId == dia.Id));
        Assert.True(await context.DiaInspections.AnyAsync(x => x.Id == other.Id));
        Assert.True(await context.DiaInspectionHistory.AnyAsync(x => x.DiaInspectionId == other.Id));
    }
}
