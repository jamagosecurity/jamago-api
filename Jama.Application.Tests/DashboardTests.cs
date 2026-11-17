using Jama.Application.Dashboard;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>The super admin's dashboard: Doha days and months, revenue, open invoices, technicians, mail, approvals.</summary>
public class DashboardTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Sunday 15 Nov 2026, 11:00 in Doha (08:00 UTC).
    private static readonly DateTime Now = new(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Ali = Guid.NewGuid(), Omar = Guid.NewGuid(), Sami = Guid.NewGuid(), Lina = Guid.NewGuid();

    private static NonMoiJob Job(string no, Guid tech, string status, DateTime? at, decimal amount = 0) => new()
    {
        Id = Guid.NewGuid(), JobNumber = no, CustomerName = "C " + no, Location = "Doha", AssignedToId = tech,
        AssignedToName = tech == Ali ? "Ali" : tech == Omar ? "Omar" : "Sami", Status = status, ScheduledAt = at,
        AmountPayable = amount,
    };

    private static async Task<DashboardDto> Run(Action<ApplicationDbContext> seed)
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"dash-{Guid.NewGuid()}").Options);
        foreach (var (id, name) in new[] { (Ali, "Ali"), (Omar, "Omar"), (Sami, "Sami"), (Lina, "Lina") })
            context.Staff.Add(new Staff
            {
                AdminUserId = id, FullName = name, Department = id == Lina ? "Panels" : "Technician",
                Account = new AdminUser { Id = id, Email = $"{name}@x.test", FullName = name, IsActive = true },
            });
        seed(context);
        await context.SaveChangesAsync();
        var mail = Microsoft.Extensions.Options.Options.Create(new MailSettings
        {
            Accounts = [new MailAccountSettings { Key = "info", Address = "info@jamago.qa" }, new MailAccountSettings { Key = "moi", Address = "moi@jamago.qa" }],
        });
        var result = await new GetDashboardQueryHandler(context, mail, new FixedTime(Now)).Handle(new GetDashboardQuery(), default);
        Assert.True(result.Succeeded);
        return result.Data!;
    }

    [Fact]
    public async Task Revenue_is_the_money_received_this_Doha_month()
    {
        var d = await Run(c =>
        {
            var a = Job("A", Ali, "Closed", null, 1000);
            var b = Job("B", Ali, "Sent", null, 900);
            c.NonMoiJobs.AddRange(a, b);
            c.NonMoiJobPayments.AddRange(
                new NonMoiJobPayment { NonMoiJobId = a.Id, Method = "Cash", Amount = 600, ReceivedOn = new DateOnly(2026, 11, 3), RecordedByName = "x" },
                new NonMoiJobPayment { NonMoiJobId = a.Id, Method = "Online", Amount = 400, ReceivedOn = new DateOnly(2026, 11, 1), RecordedByName = "x" },
                new NonMoiJobPayment { NonMoiJobId = b.Id, Method = "Cash", Amount = 250, ReceivedOn = new DateOnly(2026, 10, 20), RecordedByName = "x" });
        });

        Assert.Equal(1000m, d.RevenueThisMonth);          // approved but unpaid invoices are not revenue
        Assert.Equal(250m, d.RevenueLastMonth);
        Assert.Equal((2, 600m, 400m), (d.PaymentsThisMonth, d.CashThisMonth, d.OnlineThisMonth));
    }

    [Fact]
    public async Task Open_invoices_are_the_ones_waiting_on_the_client_or_sent_back()
    {
        var d = await Run(c => c.NonMoiJobs.AddRange(
            Job("S1", Ali, "Sent", null, 400), Job("R1", Ali, "ChangesRequested", null, 100),
            Job("C1", Ali, "Completed", null, 50), Job("X1", Ali, "Closed", null, 70)));

        Assert.Equal(["S1", "R1"], d.OpenInvoices.Select(i => i.JobNumber).OrderByDescending(x => x).ToArray());
        Assert.Equal(500m, d.OpenInvoicesTotal);
        Assert.Equal(1, d.Approvals.JobsToClose);
    }

    [Fact]
    public async Task Today_and_each_technician_follow_the_Doha_clock()
    {
        var d = await Run(c => c.NonMoiJobs.AddRange(
            Job("ON", Ali, "Assigned", Now.AddHours(-1)),                 // 10:00 Doha: on the job now
            Job("NEXT", Omar, "Assigned", Now.AddHours(3)),               // 14:00 Doha: later today
            Job("LATE", Sami, "Assigned", Now.AddDays(-2)),               // two days ago, never done
            Job("TOMORROW", Omar, "Assigned", Now.AddDays(1))));

        Assert.Equal(["ON", "NEXT"], d.TodayJobs.Select(j => j.JobNumber).ToArray());
        Assert.Equal(1, d.OverdueJobs);
        Assert.Equal(3, d.Technicians.Count);                                  // Lina is not a technician
        Assert.Equal(("On job", "ON"), (d.Technicians.Single(t => t.Name == "Ali").State, d.Technicians.Single(t => t.Name == "Ali").JobNumber));
        Assert.Equal("Next job", d.Technicians.Single(t => t.Name == "Omar").State);
        Assert.Equal("Overdue", d.Technicians.Single(t => t.Name == "Sami").State);
    }

    [Fact]
    public async Task Unread_mail_and_approvals_are_counted()
    {
        var d = await Run(c =>
        {
            c.MailMessages.AddRange(
                new MailMessage { Id = Guid.NewGuid(), AccountKey = "info", Folder = "Inbox", IsRead = false, Uid = 1 },
                new MailMessage { Id = Guid.NewGuid(), AccountKey = "info", Folder = "Inbox", IsRead = false, Uid = 2 },
                new MailMessage { Id = Guid.NewGuid(), AccountKey = "info", Folder = "Inbox", IsRead = true, Uid = 3 },
                new MailMessage { Id = Guid.NewGuid(), AccountKey = "gone", Folder = "Inbox", IsRead = false, Uid = 4 });
            c.Boqs.Add(new Boq { Id = Guid.NewGuid(), BoqNumber = "Q1", ProjectName = "P", Status = BoqStatus.Submitted });
            c.Drawings.Add(new Drawing { Id = Guid.NewGuid(), DrawingNumber = "D1", ProjectName = "P", Status = DrawingStatus.Submitted });
            c.ClientRegistrations.Add(new ClientRegistration { Id = Guid.NewGuid(), FullName = "N", Email = "n@x.test", Mobile = "1", SiteLocation = "s" });
        });

        Assert.Equal(2, d.UnreadMail);
        Assert.Equal("info", d.Mailboxes.Single().Key);
        Assert.Equal((1, 1, 1, 3), (d.Approvals.Quotations, d.Approvals.Drawings, d.Approvals.ClientRegistrations, d.Approvals.Total));
    }
}
