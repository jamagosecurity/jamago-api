using Jama.Application.Boqs;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// Deleting a stock item sets its lines' CameraId to null and leaves the copied
/// name, unit and rate standing — a bill someone approved must not change under
/// them. These pin the other half of that promise: such a line survives the next
/// SAVE too. It used to make the whole document unsaveable, because the editor
/// posted the empty id back and the request failed to parse at all.
///
/// The line is rebuilt from the row already stored, never from the request, so
/// "keep this line" is the only thing a client can say about it.
/// </summary>
public class BoqRetiredItemTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"boq-retired-{Guid.NewGuid()}")
            .Options);

    /// <summary>A BOQ holding one line whose stock item has since been deleted.</summary>
    private static async Task<(ApplicationDbContext Context, Boq Boq, BoqLine Line)> SeedAsync()
    {
        var context = NewContext();

        var boq = new Boq { Id = Guid.CreateVersion7(), ProjectName = "Villa 22" };
        var section = new BoqSection
        {
            Id = Guid.CreateVersion7(),
            BoqId = boq.Id,
            Title = BoqSectionTitles.Service,
            SortOrder = 0,
        };
        var line = new BoqLine
        {
            Id = Guid.CreateVersion7(),
            BoqSectionId = section.Id,
            // Null: this is exactly what the delete rule leaves behind.
            CameraId = null,
            ItemName = "Installation & Commissioning",
            ModelNo = "SVC-INST",
            Brand = "Jama Go",
            Type = "Labour",
            Uom = UnitOfMeasurement.Location,
            Quantity = 2,
            Resolution = CameraResolution.Unspecified,
            CatalogueRate = 1500m,
            UnitRate = 1500m,
            SortOrder = 0,
        };

        context.Boqs.Add(boq);
        context.BoqSections.Add(section);
        context.BoqLines.Add(line);
        await context.SaveChangesAsync();

        return (context, boq, line);
    }

    private sealed record Request(IReadOnlyList<BoqSectionInput> Sections) : IBoqWrite
    {
        public string? ProjectName => "Villa 22";
        public string? SiteLocation => null;
        public string? ClientName => null;
        public string? ContactNumber => null;
        public DateOnly? IssueDate => null;
        public BoqStatus Status => BoqStatus.Draft;
        public string? Notes => null;
        public decimal SpecialDiscount => 0m;
    }

    private static Request OneLine(BoqLineInput line) =>
        new([new BoqSectionInput { Title = BoqSectionTitles.Service, Lines = [line] }]);

    private static Task<(string? Error, List<BoqSection> Sections)> BuildAsync(
        ApplicationDbContext context, Boq boq, Request request) =>
        BoqWriter.BuildAsync(boq, request, context, TimeProvider.System, CancellationToken.None);

    [Fact]
    public async Task A_line_whose_stock_item_is_gone_survives_the_next_save()
    {
        var (context, boq, line) = await SeedAsync();

        // What the editor sends for such a line: no camera to name, so it names
        // the line — and a new quantity.
        var (error, sections) = await BuildAsync(context, boq,
            OneLine(new BoqLineInput { Id = line.Id, CameraId = null, Quantity = 5 }));

        Assert.Null(error);
        var saved = sections.Single().Lines.Single();

        // Everything describing the item comes off the stored row, unchanged.
        Assert.Equal("Installation & Commissioning", saved.ItemName);
        Assert.Equal("SVC-INST", saved.ModelNo);
        Assert.Equal("Jama Go", saved.Brand);
        Assert.Equal("Labour", saved.Type);
        Assert.Equal(UnitOfMeasurement.Location, saved.Uom);
        Assert.Equal(1500m, saved.UnitRate);
        Assert.Equal(1500m, saved.CatalogueRate);
        Assert.Null(saved.CameraId);

        // Only the quantity was the client's to change.
        Assert.Equal(5m, saved.Quantity);
        Assert.Equal(7500m, saved.LineTotal);
    }

    [Fact]
    public async Task A_carried_line_may_still_be_repriced()
    {
        var (context, boq, line) = await SeedAsync();

        var (error, sections) = await BuildAsync(context, boq,
            OneLine(new BoqLineInput { Id = line.Id, CameraId = null, Quantity = 2, UnitRate = 1200m }));

        Assert.Null(error);
        var saved = sections.Single().Lines.Single();
        Assert.Equal(1200m, saved.UnitRate);
        // The list price the line was written with still stands beside it, the
        // same as for a line that still has its stock item.
        Assert.Equal(1500m, saved.CatalogueRate);
    }

    [Fact]
    public async Task A_line_id_from_another_boq_is_refused()
    {
        var (context, _, line) = await SeedAsync();

        // The id names a real line — on a different document. Carrying it across
        // would let a client copy another BOQ's pricing onto this one.
        var other = new Boq { Id = Guid.CreateVersion7(), ProjectName = "Somebody else's job" };
        var (error, sections) = await BuildAsync(context, other,
            OneLine(new BoqLineInput { Id = line.Id, CameraId = null, Quantity = 1 }));

        Assert.Equal("A line is no longer on this BOQ. Reload the page and try again.", error);
        Assert.Empty(sections);
    }

    [Fact]
    public async Task A_line_naming_neither_an_item_nor_itself_is_refused()
    {
        var (context, boq, _) = await SeedAsync();

        var (error, sections) = await BuildAsync(context, boq,
            OneLine(new BoqLineInput { Id = null, CameraId = null, Quantity = 1 }));

        Assert.Equal("Every line must point at a stock item.", error);
        Assert.Empty(sections);
    }
}
