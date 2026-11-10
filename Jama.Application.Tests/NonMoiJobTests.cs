using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.NonMoiJobs.Commands;
using Jama.Application.NonMoiJobs.Queries;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>Non-MOI jobs: admin issues one to a single technician, who alone sees it.</summary>
public class NonMoiJobTests
{
    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
        public string DisplayName => "Tech";
        public string Role => Roles.Technician;
        public bool Has(string permission) => false;
        public bool IsSuperAdmin => false;
    }

    private static readonly Guid TechA = Guid.NewGuid();
    private static readonly Guid TechB = Guid.NewGuid();
    private static readonly Guid Clerk = Guid.NewGuid();
    private static readonly Guid Off = Guid.NewGuid();

    private static AdminUser Account(Guid id, bool enabled = true) =>
        new() { Id = id, Email = $"{id}@example.test", FullName = "x", IsActive = enabled };

    private static ApplicationDbContext NewContext()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"nonmoi-{Guid.NewGuid()}").Options);
        context.Staff.AddRange(
            // IsActive false on purpose: that only hides someone from the public
            // Our Team page, and must not stop a technician being given work.
            new Staff { AdminUserId = TechA, FullName = "Tech A", Department = "Technician", IsActive = false, Account = Account(TechA) },
            new Staff { AdminUserId = TechB, FullName = "Tech B", Department = "Technician", Account = Account(TechB) },
            new Staff { AdminUserId = Clerk, FullName = "Panels Clerk", Department = "Panels", Account = Account(Clerk) },
            new Staff { AdminUserId = Off, FullName = "Tech Off", Department = "Technician", Account = Account(Off, enabled: false) });
        context.SaveChanges();
        return context;
    }

    private static CreateNonMoiJobCommand Job(Guid assignee, double? lat = 25.286106, double? lng = 51.534817) => new()
    {
        CustomerName = " Al Noor Trading ", CustomerNumber = "+974 5550 1234", Location = "Industrial Area, Doha",
        Latitude = lat, Longitude = lng,
        DescriptionEn = "Replace two faulty cameras", DescriptionAr = "استبدال كاميرتين معطلتين",
        AssignedToId = assignee,
    };

    [Fact]
    public async Task A_job_is_issued_with_a_number_and_trimmed_details()
    {
        var context = NewContext();

        var first = await new CreateNonMoiJobCommandHandler(context, new FakeCurrentUser(Clerk)).Handle(Job(TechA), CancellationToken.None);
        var second = await new CreateNonMoiJobCommandHandler(context, new FakeCurrentUser(Clerk)).Handle(Job(TechA), CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.Equal("NMJ-00001", first.Data!.JobNumber);
        Assert.Equal("NMJ-00002", second.Data!.JobNumber);
        Assert.Equal("Al Noor Trading", first.Data.CustomerName);
        Assert.Equal("Tech A", first.Data.AssignedToName);
    }

    [Fact]
    public async Task A_job_cannot_go_to_staff_outside_the_technician_department()
    {
        var context = NewContext();

        var result = await new CreateNonMoiJobCommandHandler(context, new FakeCurrentUser(Clerk)).Handle(Job(Clerk), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(context.NonMoiJobs);
    }

    [Fact]
    public async Task A_technician_whose_login_is_switched_off_cannot_be_given_a_job()
    {
        var context = NewContext();

        var result = await new CreateNonMoiJobCommandHandler(context, new FakeCurrentUser(Clerk)).Handle(Job(Off), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task The_picker_lists_only_technicians_who_can_sign_in_even_if_hidden_from_Our_Team()
    {
        var context = NewContext();

        var result = await new GetJobTechniciansQueryHandler(context).Handle(new GetJobTechniciansQuery(), CancellationToken.None);

        Assert.Equal(["Tech A", "Tech B"], result.Data!.Select(t => t.FullName));
    }

    [Fact]
    public async Task A_technician_sees_only_the_jobs_assigned_to_them()
    {
        var context = NewContext();
        var create = new CreateNonMoiJobCommandHandler(context, new FakeCurrentUser(Clerk));
        await create.Handle(Job(TechA), CancellationToken.None);
        await create.Handle(Job(TechB), CancellationToken.None);

        var mine = await new GetMyNonMoiJobsQueryHandler(context, new FakeCurrentUser(TechA))
            .Handle(new GetMyNonMoiJobsQuery(), CancellationToken.None);
        var all = await new GetNonMoiJobsQueryHandler(context).Handle(new GetNonMoiJobsQuery(), CancellationToken.None);

        Assert.Equal("Tech A", Assert.Single(mine.Data!).AssignedToName);
        Assert.Equal(2, all.Data!.Count);
    }

    [Fact]
    public async Task Reassigning_a_job_moves_it_to_the_other_technicians_list()
    {
        var context = NewContext();
        var created = await new CreateNonMoiJobCommandHandler(context, new FakeCurrentUser(Clerk)).Handle(Job(TechA), CancellationToken.None);

        var updated = await new UpdateNonMoiJobCommandHandler(context, TimeProvider.System).Handle(
            new UpdateNonMoiJobCommand
            {
                Id = created.Data!.Id, CustomerName = "Al Noor Trading", CustomerNumber = "+974 5550 1234",
                Location = "Industrial Area, Doha", DescriptionEn = "Replace two faulty cameras", AssignedToId = TechB,
            },
            CancellationToken.None);

        Assert.True(updated.Succeeded);
        Assert.Null(updated.Data!.Latitude);
        var mineA = await new GetMyNonMoiJobsQueryHandler(context, new FakeCurrentUser(TechA)).Handle(new GetMyNonMoiJobsQuery(), CancellationToken.None);
        var mineB = await new GetMyNonMoiJobsQueryHandler(context, new FakeCurrentUser(TechB)).Handle(new GetMyNonMoiJobsQuery(), CancellationToken.None);
        Assert.Empty(mineA.Data!);
        Assert.Single(mineB.Data!);
    }

    [Theory]
    [InlineData(25.28, null)]      // half a pin
    [InlineData(95.0, 51.5)]       // latitude out of range
    [InlineData(0.0, 0.0)]         // the Atlantic
    public void A_bad_map_pin_is_refused(double? lat, double? lng)
    {
        var result = new CreateNonMoiJobCommandValidator().Validate(Job(TechA, lat, lng));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void The_customer_and_english_description_are_required_but_arabic_and_pin_are_not()
    {
        var validator = new CreateNonMoiJobCommandValidator();

        Assert.True(validator.Validate(Job(TechA, null, null) with { DescriptionAr = null }).IsValid);
        Assert.False(validator.Validate(Job(TechA) with { CustomerName = " " }).IsValid);
        Assert.False(validator.Validate(Job(TechA) with { DescriptionEn = "" }).IsValid);
    }

    [Theory]
    [InlineData("+974 5550 1234")]
    [InlineData("55501234")]
    [InlineData("(974) 5550-1234")]
    [InlineData("30102839894")]
    public void A_real_phone_number_is_accepted(string number)
    {
        Assert.True(new CreateNonMoiJobCommandValidator().Validate(Job(TechA) with { CustomerNumber = number }).IsValid);
    }

    [Theory]
    [InlineData("asdasd")]          // letters
    [InlineData("C-1001")]          // a reference, not a phone
    [InlineData("12345")]           // too short
    [InlineData("1234567890123456")] // too long
    [InlineData("555 0123 ext 4")]  // letters mixed in
    [InlineData("+++555012345")]
    [InlineData("   ")]
    public void A_customer_number_that_is_not_a_phone_number_is_refused(string number)
    {
        Assert.False(new CreateNonMoiJobCommandValidator().Validate(Job(TechA) with { CustomerNumber = number }).IsValid);
    }

    [Theory]
    [InlineData("A")]               // name: 1 character
    public void A_one_letter_customer_name_is_refused(string name)
    {
        Assert.False(new CreateNonMoiJobCommandValidator().Validate(Job(TechA) with { CustomerName = name }).IsValid);
    }

    [Fact]
    public void A_too_short_location_or_description_is_refused()
    {
        var validator = new CreateNonMoiJobCommandValidator();
        Assert.False(validator.Validate(Job(TechA) with { Location = "ab" }).IsValid);
        Assert.False(validator.Validate(Job(TechA) with { DescriptionEn = "x" }).IsValid);
    }
}
