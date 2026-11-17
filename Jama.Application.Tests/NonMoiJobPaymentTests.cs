using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.NonMoiJobs;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>Money received for a Non-MOI job: cash with who collected it, online with its receipt, by Doha day.</summary>
public class NonMoiJobPaymentTests
{
    private sealed class FakeUser(bool super = false) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => "Sara";
        public string Role => Roles.Admin;
        public bool Has(string permission) => true;
        public bool IsSuperAdmin { get; } = super;
    }

    private sealed class MemoryStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public async Task<string> SaveAsync(Stream content, string key, CancellationToken ct)
        {
            using var copy = new MemoryStream(); await content.CopyToAsync(copy, ct); Files[key] = copy.ToArray(); return key;
        }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
            Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);
        public Task DeleteAsync(string key, CancellationToken ct) { Files.Remove(key); return Task.CompletedTask; }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // 15 Nov 2026, 23:30 in Doha (20:30 UTC): still the 15th there.
    private static readonly FixedTime Clock = new(new DateTimeOffset(2026, 11, 15, 20, 30, 0, TimeSpan.Zero));
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3];
    private static readonly Guid Tech = Guid.NewGuid();

    private static async Task<(ApplicationDbContext Db, NonMoiJob Job)> Seed(string status = "Sent", decimal payable = 1000)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"pay-{Guid.NewGuid()}").Options);
        db.AdminUsers.Add(new AdminUser { Id = Tech, Email = "ali@x.test", FullName = "Ali", Role = Roles.Technician });
        var job = new NonMoiJob
        {
            Id = Guid.NewGuid(), JobNumber = "NMJ-00009", CustomerName = "Imane", Status = status, AssignedToId = Tech,
            AssignedToName = "Ali", WorkTotal = payable, AmountPayable = payable, EmailSentAt = DateTime.UtcNow,
        };
        db.NonMoiJobs.Add(job);
        await db.SaveChangesAsync();
        return (db, job);
    }

    private static AddNonMoiJobPaymentCommandHandler Add(ApplicationDbContext db, MemoryStorage? s = null) =>
        new(db, s ?? new MemoryStorage(), new FakeUser(), Clock);

    [Fact]
    public async Task Cash_is_recorded_with_who_collected_it_on_todays_Doha_date()
    {
        var (db, job) = await Seed();
        var r = await Add(db).Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Method = "cash", Amount = 400, CollectedById = Tech }, default);

        Assert.True(r.Succeeded, string.Join(",", r.Errors));
        var p = await db.NonMoiJobPayments.SingleAsync();
        Assert.Equal(("Cash", 400m, "Ali", new DateOnly(2026, 11, 15)), (p.Method, p.Amount, p.CollectedByName, p.ReceivedOn));
        Assert.Contains("collected by Ali", (await db.NonMoiJobEvents.SingleAsync()).Note);
    }

    [Fact]
    public async Task Online_needs_the_receipt_and_keeps_it()
    {
        var (db, job) = await Seed();
        var storage = new MemoryStorage();

        var without = await Add(db, storage).Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Method = "Online", Amount = 300 }, default);
        Assert.False(without.Succeeded);

        var with = await Add(db, storage).Handle(new AddNonMoiJobPaymentCommand
        {
            JobId = job.Id, Method = "Online", Amount = 300, ReceivedOn = new DateOnly(2026, 11, 14),
            ReceiptFileName = "transfer.png", ReceiptContentType = "image/png", ReceiptSize = Png.Length, Receipt = new MemoryStream(Png),
        }, default);
        Assert.True(with.Succeeded, string.Join(",", with.Errors));
        Assert.Equal(Png, storage.Files.Values.Single());
        Assert.True(with.Data!.HasReceipt);
        Assert.Equal(new DateOnly(2026, 11, 14), with.Data.ReceivedOn);
    }

    [Fact]
    public async Task A_fake_receipt_a_future_date_and_paying_more_than_the_balance_are_refused()
    {
        var (db, job) = await Seed(payable: 500);
        var h = Add(db);

        Assert.False((await h.Handle(new AddNonMoiJobPaymentCommand
        {
            JobId = job.Id, Method = "Online", Amount = 100, ReceiptFileName = "r.png", ReceiptSize = 3, Receipt = new MemoryStream([1, 2, 3]),
        }, default)).Succeeded);
        Assert.False((await h.Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Amount = 100, ReceivedOn = new DateOnly(2026, 11, 16) }, default)).Succeeded);

        Assert.True((await h.Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Amount = 300 }, default)).Succeeded);
        var over = await h.Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Amount = 250 }, default);
        Assert.False(over.Succeeded);
        Assert.Contains("200.00", over.Errors.Single());
        Assert.True((await h.Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Amount = 200 }, default)).Succeeded);
        Assert.Contains("fully paid", (await h.Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Amount = 1 }, default)).Errors.Single());
    }

    [Fact]
    public async Task Nothing_can_be_paid_before_the_technician_finishes()
    {
        var (db, job) = await Seed(status: "Assigned");
        Assert.False((await Add(db).Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Amount = 10 }, default)).Succeeded);
    }

    [Fact]
    public async Task Only_the_super_admin_can_remove_a_payment()
    {
        var (db, job) = await Seed();
        await Add(db).Handle(new AddNonMoiJobPaymentCommand { JobId = job.Id, Amount = 100 }, default);
        var id = (await db.NonMoiJobPayments.SingleAsync()).Id;

        Assert.False((await new DeleteNonMoiJobPaymentCommandHandler(db, new MemoryStorage(), new FakeUser(), Clock)
            .Handle(new DeleteNonMoiJobPaymentCommand(id), default)).Succeeded);
        Assert.True((await new DeleteNonMoiJobPaymentCommandHandler(db, new MemoryStorage(), new FakeUser(super: true), Clock)
            .Handle(new DeleteNonMoiJobPaymentCommand(id), default)).Succeeded);
        Assert.Empty(db.NonMoiJobPayments);
        Assert.Contains(db.NonMoiJobEvents, e => e.Action == "PaymentRemoved");
    }

    [Fact]
    public async Task Collections_list_a_day_with_cash_and_online_totals()
    {
        var (db, job) = await Seed(payable: 5000);
        db.NonMoiJobPayments.AddRange(
            new NonMoiJobPayment { NonMoiJobId = job.Id, Method = "Cash", Amount = 100, ReceivedOn = new DateOnly(2026, 11, 15), RecordedByName = "x" },
            new NonMoiJobPayment { NonMoiJobId = job.Id, Method = "Online", Amount = 250, ReceivedOn = new DateOnly(2026, 11, 15), RecordedByName = "x" },
            new NonMoiJobPayment { NonMoiJobId = job.Id, Method = "Cash", Amount = 70, ReceivedOn = new DateOnly(2026, 11, 14), RecordedByName = "x" });
        await db.SaveChangesAsync();
        var h = new GetCollectionsQueryHandler(db, Clock);

        var today = (await h.Handle(new GetCollectionsQuery(null, null), default)).Data!;
        Assert.Equal((100m, 250m, 350m, 2), (today.Cash, today.Online, today.Total, today.Rows.Count));
        Assert.Equal("NMJ-00009", today.Rows[0].JobNumber);

        var yesterday = (await h.Handle(new GetCollectionsQuery(new DateOnly(2026, 11, 14), null), default)).Data!;
        Assert.Equal(70m, yesterday.Total);
    }
}
