using Jama.Application.CashHandovers;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.MoiJobs;
using Jama.Application.NonMoiJobs;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>The accountant's panel: cash handed over at the end of the day, per person, MOI and Non-MOI kept apart.</summary>
public class CashHandoverTests
{
    private static readonly Guid Hasan = Guid.NewGuid();
    private static readonly Guid Tech = Guid.NewGuid();
    private static readonly Guid Accountant = Guid.NewGuid();

    private sealed class FakeUser(Guid id, string name = "Accountant", bool superAdmin = false) : ICurrentUser
    {
        public Guid UserId { get; } = id;
        public string DisplayName { get; } = name;
        public string Role => Roles.Staff;
        public bool Has(string permission) => true;
        public bool IsSuperAdmin => superAdmin;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    // 22 Nov 2026, 18:00 in Doha
    private static readonly TimeProvider Evening = new Clock(new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Today = new(2026, 11, 22);

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"handover-{Guid.NewGuid()}").Options);

    private static NonMoiJob NonMoiJob(string number, string customer = "Al Noor") => new()
    {
        Id = Guid.NewGuid(), JobNumber = number, CustomerName = customer, CustomerNumber = "5550 1234", Location = "Doha", DescriptionEn = "x",
        AssignedToId = Tech, AssignedToName = "Tech", AssignedById = Hasan, AssignedByName = "Hasan", Status = "Closed", WorkTotal = 5000m, AmountPayable = 5000m,
    };

    private static MoiJob MoiJob(string number, string customer = "Al Noor") => new()
    {
        Id = Guid.NewGuid(), JobNumber = number, CustomerName = customer, CustomerNumber = "5550 1234", Location = "Doha", DescriptionEn = "x",
        AssignedToId = Tech, AssignedToName = "Tech", AssignedById = Hasan, AssignedByName = "Hasan", Status = "Closed", WorkTotal = 5000m, AmountPayable = 5000m,
    };

    private static readonly Guid Boss = Guid.NewGuid();

    private static NonMoiJobPayment Pay(NonMoiJob job, decimal amount, Guid? by, string? name, string method = "Cash", DateOnly? day = null,
        Guid? recordedById = null, string recordedBy = "Hasan") => new()
    {
        Id = Guid.NewGuid(), NonMoiJobId = job.Id, Method = method, Amount = amount, ReceivedOn = day ?? Today,
        CollectedById = method == "Cash" ? by : null, CollectedByName = method == "Cash" ? name : null,
        RecordedById = recordedById ?? Hasan, RecordedByName = recordedBy, CreatedAt = Evening.GetUtcNow().UtcDateTime,
    };

    private static MoiJobPayment PayMoi(MoiJob job, decimal amount, Guid? by, string? name, string method = "Cash") => new()
    {
        Id = Guid.NewGuid(), MoiJobId = job.Id, Method = method, Amount = amount, ReceivedOn = Today,
        CollectedById = method == "Cash" ? by : null, CollectedByName = method == "Cash" ? name : null,
        RecordedById = Hasan, RecordedByName = "Hasan", CreatedAt = Evening.GetUtcNow().UtcDateTime,
    };

    /// <summary>Non-MOI: Tech collected 300 + 200 (one yesterday), Hasan 150, 50 "received at the office" that Boss recorded, and an online
    /// transfer. MOI: Tech 400.</summary>
    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var context = NewContext();
        var a = NonMoiJob("NMJ-00001"); var b = NonMoiJob("NMJ-00002", "Mais"); var c = NonMoiJob("NMJ-00003", "Imane");
        var m = MoiJob("MOJ-00001", "Ministry");
        context.NonMoiJobs.AddRange(a, b, c);
        context.MoiJobs.Add(m);
        context.NonMoiJobPayments.AddRange(
            Pay(a, 300m, Tech, "Tech"), Pay(b, 200m, Tech, "Tech", day: Today.AddDays(-1)),
            Pay(a, 150m, Hasan, "Hasan"), Pay(c, 50m, null, null, recordedById: Boss, recordedBy: "Boss <boss@jamago.qa>"),
            Pay(c, 1000m, null, null, method: "Online"));
        context.MoiJobPayments.Add(PayMoi(m, 400m, Tech, "Tech"));
        await context.SaveChangesAsync();
        return context;
    }

    private static ConfirmCashHandoverCommandHandler Confirmer(ApplicationDbContext context) => new(context, new FakeUser(Accountant), Evening);

    private static async Task<PendingHandoversDto> PendingAsync(ApplicationDbContext context, string kind) =>
        (await new GetPendingHandoversQueryHandler(context, Evening).Handle(new GetPendingHandoversQuery(kind), default)).Data!;

    // ===== What is waiting =====

    [Fact]
    public async Task Each_person_has_the_cash_recorded_for_them_and_office_cash_is_the_recording_admins()
    {
        var context = await SeedAsync();

        var pending = await PendingAsync(context, "nonmoi");

        Assert.Equal("NonMOI", pending.Kind);
        Assert.Equal(Today, pending.Today);
        Assert.Equal(["Boss", "Hasan", "Tech"], pending.Groups.Select(g => g.CollectorName));      // the email is not shown
        Assert.Equal([50m, 150m, 500m], pending.Groups.Select(g => g.Total));
        Assert.Equal(Boss, pending.Groups[0].CollectorId);                                          // office cash belongs to who recorded it
        Assert.Equal(700m, pending.Total);                       // the online 1,000 is not cash
        var tech = pending.Groups.Single(g => g.CollectorId == Tech);
        Assert.Equal(Today.AddDays(-1), tech.OldestDay);          // yesterday's cash that was never handed over is still waiting
        Assert.Equal(Today, tech.NewestDay);
        Assert.Equal(["NMJ-00002", "NMJ-00001"], tech.Payments.Select(p => p.JobNumber));
    }

    [Fact]
    public async Task Moi_and_non_moi_are_separate_lists()
    {
        var context = await SeedAsync();

        var moi = await PendingAsync(context, "MOI");

        var only = Assert.Single(moi.Groups);
        Assert.Equal("Tech", only.CollectorName);
        Assert.Equal(400m, only.Total);
        Assert.Equal("MOJ-00001", Assert.Single(only.Payments).JobNumber);
    }

    [Theory]
    [InlineData("", false)] [InlineData("cash", false)] [InlineData("moi", true)] [InlineData("MOI", true)]
    [InlineData("nonmoi", true)] [InlineData("non-moi", true)] [InlineData("NonMOI", true)]
    public async Task Only_moi_or_non_moi_is_accepted_as_the_kind(string kind, bool ok)
    {
        var context = await SeedAsync();
        Assert.Equal(ok, (await new GetPendingHandoversQueryHandler(context, Evening).Handle(new GetPendingHandoversQuery(kind), default)).Succeeded);
    }

    // ===== Receiving it =====

    [Fact]
    public async Task Confirming_records_the_handover_and_the_cash_is_no_longer_waiting()
    {
        var context = await SeedAsync();

        var result = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 500m, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal(500m, result.Data!.Expected);
        Assert.Equal(500m, result.Data.Received);
        Assert.Equal(0m, result.Data.Difference);
        Assert.Equal("Tech", result.Data.CollectorName);
        Assert.Equal("Accountant", result.Data.ReceivedByName);
        Assert.Equal(2, result.Data.Payments.Count);

        var pending = await PendingAsync(context, "nonmoi");
        Assert.DoesNotContain(pending.Groups, g => g.CollectorId == Tech);
        Assert.Equal(200m, pending.Total);                        // Hasan's 150 and Boss's 50 are still waiting
        Assert.Equal(2, await context.NonMoiJobPayments.CountAsync(p => p.CashHandoverId == result.Data.Id));
        Assert.Equal(0, await context.MoiJobPayments.CountAsync(p => p.CashHandoverId != null));   // MOI is a separate handover
    }

    [Fact]
    public async Task The_same_cash_cannot_be_handed_over_twice()
    {
        var context = await SeedAsync();
        await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 500m, null), default);

        var again = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 500m, null), default);

        Assert.False(again.Succeeded);
        Assert.Contains("Nothing is waiting", again.Errors.Single());
        Assert.Equal(1, await context.CashHandovers.CountAsync());
    }

    [Fact]
    public async Task A_different_amount_needs_a_note_and_the_difference_is_kept()
    {
        var context = await SeedAsync();

        var noNote = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 450m, null), default);
        Assert.False(noNote.Succeeded);
        Assert.Contains("QAR 50.00 is missing", noNote.Errors.Single());
        Assert.Equal(0, await context.CashHandovers.CountAsync());                // refused: nothing recorded

        var missing = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 450m, "  Fuel paid from the cash  "), default);
        Assert.True(missing.Succeeded);
        Assert.Equal(-50m, missing.Data!.Difference);
        Assert.Equal("Fuel paid from the cash", missing.Data.Note);

        var extra = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Hasan, 150m, 175m, null), default);
        Assert.False(extra.Succeeded);
        Assert.Contains("QAR 25.00 is extra", extra.Errors.Single());
        var extraWithNote = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Hasan, 150m, 175m, "Customer tipped"), default);
        Assert.Equal(25m, extraWithNote.Data!.Difference);
    }

    [Fact]
    public async Task Cash_added_since_the_page_was_opened_is_refused_so_nobody_confirms_a_figure_they_did_not_see()
    {
        var context = await SeedAsync();
        var job = await context.NonMoiJobs.FirstAsync(j => j.JobNumber == "NMJ-00003");
        context.NonMoiJobPayments.Add(Pay(job, 75m, Tech, "Tech"));               // recorded while the accountant was counting
        await context.SaveChangesAsync();

        var result = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 500m, null), default);

        Assert.False(result.Succeeded);
        Assert.Contains("Cash was added or changed", result.Errors.Single());
        Assert.Equal(0, await context.CashHandovers.CountAsync());

        Assert.True((await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 575m, 575m, null), default)).Succeeded);
    }

    [Fact]
    public async Task Cash_received_at_the_office_is_handed_over_by_the_admin_who_recorded_it()
    {
        var context = await SeedAsync();

        var result = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Boss, 50m, 50m, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal(Boss, result.Data!.CollectorId);
        Assert.Equal("Boss", result.Data.CollectorName);
        Assert.Equal(650m, (await PendingAsync(context, "nonmoi")).Total);
    }

    [Fact]
    public async Task One_person_who_collected_cash_and_recorded_office_cash_hands_over_both_together()
    {
        var context = await SeedAsync();
        var job = await context.NonMoiJobs.FirstAsync(j => j.JobNumber == "NMJ-00003");
        context.NonMoiJobPayments.Add(Pay(job, 25m, null, null, recordedById: Hasan, recordedBy: "Hasan <hasan@jamago.qa>"));   // office cash Hasan recorded
        await context.SaveChangesAsync();

        var hasan = (await PendingAsync(context, "nonmoi")).Groups.Single(g => g.CollectorId == Hasan);

        Assert.Equal(175m, hasan.Total);                 // his own 150 plus the 25 office cash
        Assert.Equal(2, hasan.Payments.Count);
        Assert.Equal("Hasan", hasan.CollectorName);      // the name he was picked under, without an email
        Assert.True((await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Hasan, 175m, 175m, null), default)).Succeeded);
        Assert.Equal(2, await context.NonMoiJobPayments.CountAsync(p => p.CashHandoverId != null));   // exactly his two; Tech's cash is untouched
    }

    [Fact]
    public async Task Moi_cash_is_confirmed_on_its_own_without_touching_non_moi()
    {
        var context = await SeedAsync();

        var result = await Confirmer(context).Handle(new ConfirmCashHandoverCommand("moi", Tech, 400m, 400m, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal("MOI", result.Data!.Kind);
        Assert.Equal(1, await context.MoiJobPayments.CountAsync(p => p.CashHandoverId != null));
        Assert.Equal(0, await context.NonMoiJobPayments.CountAsync(p => p.CashHandoverId != null));
        Assert.Equal(700m, (await PendingAsync(context, "nonmoi")).Total);       // Tech's Non-MOI cash is still waiting
    }

    [Fact]
    public void The_amount_received_is_checked()
    {
        var v = new ConfirmCashHandoverCommandValidator();
        Assert.True(v.Validate(new ConfirmCashHandoverCommand("moi", null, 10m, 0m, null)).IsValid);     // nothing handed over, with a reason
        Assert.False(v.Validate(new ConfirmCashHandoverCommand("moi", null, 10m, -1m, null)).IsValid);
        Assert.False(v.Validate(new ConfirmCashHandoverCommand("moi", null, 10m, 10.001m, null)).IsValid);
        Assert.False(v.Validate(new ConfirmCashHandoverCommand("moi", null, 10m, 10_000_000m, null)).IsValid);
        Assert.False(v.Validate(new ConfirmCashHandoverCommand("bank", null, 10m, 10m, null)).IsValid);
        Assert.False(v.Validate(new ConfirmCashHandoverCommand("moi", null, 10m, 10m, new string('x', 501))).IsValid);
    }

    // ===== Cash that was handed over stays put =====

    [Fact]
    public async Task Cash_already_handed_over_cannot_be_removed_but_cash_still_waiting_can()
    {
        var context = await SeedAsync();
        await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 500m, null), default);
        await Confirmer(context).Handle(new ConfirmCashHandoverCommand("moi", Tech, 400m, 400m, null), default);
        var storage = new NoFiles();
        var boss = new FakeUser(Guid.NewGuid(), "Boss", superAdmin: true);

        var handed = await context.NonMoiJobPayments.FirstAsync(p => p.CashHandoverId != null);
        var refused = await new DeleteNonMoiJobPaymentCommandHandler(context, storage, boss, Evening).Handle(new DeleteNonMoiJobPaymentCommand(handed.Id), default);
        Assert.False(refused.Succeeded);
        Assert.Contains("already handed over", refused.Errors.Single());

        var handedMoi = await context.MoiJobPayments.FirstAsync(p => p.CashHandoverId != null);
        Assert.False((await new DeleteMoiJobPaymentCommandHandler(context, storage, boss, Evening).Handle(new DeleteMoiJobPaymentCommand(handedMoi.Id), default)).Succeeded);

        var waiting = await context.NonMoiJobPayments.FirstAsync(p => p.CashHandoverId == null && p.Method == "Cash");
        Assert.True((await new DeleteNonMoiJobPaymentCommandHandler(context, storage, boss, Evening).Handle(new DeleteNonMoiJobPaymentCommand(waiting.Id), default)).Succeeded);
    }

    private sealed class NoFiles : IFileStorage
    {
        public Task<string> SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken) => Task.FromResult(storageKey);
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    // ===== What was received =====

    [Fact]
    public async Task The_history_lists_the_handovers_of_a_kind_with_the_payments_they_covered()
    {
        var context = await SeedAsync();
        await Confirmer(context).Handle(new ConfirmCashHandoverCommand("nonmoi", Tech, 500m, 450m, "Fuel"), default);
        await Confirmer(context).Handle(new ConfirmCashHandoverCommand("moi", Tech, 400m, 400m, null), default);
        var history = new GetCashHandoverHistoryQueryHandler(context, Evening);

        var nonMoi = (await history.Handle(new GetCashHandoverHistoryQuery("nonmoi", null, null), default)).Data!;

        var one = Assert.Single(nonMoi);
        Assert.Equal(-50m, one.Difference);
        Assert.Equal("Fuel", one.Note);
        Assert.Equal(["NMJ-00002", "NMJ-00001"], one.Payments.Select(p => p.JobNumber));
        Assert.Equal("MOJ-00001", Assert.Single(Assert.Single((await history.Handle(new GetCashHandoverHistoryQuery("moi", null, null), default)).Data!).Payments).JobNumber);

        // A day with no handovers is empty.
        var other = (await history.Handle(new GetCashHandoverHistoryQuery("nonmoi", Today.AddDays(-10), Today.AddDays(-5)), default)).Data!;
        Assert.Empty(other);
        Assert.False((await history.Handle(new GetCashHandoverHistoryQuery("nonmoi", Today.AddDays(-400), Today), default)).Succeeded);
    }

    // ===== Online transfers =====

    [Fact]
    public async Task Online_transfers_are_listed_for_checking_against_the_bank_and_need_no_handover()
    {
        var context = await SeedAsync();
        var online = new GetOnlinePaymentsQueryHandler(context, Evening);

        var nonMoi = (await online.Handle(new GetOnlinePaymentsQuery("nonmoi", null, null), default)).Data!;

        var row = Assert.Single(nonMoi.Rows);
        Assert.Equal(1000m, nonMoi.Total);
        Assert.Equal("NMJ-00003", row.JobNumber);
        Assert.Equal("Imane", row.CustomerName);
        Assert.Empty((await online.Handle(new GetOnlinePaymentsQuery("moi", null, null), default)).Data!.Rows);
        Assert.Equal(Today, nonMoi.From);
    }

    // ===== Who may use it =====

    [Fact]
    public void The_accountant_permission_exists_and_is_described()
    {
        Assert.True(Permissions.IsValid(Permissions.AccountsManage));
        Assert.Equal("accounts.manage", Permissions.AccountsManage);
        Assert.Contains(Permissions.All, p => p.Key == Permissions.AccountsManage && p.Name.StartsWith("Accountant"));
    }
}
