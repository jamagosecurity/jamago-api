using Jama.Application.Boqs;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// The rate on a quotation line is a commercial decision, so who may set it is
/// worth pinning down. These cover the boundary: the catalogue price is the
/// default, an override needs the grant, and a caller without it is refused
/// rather than silently repriced.
/// </summary>
public class BoqRateOverrideTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"boq-rate-{Guid.NewGuid()}")
            .Options);

    private static async Task<(ApplicationDbContext Context, Guid CameraId)> SeedAsync(decimal rate)
    {
        var context = NewContext();
        var camera = new Camera
        {
            Id = Guid.CreateVersion7(),
            ItemName = "DH-IPC-HDBW2241E-S",
            Brand = "Dahua",
            Type = "Dome",
            Category = ProductCategory.Cctv,
            Uom = UnitOfMeasurement.Piece,
            Rate = rate,
        };
        context.Cameras.Add(camera);
        await context.SaveChangesAsync();
        return (context, camera.Id);
    }

    private sealed record Request(IReadOnlyList<BoqSectionInput> Sections) : IBoqWrite
    {
        public string? ProjectName => "Test";
        public string? SiteLocation => null;
        public string? ClientName => null;
        public string? ContactNumber => null;
        public DateOnly? IssueDate => null;
        public BoqStatus Status => BoqStatus.Draft;
        public string? Notes => null;
    }

    private static Request OneLine(Guid cameraId, decimal quantity, decimal? unitRate) =>
        new([
            new BoqSectionInput
            {
                Title = BoqSectionTitles.MainCctv,
                Lines = [new BoqLineInput { CameraId = cameraId, Quantity = quantity, UnitRate = unitRate }],
            },
        ]);

    private static Task<(string? Error, List<BoqSection> Sections)> BuildAsync(
        ApplicationDbContext context, Request request, bool canPrice) =>
        BoqWriter.BuildAsync(
            new Boq { Id = Guid.CreateVersion7() },
            request,
            context,
            TimeProvider.System,
            canPrice,
            CancellationToken.None);

    [Fact]
    public async Task Line_without_a_rate_takes_the_catalogue_price()
    {
        var (context, cameraId) = await SeedAsync(270.47m);

        var (error, sections) = await BuildAsync(context, OneLine(cameraId, 17, null), canPrice: false);

        Assert.Null(error);
        var line = sections.Single().Lines.Single();
        Assert.Equal(270.47m, line.UnitRate);
        // Both carry it, so an untouched line reads as "no discount" rather than
        // as a discount down from zero.
        Assert.Equal(270.47m, line.CatalogueRate);
    }

    [Fact]
    public async Task Holder_of_the_grant_may_reprice_a_line()
    {
        var (context, cameraId) = await SeedAsync(270.47m);

        var (error, sections) = await BuildAsync(context, OneLine(cameraId, 17, 250m), canPrice: true);

        Assert.Null(error);
        var line = sections.Single().Lines.Single();
        Assert.Equal(250m, line.UnitRate);
        // The list price survives the override — that is what makes the discount
        // reviewable afterwards instead of invisible.
        Assert.Equal(270.47m, line.CatalogueRate);
        Assert.Equal(4250m, line.LineTotal);
    }

    [Fact]
    public async Task Caller_without_the_grant_is_refused_rather_than_silently_repriced()
    {
        var (context, cameraId) = await SeedAsync(270.47m);

        var (error, sections) = await BuildAsync(context, OneLine(cameraId, 17, 1m), canPrice: false);

        // Refused, not ignored: dropping the rate would print 270.47 to a
        // preparer who believed they had agreed 1.00.
        Assert.NotNull(error);
        Assert.Empty(sections);
    }

    [Fact]
    public async Task Resending_the_catalogue_rate_is_not_an_override()
    {
        var (context, cameraId) = await SeedAsync(270.47m);

        // The editor posts the rate it was showing. Unchanged, that must not
        // count as repricing, or every ordinary save from an account without the
        // grant would fail.
        var (error, sections) = await BuildAsync(context, OneLine(cameraId, 17, 270.47m), canPrice: false);

        Assert.Null(error);
        Assert.Equal(270.47m, sections.Single().Lines.Single().UnitRate);
    }

    [Fact]
    public async Task Zero_is_a_price_a_grant_holder_may_set()
    {
        var (context, cameraId) = await SeedAsync(270.47m);

        // An item included at no charge is a normal thing to quote, and must not
        // be confused with "no rate supplied".
        var (error, sections) = await BuildAsync(context, OneLine(cameraId, 2, 0m), canPrice: true);

        Assert.Null(error);
        var line = sections.Single().Lines.Single();
        Assert.Equal(0m, line.UnitRate);
        Assert.Equal(0m, line.LineTotal);
        Assert.Equal(270.47m, line.CatalogueRate);
    }
}
