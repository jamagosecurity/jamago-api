using Jama.Application.Boqs;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// A lump sum agreed off a finished quotation.
///
/// These pin the arithmetic the document prints: the lines come to Total, the
/// discount comes off it, and what is left is what the customer pays. The
/// discount is never folded into the line rates — a quotation has to be able to
/// show what was given away.
/// </summary>
public class BoqDiscountTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"boq-discount-{Guid.NewGuid()}")
            .Options);

    private static async Task<(ApplicationDbContext Context, Guid CameraId)> SeedAsync(decimal rate)
    {
        var context = NewContext();
        var camera = new Camera
        {
            Id = Guid.CreateVersion7(),
            ItemName = "DS-2CD2143G2-I",
            Brand = "Hikvision",
            Category = ProductCategory.Cctv,
            Uom = UnitOfMeasurement.Piece,
            Rate = rate,
        };
        context.Cameras.Add(camera);
        await context.SaveChangesAsync();
        return (context, camera.Id);
    }

    private sealed record Request(IReadOnlyList<BoqSectionInput> Sections, decimal Discount) : IBoqWrite
    {
        public string? ProjectName => "Test";
        public string? SiteLocation => null;
        public string? ClientName => null;
        public string? ContactNumber => null;
        public DateOnly? IssueDate => null;
        public BoqStatus Status => BoqStatus.Draft;
        public string? Notes => null;
        public decimal SpecialDiscount => Discount;
    }

    private static Request OneLine(Guid cameraId, decimal quantity, decimal discount) =>
        new(
            [
                new BoqSectionInput
                {
                    Title = BoqSectionTitles.MainCctv,
                    Lines = [new BoqLineInput { CameraId = cameraId, Quantity = quantity }],
                },
            ],
            discount);

    private static async Task<(string? Error, Boq Boq)> BuildAsync(
        ApplicationDbContext context, Request request)
    {
        var boq = new Boq { Id = Guid.CreateVersion7() };
        var (error, _) = await BoqWriter.BuildAsync(
            boq, request, context, TimeProvider.System, CancellationToken.None);
        return (error, boq);
    }

    [Fact]
    public async Task A_quotation_with_no_discount_is_payable_in_full()
    {
        var (context, cameraId) = await SeedAsync(1650m);

        var (error, boq) = await BuildAsync(context, OneLine(cameraId, 3, discount: 0m));

        Assert.Null(error);
        Assert.Equal(4950m, boq.Total);
        Assert.Equal(0m, boq.SpecialDiscount);
        // Not left at zero: the amount payable is the figure the document ends
        // on, and it has to stand on its own without the reader adding anything.
        Assert.Equal(4950m, boq.GrandTotal);
    }

    [Fact]
    public async Task A_discount_comes_off_the_total()
    {
        var (context, cameraId) = await SeedAsync(1650m);

        var (error, boq) = await BuildAsync(context, OneLine(cameraId, 3, discount: 450m));

        Assert.Null(error);
        // The lines are untouched by it — what was quoted per item still reads
        // as what was quoted per item.
        Assert.Equal(4950m, boq.Total);
        Assert.Equal(450m, boq.SpecialDiscount);
        Assert.Equal(4500m, boq.GrandTotal);
    }

    [Fact]
    public async Task A_discount_larger_than_the_quotation_is_refused()
    {
        var (context, cameraId) = await SeedAsync(1650m);

        // 5,000 off a 1,650 quotation is a typo, and a quotation silently worth
        // nothing is worse than a save that comes back and says so.
        var (error, _) = await BuildAsync(context, OneLine(cameraId, 1, discount: 5000m));

        Assert.NotNull(error);
        Assert.Contains("more than the quotation total", error);
    }

    [Fact]
    public async Task A_discount_equal_to_the_quotation_is_allowed()
    {
        var (context, cameraId) = await SeedAsync(1650m);

        // A job thrown in at no charge is a real thing to quote; only more than
        // the total is nonsense.
        var (error, boq) = await BuildAsync(context, OneLine(cameraId, 1, discount: 1650m));

        Assert.Null(error);
        Assert.Equal(0m, boq.GrandTotal);
    }

    [Fact]
    public async Task A_discount_is_rounded_to_fils()
    {
        var (context, cameraId) = await SeedAsync(1650m);

        // A UI showing 100.00 can post 100.000000001 back; stored as typed, the
        // document would print one figure and hold another.
        var (error, boq) = await BuildAsync(context, OneLine(cameraId, 1, discount: 100.005m));

        Assert.Null(error);
        Assert.Equal(100.01m, boq.SpecialDiscount);
        Assert.Equal(1549.99m, boq.GrandTotal);
    }

    [Fact]
    public void A_discount_is_clamped_when_the_lines_shrink_under_it()
    {
        // The writer refuses an oversized discount on the way in, but a quotation
        // whose lines are cut down afterwards must still not print a negative
        // amount payable.
        var boq = new Boq { Total = 500m, SpecialDiscount = 900m };

        BoqMath.ApplyDiscount(boq);

        Assert.Equal(500m, boq.SpecialDiscount);
        Assert.Equal(0m, boq.GrandTotal);
    }
}
