using Jama.Application.Boqs.Queries.GetContractPdf;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// A contract represents an agreement, so — unlike the plain quotation PDF,
/// which prints a watermarked draft at any status — it does not exist at all
/// until the quotation is Approved. Pins that refusal, and that an approved
/// one succeeds and carries the quotation's own figures.
/// </summary>
public class GetContractPdfTests
{
    private sealed class FakeCurrentUser(Guid userId, bool canApprove = false) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
        public string DisplayName => "Test User";
        public string Role => Roles.Staff;
        public bool Has(string permission) => canApprove && permission == Permissions.BoqApprove;
        public bool IsSuperAdmin => false;
    }

    private sealed class FakeContractPdfGenerator : IContractPdfGenerator
    {
        public ContractPdfModel? Last { get; private set; }

        public byte[] Generate(ContractPdfModel model)
        {
            Last = model;
            return [1, 2, 3];
        }
    }

    private sealed class FakeContractTextTemplate : IContractTextTemplate
    {
        public string BuildDefault(ContractTextModel model) => "default contract text";
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"contract-pdf-{Guid.NewGuid()}")
            .Options);

    private static async Task<(ApplicationDbContext Context, Boq Boq)> SeedAsync(BoqStatus status)
    {
        var context = NewContext();
        var now = new DateTime(2026, 9, 19, 9, 0, 0, DateTimeKind.Utc);

        var boq = new Boq
        {
            Id = Guid.CreateVersion7(),
            BoqNumber = "JMG-00037",
            ProjectName = "Villa 22",
            ClientName = "Ahmed Al-Kuwari",
            ContactNumber = "+974 5555 1234",
            SiteLocation = "Doha, Qatar",
            Status = status,
            Total = 2_246_097m,
            GrandTotal = 2_246_097m,
            CreatedAt = now,
            ApprovedAt = status == BoqStatus.Approved ? now : null,
        };

        context.Boqs.Add(boq);
        await context.SaveChangesAsync();

        return (context, boq);
    }

    [Theory]
    [InlineData(BoqStatus.Draft)]
    [InlineData(BoqStatus.Submitted)]
    [InlineData(BoqStatus.Rejected)]
    public async Task A_contract_cannot_be_generated_before_the_quotation_is_approved(BoqStatus status)
    {
        var (context, boq) = await SeedAsync(status);
        var actor = new FakeCurrentUser(boq.PreparedById);

        var result = await new GetContractPdfQueryHandler(
            context, new FakeContractPdfGenerator(), new FakeContractTextTemplate(), actor).Handle(
            new GetContractPdfQuery(boq.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "A contract can only be generated once this quotation is approved.", result.Errors);
    }

    [Fact]
    public async Task An_approved_quotation_produces_a_contract()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Approved);
        var actor = new FakeCurrentUser(boq.PreparedById);

        var result = await new GetContractPdfQueryHandler(
            context, new FakeContractPdfGenerator(), new FakeContractTextTemplate(), actor).Handle(
            new GetContractPdfQuery(boq.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("JMG-00037 Rev0 Contract.pdf", result.Data!.FileName);
        Assert.NotEmpty(result.Data.Content);
    }

    [Fact]
    public async Task Someone_who_cannot_see_the_quotation_cannot_generate_its_contract()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Approved);
        boq.Status = BoqStatus.Submitted; // not yet visible to a stranger, and not approved either
        await context.SaveChangesAsync();

        var stranger = new FakeCurrentUser(Guid.CreateVersion7());

        var result = await new GetContractPdfQueryHandler(
            context, new FakeContractPdfGenerator(), new FakeContractTextTemplate(), stranger).Handle(
            new GetContractPdfQuery(boq.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("BOQ not found.", result.Errors);
    }

    [Fact]
    public async Task The_contract_carries_the_project_name_and_the_quotations_prices()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Approved);
        boq.SpecialDiscount = 46_097m;
        boq.Sections.Add(new BoqSection
        {
            Id = Guid.CreateVersion7(), BoqId = boq.Id, Title = "Main CCTV System", SortOrder = 1,
            Lines =
            [
                new BoqLine { Id = Guid.CreateVersion7(), ItemName = "NVR 32-Channel", Brand = "Hikvision", ModelNo = "DS-7732", Quantity = 2, UnitRate = 3_900m, LineTotal = 7_800m, SortOrder = 1 },
                new BoqLine { Id = Guid.CreateVersion7(), ItemName = "Cable roll", Quantity = 10, UnitRate = 150m, LineTotal = 1_500m, SortOrder = 2 },
            ],
        });
        await context.SaveChangesAsync();
        var generator = new FakeContractPdfGenerator();

        var result = await new GetContractPdfQueryHandler(
            context, generator, new FakeContractTextTemplate(), new FakeCurrentUser(boq.PreparedById)).Handle(
            new GetContractPdfQuery(boq.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        var model = generator.Last!;
        Assert.Equal("Villa 22", model.ProjectName);
        var prices = Assert.IsType<ContractPriceSchedule>(model.Prices);
        var section = Assert.Single(prices.Sections);
        Assert.Equal("Main CCTV System", section.Title);
        Assert.Equal(2, section.Lines.Count);
        Assert.Equal("1.1", section.Lines[0].Number);
        Assert.Equal(9_300m, section.Subtotal);
        Assert.Equal(46_097m, prices.SpecialDiscount);
        Assert.Equal(2_246_097m, prices.GrandTotal);
    }

    [Fact]
    public async Task A_quotation_with_no_lines_gets_a_contract_without_a_price_schedule()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Approved);
        var generator = new FakeContractPdfGenerator();

        await new GetContractPdfQueryHandler(
            context, generator, new FakeContractTextTemplate(), new FakeCurrentUser(boq.PreparedById)).Handle(
            new GetContractPdfQuery(boq.Id), CancellationToken.None);

        Assert.Null(generator.Last!.Prices);
    }
}
