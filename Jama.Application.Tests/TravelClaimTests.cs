using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.TravelClaims;
using Jama.Application.TravelClaims.Commands.ApproveTravelClaims;
using Jama.Application.TravelClaims.Commands.ReimburseTravelClaims;
using Jama.Application.TravelClaims.Commands.RejectTravelClaim;
using Jama.Application.TravelClaims.Commands.SetLunchRate;
using Jama.Application.TravelClaims.Commands.SubmitTravelClaim;
using Jama.Application.TravelClaims.Commands.WithdrawTravelClaim;
using Jama.Application.TravelClaims.Queries.GetMyTravelClaims;
using Jama.Application.TravelClaims.Queries.GetTravelClaimFile;
using Jama.Application.TravelClaims.Queries.GetTravelClaims;
using Jama.Application.TravelClaims.Queries.ReadUberReceipt;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>Uber bills from claim to payout: what a claim pays, who may act on it, and that every step is recorded.</summary>
public class TravelClaimTests
{
    private static readonly Guid Hasan = Guid.NewGuid(), Ali = Guid.NewGuid(), Omar = Guid.NewGuid(), Accountant = Guid.NewGuid(), Gone = Guid.NewGuid();

    private sealed class User(Guid id, string name, string role = Roles.Staff, bool accountant = false) : ICurrentUser
    {
        public Guid UserId { get; } = id;
        public string DisplayName { get; } = $"{name} <{name.ToLowerInvariant()}@jamago.qa>";
        public string Role { get; } = role;
        public bool Has(string permission) => accountant && permission == Permissions.AccountsManage;
        public bool IsSuperAdmin => false;
    }

    private static readonly User Me = new(Hasan, "Hasan");
    private static readonly User Acc = new(Accountant, "Rania", accountant: true);

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Files : IFileStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = [];
        public async Task<string> SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken)
        {
            using var m = new MemoryStream(); await content.CopyToAsync(m, cancellationToken); Saved[storageKey] = m.ToArray(); return storageKey;
        }
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(Saved.TryGetValue(storageKey, out var b) ? new MemoryStream(b) : null);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) { Saved.Remove(storageKey); return Task.CompletedTask; }
    }

    private sealed class PdfText(string? text) : IPdfTextReader { public string? ReadText(byte[] pdf) => text; }

    /// <summary>The test "PDFs" carry their receipt text right after the PDF signature.</summary>
    private sealed class PdfBytesAsText : IPdfTextReader { public string? ReadText(byte[] pdf) => System.Text.Encoding.UTF8.GetString(pdf); }
    private static readonly PdfBytesAsText Reader = new();

    // Sunday 22 Nov 2026, 14:00 in Doha
    private static Clock NewClock() => new(new DateTimeOffset(2026, 11, 22, 11, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Today = new(2026, 11, 22);

    private sealed record World(ApplicationDbContext Db, Files Files, Clock Clock);

    private static async Task<World> NewWorld()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"travel-{Guid.NewGuid()}").Options);
        db.AdminUsers.AddRange(
            new AdminUser { Id = Hasan, FullName = "Hasan", Email = "hasan@jamago.qa", Role = Roles.Staff, IsActive = true },
            new AdminUser { Id = Ali, FullName = "Ali", Email = "ali@jamago.qa", Role = Roles.Technician, IsActive = true },
            new AdminUser { Id = Omar, FullName = "Omar", Email = "omar@jamago.qa", Role = Roles.Technician, IsActive = true },
            new AdminUser { Id = Accountant, FullName = "Rania", Email = "rania@jamago.qa", Role = Roles.Staff, IsActive = true },
            new AdminUser { Id = Gone, FullName = "Gone", Email = "gone@jamago.qa", Role = Roles.Staff, IsActive = false });
        await db.SaveChangesAsync();
        return new(db, new Files(), NewClock());
    }

    private static int _receipts;
    /// <summary>A small "PDF": the signature plus something unique, so each file is different.</summary>
    private static (Stream Stream, long Size) Pdf(string? same = null, string? text = null)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 " + (same ?? Interlocked.Increment(ref _receipts).ToString()) + "\n" + text);
        return (new MemoryStream(bytes), bytes.Length);
    }

    /// <summary>An Uber receipt as Uber writes it: the total, the day, then the route under Trip details.</summary>
    private static string UberText(decimal amount, DateOnly day) => $"""
        {day:MMM d, yyyy}
        Thanks for riding
        Total QAR {amount:0.00}
        Trip details
        8:10 AM
        Staff Accommodation, Barwa City, Doha
        8:34 AM
        Jama Go Office, Al Sadd, Doha
        """;

    private static SubmitTravelClaimCommand Ride(decimal amount = 23.50m, TravelTripType type = TravelTripType.OfficeToAccommodation,
        DateOnly? day = null, string? sameReceipt = null, IReadOnlyList<Guid>? with = null)
    {
        var (s, n) = Pdf(sameReceipt, UberText(amount, day ?? Today));
        return new SubmitTravelClaimCommand
        {
            TripType = type, SiteName = type == TravelTripType.Site ? "Lusail site" : null, Companions = with ?? [],
            ReceiptFileName = "uber.pdf", ReceiptContentType = "application/pdf", ReceiptSize = n, Receipt = s,
        };
    }

    private static SubmitTravelClaimCommand SiteWithLunch(decimal bill, IReadOnlyList<Guid> ate, IReadOnlyList<Guid>? with = null, DateOnly? day = null, decimal ride = 40m)
    {
        var (s, n) = Pdf();
        return Ride(ride, TravelTripType.Site, day, with: with ?? [Ali, Omar]) with
        {
            SiteName = "Lusail site", AteLunch = ate, LunchBillAmount = bill,
            LunchBillFileName = "lunch.pdf", LunchBillContentType = "application/pdf", LunchBillSize = n, LunchBill = s,
        };
    }

    private static Task<Jama.Application.Common.Models.ApiResult<TravelClaimDto>> Submit(World w, SubmitTravelClaimCommand c, ICurrentUser? who = null) =>
        new SubmitTravelClaimCommandHandler(w.Db, w.Files, Reader, who ?? Me, w.Clock).Handle(c, default);

    private static async Task<TravelClaimDto> Submitted(World w, SubmitTravelClaimCommand c, ICurrentUser? who = null)
    {
        var r = await Submit(w, c, who);
        Assert.True(r.Succeeded, r.Errors.FirstOrDefault());
        return r.Data!;
    }

    /// <summary>Approves one claim at the amount claimed, as the given person.</summary>
    private static async Task<Jama.Application.Common.Models.ApiResult<TravelClaimDto>> Approve(World w, ICurrentUser who, Guid id)
    {
        var r = await new ApproveTravelClaimsCommandHandler(w.Db, who, w.Clock).Handle(new ApproveTravelClaimsCommand([new TravelClaimApproval(id, null, null)]), default);
        return r.Succeeded ? Jama.Application.Common.Models.ApiResult<TravelClaimDto>.Success(r.Data!.Single()) : Jama.Application.Common.Models.ApiResult<TravelClaimDto>.Failure(r.Errors);
    }

    private static Task<Jama.Application.Common.Models.ApiResult<IReadOnlyList<TravelClaimDto>>> ApproveMany(World w, params TravelClaimApproval[] items) =>
        new ApproveTravelClaimsCommandHandler(w.Db, Acc, w.Clock).Handle(new ApproveTravelClaimsCommand(items), default);

    private static async Task<TravelClaimDto> Approved(World w, TravelClaimDto c)
    {
        var r = await Approve(w, Acc, c.Id);
        Assert.True(r.Succeeded, r.Errors.FirstOrDefault());
        return r.Data!;
    }

    // ===== Submitting =====

    [Fact]
    public async Task A_ride_is_claimed_with_its_receipt_and_waits_for_the_accountant()
    {
        var w = await NewWorld();

        var claim = await Submitted(w, Ride(23.50m));

        Assert.Equal("UB-00001", claim.ClaimNumber);
        Assert.Equal("Submitted", claim.Status);
        Assert.Equal(23.50m, claim.Total);
        Assert.Equal("Hasan <hasan@jamago.qa>", claim.ClaimantName);
        var line = Assert.Single(claim.Lines);
        Assert.Equal(("Ride", 23.50m, 23.50m), (line.Kind, line.BillAmount, line.Amount));
        Assert.True(line.ReadFromReceipt);
        // Everything about the ride comes from the receipt, never from the form.
        Assert.Equal((Today, new TimeOnly(8, 10)), (claim.TripOn, claim.TripTime));
        Assert.Equal(("Staff Accommodation, Barwa City, Doha", "Jama Go Office, Al Sadd, Doha"), (claim.Pickup, claim.Dropoff));
        Assert.Single(w.Files.Saved);                                         // the receipt is stored
        Assert.Equal("Submitted", Assert.Single(claim.Events).Action);
        Assert.Equal("UB-00002", (await Submitted(w, Ride())).ClaimNumber);
    }

    [Theory]
    [InlineData(30, 30)]        // under the limit: the bill
    [InlineData(36, 36)]        // exactly 12 × 3
    [InlineData(50, 36)]        // over: never more than 12 for each of the 3
    public async Task Site_lunch_pays_the_bill_but_never_more_than_12_for_each_person_who_ate(decimal bill, decimal paid)
    {
        var w = await NewWorld();

        var claim = await Submitted(w, SiteWithLunch(bill, [Hasan, Ali, Omar]));

        var lunch = claim.Lines.Single(l => l.Kind == "Lunch");
        Assert.Equal((bill, paid, 3, 12m), (lunch.BillAmount, lunch.Amount, lunch.Persons, lunch.RatePerPerson!.Value));
        Assert.Equal(40m + paid, claim.Total);
        Assert.True(claim.ClaimantAteLunch);
        Assert.All(claim.People, p => Assert.True(p.AteLunch));
    }

    [Fact]
    public async Task Only_the_people_who_ate_count_for_lunch()
    {
        var w = await NewWorld();
        var claim = await Submitted(w, SiteWithLunch(50m, [Ali]));          // Hasan and Omar came but did not eat

        Assert.Equal(12m, claim.Lines.Single(l => l.Kind == "Lunch").Amount);
        Assert.False(claim.ClaimantAteLunch);
        Assert.Equal([true, false], claim.People.OrderBy(p => p.Name).Select(p => p.AteLunch));
    }

    [Fact]
    public void Lunch_is_only_for_site_trips_and_needs_who_ate_and_the_bill_amount()
    {
        var v = new SubmitTravelClaimCommandValidator();
        Assert.False(v.Validate(Ride() with { AteLunch = [Hasan], LunchBillAmount = 20m }).IsValid);              // office trip
        Assert.False(v.Validate(Ride(type: TravelTripType.Site) with { SiteName = "Lusail", LunchBillAmount = 20m }).IsValid);         // nobody ticked
        Assert.False(v.Validate(Ride(type: TravelTripType.Site) with { SiteName = "Lusail", AteLunch = [Hasan] }).IsValid);            // no bill amount
        Assert.True(v.Validate(Ride(type: TravelTripType.Site) with { SiteName = "Lusail", AteLunch = [Hasan], LunchBillAmount = 20m }).IsValid);
        Assert.False(v.Validate(Ride(type: TravelTripType.Site) with { SiteName = " " }).IsValid);                // a site trip needs the site name
        Assert.True(v.Validate(Ride()).IsValid);
        Assert.True(v.Validate(Ride(type: TravelTripType.Site) with { SiteName = "Lusail" }).IsValid);
        Assert.False(v.Validate(Ride() with { TripType = null }).IsValid);
        Assert.False(v.Validate(Ride() with { ReceiptFileName = "uber.jpg" }).IsValid);      // Uber's PDF only — not a photo or screenshot
    }

    [Fact]
    public async Task A_lunch_needs_its_bill_file_and_only_people_on_the_trip_can_eat()
    {
        var w = await NewWorld();

        var noFile = await Submit(w, SiteWithLunch(20m, [Hasan]) with { LunchBill = null, LunchBillSize = 0, LunchBillFileName = null });
        Assert.False(noFile.Succeeded);
        Assert.Contains("lunch bill", noFile.Errors.Single());

        var stranger = await Submit(w, SiteWithLunch(20m, [Hasan, Accountant], with: [Ali]));
        Assert.False(stranger.Succeeded);
        Assert.Contains("people who came with you", stranger.Errors.Single());
    }

    [Fact]
    public async Task Nobody_gets_two_lunches_on_the_same_day_but_a_rejected_one_does_not_count()
    {
        var w = await NewWorld();
        var first = await Submitted(w, SiteWithLunch(24m, [Hasan, Ali], with: [Ali]));

        // Ali submits his own trip the same day and claims lunch again.
        var again = await Submit(w, SiteWithLunch(12m, [Ali], with: [Omar]), new User(Ali, "Ali", Roles.Technician));
        Assert.False(again.Succeeded);
        Assert.Contains("You already had lunch paid on 22 Nov (UB-00001)", again.Errors.Single());   // Ali is told in his own words

        // Hasan cannot put Ali's second lunch on his own claim either.
        var viaHasan = await Submit(w, SiteWithLunch(12m, [Ali], with: [Ali]));
        Assert.Contains("Ali already had lunch paid on 22 Nov (UB-00001)", viaHasan.Errors.Single());

        // Another day is fine.
        Assert.True((await Submit(w, SiteWithLunch(12m, [Ali], with: [Omar], day: Today.AddDays(-1)), new User(Ali, "Ali", Roles.Technician))).Succeeded);

        // Once the first claim is rejected, its lunch no longer counts.
        await new RejectTravelClaimCommandHandler(w.Db, Acc, w.Clock).Handle(new RejectTravelClaimCommand(first.Id, "Wrong site"), default);
        Assert.True((await Submit(w, SiteWithLunch(12m, [Ali], with: [Omar]), new User(Ali, "Ali", Roles.Technician))).Succeeded);
    }

    [Fact]
    public async Task The_same_receipt_cannot_be_claimed_twice_unless_the_first_claim_was_withdrawn()
    {
        var w = await NewWorld();
        var first = await Submitted(w, Ride(sameReceipt: "R1"));

        var twice = await Submit(w, Ride(sameReceipt: "R1"));
        Assert.False(twice.Succeeded);
        Assert.Contains("already claimed (UB-00001)", twice.Errors.Single());

        await new WithdrawTravelClaimCommandHandler(w.Db, Me, w.Clock).Handle(new WithdrawTravelClaimCommand(first.Id), default);
        Assert.True((await Submit(w, Ride(sameReceipt: "R1"))).Succeeded);
    }

    [Fact]
    public async Task Dates_people_files_and_who_may_claim_are_checked()
    {
        var w = await NewWorld();
        Assert.Contains("future", (await Submit(w, Ride(day: Today.AddDays(1)))).Errors.Single());
        Assert.Contains("more than 90 days", (await Submit(w, Ride(day: Today.AddDays(-91)))).Errors.Single());
        var unreadable = Ride() with { Receipt = new MemoryStream("%PDF-1.4 a scanned page"u8.ToArray()), ReceiptSize = 23 };
        Assert.Contains("could not read the date and amount", (await Submit(w, unreadable)).Errors.Single());
        Assert.Contains("not found", (await Submit(w, Ride(with: [Gone]))).Errors.Single());           // left the company
        Assert.Contains("Only staff", (await Submit(w, Ride(), new User(Guid.NewGuid(), "Client", Roles.Client))).Errors.Single());

        var fake = Ride() with { Receipt = new MemoryStream("not a pdf at all"u8.ToArray()), ReceiptSize = 16 };
        Assert.Contains("does not open", (await Submit(w, fake)).Errors.Single());
        Assert.Contains("must be a PDF or a photo", (await Submit(w, Ride() with { ReceiptFileName = "uber.exe" })).Errors.Single());
        Assert.Empty(await w.Db.TravelClaims.ToListAsync());
    }

    [Fact]
    public async Task A_new_lunch_rate_applies_from_its_day_and_old_claims_keep_theirs()
    {
        var w = await NewWorld();
        var before = await Submitted(w, SiteWithLunch(100m, [Hasan], day: Today.AddDays(-5)));

        await new SetLunchRateCommandHandler(w.Db, Acc, w.Clock).Handle(new SetLunchRateCommand(15m, Today.AddDays(-2)), default);

        var after = await Submitted(w, SiteWithLunch(100m, [Hasan]));
        var olderTrip = await Submitted(w, SiteWithLunch(100m, [Ali], with: [Ali], day: Today.AddDays(-3)));
        Assert.Equal(12m, before.Lines.Single(l => l.Kind == "Lunch").Amount);
        Assert.Equal(15m, after.Lines.Single(l => l.Kind == "Lunch").Amount);
        Assert.Equal(12m, olderTrip.Lines.Single(l => l.Kind == "Lunch").Amount);   // a trip before the new rate started
        Assert.False(new SetLunchRateCommandValidator().Validate(new SetLunchRateCommand(0m, null)).IsValid);
    }

    // ===== Deciding =====

    [Fact]
    public async Task The_accountant_approves_or_rejects_with_a_reason_but_never_their_own_claim()
    {
        var w = await NewWorld();
        var claim = await Submitted(w, Ride());

        var approved = await Approved(w, claim);
        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Rania <rania@jamago.qa>", approved.ApprovedByName);
        Assert.Equal(["Submitted", "Approved"], approved.Events.Select(e => e.Action));

        var own = await Submitted(w, Ride(), Acc);
        var self = await Approve(w, Acc, own.Id);
        Assert.False(self.Succeeded);
        Assert.Contains("your own claim", self.Errors.Single());

        Assert.False(new RejectTravelClaimCommandValidator().Validate(new RejectTravelClaimCommand(claim.Id, " ")).IsValid);
        var rejected = await new RejectTravelClaimCommandHandler(w.Db, Acc, w.Clock).Handle(new RejectTravelClaimCommand(claim.Id, "  Duplicate trip  "), default);
        Assert.Equal(("Rejected", "Duplicate trip"), (rejected.Data!.Status, rejected.Data.RejectReason));
        Assert.False((await Approve(w, Acc, claim.Id)).Succeeded);
    }

    [Fact]
    public async Task Only_a_claim_not_yet_decided_can_be_withdrawn_and_only_by_its_claimant()
    {
        var w = await NewWorld();
        var claim = await Submitted(w, Ride());

        Assert.False((await new WithdrawTravelClaimCommandHandler(w.Db, new User(Ali, "Ali"), w.Clock).Handle(new WithdrawTravelClaimCommand(claim.Id), default)).Succeeded);
        var withdrawn = await new WithdrawTravelClaimCommandHandler(w.Db, Me, w.Clock).Handle(new WithdrawTravelClaimCommand(claim.Id), default);
        Assert.Equal("Withdrawn", withdrawn.Data!.Status);
        Assert.Equal(1, await w.Db.TravelClaims.CountAsync());                    // kept on record, never deleted

        var other = await Approved(w, await Submitted(w, Ride()));
        Assert.Contains("already decided", (await new WithdrawTravelClaimCommandHandler(w.Db, Me, w.Clock).Handle(new WithdrawTravelClaimCommand(other.Id), default)).Errors.Single());
    }

    // ===== Approving many at once, with adjusted amounts =====

    [Fact]
    public async Task Ten_bills_approved_at_once_with_two_lowered_add_up_until_paid_then_start_again_from_zero()
    {
        var w = await NewWorld();
        var bills = new List<TravelClaimDto>();
        for (var d = 0; d < 10; d++) bills.Add(await Submitted(w, Ride(25m, day: Today.AddDays(-d))));

        var approved = await ApproveMany(w, bills.Select((b, i) => i < 2
            ? new TravelClaimApproval(b.Id, 24m, "Waiting time charged")          // lowered by 1 QAR, with a reason
            : new TravelClaimApproval(b.Id, null, null)).ToArray());

        Assert.True(approved.Succeeded, approved.Errors.FirstOrDefault());
        Assert.Equal(2, approved.Data!.Count(c => c.ApprovedAmount == 24m && c.AdjustmentReason == "Waiting time charged" && c.Total == 25m));
        Assert.Contains("Approved 24.00 of 25.00 QAR claimed — Waiting time charged", approved.Data!.First().Events.Last().Note);
        var mine = async () => (await new GetMyTravelClaimsQueryHandler(w.Db, Me, w.Clock).Handle(new GetMyTravelClaimsQuery(), default)).Data!.Balance;
        Assert.Equal((248m, 10), ((await mine()).Owed, (await mine()).OwedCount));           // 8 × 25 + 2 × 24

        var payout = await new ReimburseTravelClaimsCommandHandler(w.Db, Acc, w.Clock)
            .Handle(new ReimburseTravelClaimsCommand(bills.Select(b => b.Id).ToList(), ReimbursementMethod.Cash, null), default);
        Assert.Equal(248m, payout.Data!.Amount);                                               // he is paid what was approved
        Assert.Equal((0m, 248m), ((await mine()).Owed, (await mine()).PaidTotal));             // back to zero

        // The next bills start a new balance.
        await Approved(w, await Submitted(w, Ride(30m)));
        Assert.Equal((30m, 1, 248m), ((await mine()).Owed, (await mine()).OwedCount, (await mine()).PaidTotal));
    }

    [Fact]
    public async Task A_lowered_amount_needs_a_reason_can_never_go_above_the_claim_and_one_bad_bill_stops_the_whole_batch()
    {
        var w = await NewWorld();
        var a = await Submitted(w, Ride(25m));
        var b = await Submitted(w, Ride(30m));

        var noReason = await ApproveMany(w, new(a.Id, null, null), new(b.Id, 29m, "  "));
        Assert.False(noReason.Succeeded);
        Assert.Contains("write why it is approved at 29.00 instead of 30.00", noReason.Errors.Single());
        Assert.Equal(0, await w.Db.TravelClaims.CountAsync(c => c.Status == TravelClaimStatus.Approved));   // nothing approved, not even A

        Assert.Contains("cannot be more than the 30.00", (await ApproveMany(w, new TravelClaimApproval(b.Id, 31m, "Tip"))).Errors.Single());
        var v = new ApproveTravelClaimsCommandValidator();
        Assert.False(v.Validate(new ApproveTravelClaimsCommand([new(b.Id, 0m, "Nothing")])).IsValid);         // nothing = reject instead
        Assert.False(v.Validate(new ApproveTravelClaimsCommand([new(b.Id, 10.001m, "x")])).IsValid);
        Assert.False(v.Validate(new ApproveTravelClaimsCommand([new(b.Id, null, null), new(b.Id, null, null)])).IsValid);
        Assert.False(v.Validate(new ApproveTravelClaimsCommand([])).IsValid);

        var ok = await ApproveMany(w, new(a.Id, null, null), new(b.Id, 29m, "Toll not allowed"));
        Assert.True(ok.Succeeded);
        Assert.Contains("no longer waiting", (await ApproveMany(w, new TravelClaimApproval(a.Id, null, null))).Errors.Single());
    }

    [Fact]
    public async Task The_accountants_own_bill_in_a_batch_stops_the_batch()
    {
        var w = await NewWorld();
        var hasans = await Submitted(w, Ride());
        var own = await Submitted(w, Ride(), Acc);

        var r = await ApproveMany(w, new(hasans.Id, null, null), new(own.Id, null, null));

        Assert.False(r.Succeeded);
        Assert.Contains("your own claim", r.Errors.Single());
        Assert.Equal(0, await w.Db.TravelClaims.CountAsync(c => c.Status == TravelClaimStatus.Approved));
    }

    // ===== Paying back =====

    [Fact]
    public async Task One_payout_settles_several_claims_of_one_person_and_records_when_how_and_how_long()
    {
        var w = await NewWorld();
        var a = await Approved(w, await Submitted(w, Ride(20m)));
        var b = await Approved(w, await Submitted(w, SiteWithLunch(30m, [Hasan, Ali], with: [Ali])));
        w.Clock.Now = w.Clock.Now.AddDays(3);                                     // the accountant pays three days later

        var payout = await new ReimburseTravelClaimsCommandHandler(w.Db, Acc, w.Clock)
            .Handle(new ReimburseTravelClaimsCommand([a.Id, b.Id], ReimbursementMethod.Cash, "Handed in the office"), default);

        Assert.True(payout.Succeeded, payout.Errors.FirstOrDefault());
        Assert.Equal(("RB-00001", 20m + 40m + 24m, "Cash"), (payout.Data!.Number, payout.Data.Amount, payout.Data.Method));
        Assert.Equal(("Hasan", "Rania <rania@jamago.qa>"), (payout.Data.PayeeName, payout.Data.PaidByName));
        Assert.Equal(["UB-00001", "UB-00002"], payout.Data.ClaimNumbers);

        var mine = (await new GetMyTravelClaimsQueryHandler(w.Db, Me, w.Clock).Handle(new GetMyTravelClaimsQuery(), default)).Data!;
        Assert.All(mine.Claims, c =>
        {
            Assert.Equal("Paid", c.Status);
            Assert.Equal("RB-00001", c.ReimbursementNumber);
            Assert.Equal(w.Clock.Now.UtcDateTime, c.PaidAt);
            Assert.Equal(3, c.DaysWaiting);                                       // submitted to paid back
            Assert.Equal("Reimbursed", c.Events.Last().Action);
        });
        Assert.Equal((0m, 84m, 84m), (mine.Balance.Owed, mine.Balance.PaidThisMonth, mine.Balance.PaidTotal));
    }

    [Fact]
    public async Task A_payout_is_for_approved_claims_of_one_person_and_never_your_own()
    {
        var w = await NewWorld();
        var mine = await Approved(w, await Submitted(w, Ride()));
        var alis = await Approved(w, await Submitted(w, Ride(), new User(Ali, "Ali", Roles.Technician)));
        var waiting = await Submitted(w, Ride());
        var pay = new ReimburseTravelClaimsCommandHandler(w.Db, Acc, w.Clock);

        Assert.Contains("one person at a time", (await pay.Handle(new([mine.Id, alis.Id], ReimbursementMethod.Cash, null), default)).Errors.Single());
        Assert.Contains("not approved", (await pay.Handle(new([waiting.Id], ReimbursementMethod.Cash, null), default)).Errors.Single());
        // The accountant's own Uber bill: another accountant approves it, and still the accountant cannot pay it to themself.
        var secondAccountant = new User(Guid.NewGuid(), "Boss", Roles.Admin, accountant: true);
        var accOwn = await Submitted(w, Ride(), Acc);
        Assert.True((await Approve(w, secondAccountant, accOwn.Id)).Succeeded);
        Assert.Contains("your own", (await pay.Handle(new([accOwn.Id], ReimbursementMethod.Cash, null), default)).Errors.Single());
        Assert.True((await new ReimburseTravelClaimsCommandHandler(w.Db, secondAccountant, w.Clock).Handle(new([accOwn.Id], ReimbursementMethod.Cash, null), default)).Succeeded);
        Assert.False(new ReimburseTravelClaimsCommandValidator().Validate(new ReimburseTravelClaimsCommand([mine.Id], null, null)).IsValid);
        Assert.True((await pay.Handle(new([mine.Id], ReimbursementMethod.BankTransfer, "QIIB ref 7781"), default)).Succeeded);
        Assert.Contains("not approved", (await pay.Handle(new([mine.Id], ReimbursementMethod.Cash, null), default)).Errors.Single());   // never paid twice
    }

    // ===== Balances and the accountant's board =====

    [Fact]
    public async Task Each_person_sees_what_is_waiting_what_is_owed_and_for_how_long()
    {
        var w = await NewWorld();
        await Submitted(w, Ride(10m));
        await Approved(w, await Submitted(w, Ride(25m)));
        await Submitted(w, Ride(7m), new User(Ali, "Ali", Roles.Technician));
        w.Clock.Now = w.Clock.Now.AddDays(4);

        var board = (await new GetTravelClaimsQueryHandler(w.Db, w.Clock).Handle(new GetTravelClaimsQuery(null, null, null), default)).Data!;

        Assert.Equal((17m, 2, 25m, 1), (board.ToApproveTotal, board.ToApproveCount, board.OwedTotal, board.OwedCount));
        var hasan = board.People.Single(p => p.PersonId == Hasan);
        Assert.Equal(("Hasan", 10m, 1, 25m, 1, 4), (hasan.Name, hasan.WaitingApproval, hasan.WaitingCount, hasan.Owed, hasan.OwedCount, hasan.OldestOpenDays));
        Assert.Equal(3, board.Claims.Count);
        Assert.Equal(12m, board.LunchRate);
        Assert.Single((await new GetTravelClaimsQueryHandler(w.Db, w.Clock).Handle(new GetTravelClaimsQuery(Ali, null, null), default)).Data!.Claims);
    }

    // ===== Files =====

    [Fact]
    public async Task A_receipt_opens_for_the_claimant_and_the_accountant_only()
    {
        var w = await NewWorld();
        var claim = await Submitted(w, Ride());
        var line = claim.Lines.Single().Id;

        Assert.True((await new GetTravelClaimFileQueryHandler(w.Db, w.Files, Me).Handle(new(claim.Id, line), default)).Succeeded);
        Assert.True((await new GetTravelClaimFileQueryHandler(w.Db, w.Files, Acc).Handle(new(claim.Id, line), default)).Succeeded);
        Assert.False((await new GetTravelClaimFileQueryHandler(w.Db, w.Files, new User(Ali, "Ali")).Handle(new(claim.Id, line), default)).Succeeded);
    }

    [Fact]
    public async Task Reading_a_receipt_fills_the_form_from_a_pdf_and_leaves_a_photo_for_typing()
    {
        var (s, n) = Pdf();
        var read = await new ReadUberReceiptQueryHandler(new PdfText("Total QAR 19.75\nNovember 21, 2026\n9:05 AM Office"), Me)
            .Handle(new ReadUberReceiptQuery("trip.pdf", n, s), default);
        Assert.Equal((19.75m, new DateOnly(2026, 11, 21)), (read.Data!.Amount, read.Data.TripOn));

        var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        var photo = await new ReadUberReceiptQueryHandler(new PdfText("Total QAR 99"), Me).Handle(new ReadUberReceiptQuery("trip.jpg", jpg.Length, new MemoryStream(jpg)), default);
        Assert.Null(photo.Data!.Amount);
    }
}
