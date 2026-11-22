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

    private static MoiJob MoiJob(string no, Guid tech, string status, DateTime? at, decimal amount = 0) => new()
    {
        Id = Guid.NewGuid(), JobNumber = no, CustomerName = "C " + no, Location = "Doha", AssignedToId = tech,
        AssignedToName = tech == Ali ? "Ali" : "Omar", Status = status, ScheduledAt = at, AmountPayable = amount,
    };

    [Fact]
    public async Task Moi_jobs_show_beside_Non_MOI_in_jobs_technicians_invoices_approvals_and_revenue()
    {
        var d = await Run(c =>
        {
            var non = Job("NMJ-1", Ali, "Sent", Now, 400);
            var moiSent = MoiJob("MOJ-1", Omar, "Sent", Now, 300);
            var moiDone = MoiJob("MOJ-2", Omar, "Completed", Now.AddHours(1), 0);
            var moiLate = MoiJob("MOJ-3", Ali, "Assigned", Now.AddDays(-2), 0);
            c.NonMoiJobs.Add(non);
            c.MoiJobs.AddRange(moiSent, moiDone, moiLate);
            c.NonMoiJobPayments.Add(new NonMoiJobPayment { NonMoiJobId = non.Id, Method = "Online", Amount = 80, ReceivedOn = new DateOnly(2026, 11, 2), RecordedByName = "x" });
            c.MoiJobPayments.Add(new MoiJobPayment { MoiJobId = moiSent.Id, Method = "Cash", Amount = 120, ReceivedOn = new DateOnly(2026, 11, 2), RecordedByName = "x" });
        });

        Assert.Equal(["MOJ-1", "NMJ-1"], d.OpenInvoices.Select(i => i.JobNumber).OrderBy(x => x).ToArray());
        Assert.Equal(["MOI", "NonMOI"], d.OpenInvoices.Select(i => i.Kind).OrderBy(x => x).ToArray());
        Assert.Equal(700m, d.OpenInvoicesTotal);
        Assert.Equal(1, d.Approvals.MoiJobsToClose);                    // MOJ-2 is done and waiting to be billed
        Assert.Equal(0, d.Approvals.JobsToClose);
        Assert.Equal(["MOJ-1", "MOJ-2", "NMJ-1"], d.TodayJobs.Select(j => j.JobNumber).OrderBy(x => x).ToArray());
        Assert.Equal(["MOI", "MOI", "NonMOI"], d.TodayJobs.OrderBy(j => j.JobNumber).Select(j => j.Kind).ToArray());
        Assert.Equal(1, d.OverdueJobs);                                  // MOJ-3 is two days late
        Assert.Equal("Overdue", d.Technicians.Single(t => t.Name == "Ali").State);
        Assert.Equal(("MOI", "MOJ-3"), (d.Technicians.Single(t => t.Name == "Ali").Kind, d.Technicians.Single(t => t.Name == "Ali").JobNumber));
        Assert.Equal(2, d.Technicians.Single(t => t.Name == "Omar").JobsToday);
        Assert.Equal((200m, 80m, 120m), (d.RevenueThisMonth, d.RevenueNonMoiThisMonth, d.RevenueMoiThisMonth));
    }

    [Fact]
    public async Task What_clients_still_owe_is_the_unpaid_part_of_approved_bills_of_both_kinds()
    {
        var d = await Run(c =>
        {
            var a = Job("N1", Ali, "Closed", null, 1000);
            var b = Job("N2", Ali, "Sent", null, 900);                  // not approved yet: not owed
            var m1 = MoiJob("M1", Omar, "Closed", null, 500);
            var m2 = MoiJob("M2", Omar, "Closed", null, 300);
            c.NonMoiJobs.AddRange(a, b);
            c.MoiJobs.AddRange(m1, m2);
            c.NonMoiJobPayments.Add(new NonMoiJobPayment { NonMoiJobId = a.Id, Method = "Cash", Amount = 400, ReceivedOn = new DateOnly(2026, 11, 3), RecordedByName = "x" });
            c.MoiJobPayments.Add(new MoiJobPayment { MoiJobId = m1.Id, Method = "Online", Amount = 500, ReceivedOn = new DateOnly(2026, 11, 3), RecordedByName = "x" });
        });

        Assert.Equal(900m, d.UnpaidTotal);       // 600 left on N1 + 300 on M2; M1 is fully paid
        Assert.Equal(2, d.UnpaidJobs);
    }

    [Fact]
    public async Task Cash_figures_show_what_people_hold_what_was_received_today_and_the_difference_this_month()
    {
        var boss = Guid.NewGuid();
        var d = await Run(c =>
        {
            var n = Job("N1", Ali, "Closed", null, 1000);
            var m = MoiJob("M1", Omar, "Closed", null, 500);
            var handed = new CashHandover { Id = Guid.NewGuid(), Kind = "NonMOI", CollectorName = "Ali", ExpectedAmount = 100, ReceivedAmount = 95, ReceivedByName = "Acc", CreatedAt = Now.AddHours(-1) };
            var lastMonth = new CashHandover { Id = Guid.NewGuid(), Kind = "MOI", CollectorName = "Omar", ExpectedAmount = 100, ReceivedAmount = 60, ReceivedByName = "Acc", CreatedAt = Now.AddDays(-20) };
            c.NonMoiJobs.Add(n); c.MoiJobs.Add(m);
            c.CashHandovers.AddRange(handed, lastMonth);
            c.NonMoiJobPayments.AddRange(
                new NonMoiJobPayment { NonMoiJobId = n.Id, Method = "Cash", Amount = 100, ReceivedOn = new DateOnly(2026, 11, 14), CollectedById = Ali, CollectedByName = "Ali", RecordedById = Ali, RecordedByName = "x", CashHandoverId = handed.Id },
                new NonMoiJobPayment { NonMoiJobId = n.Id, Method = "Cash", Amount = 150, ReceivedOn = new DateOnly(2026, 11, 15), CollectedById = Ali, CollectedByName = "Ali", RecordedById = Ali, RecordedByName = "x" },
                new NonMoiJobPayment { NonMoiJobId = n.Id, Method = "Cash", Amount = 50, ReceivedOn = new DateOnly(2026, 11, 15), RecordedById = boss, RecordedByName = "Boss" },   // office cash: the recording admin holds it
                new NonMoiJobPayment { NonMoiJobId = n.Id, Method = "Online", Amount = 700, ReceivedOn = new DateOnly(2026, 11, 15), RecordedById = boss, RecordedByName = "Boss" });
            c.MoiJobPayments.Add(new MoiJobPayment { MoiJobId = m.Id, Method = "Cash", Amount = 70, ReceivedOn = new DateOnly(2026, 11, 15), CollectedById = Omar, CollectedByName = "Omar", RecordedById = Omar, RecordedByName = "x" });
        });

        Assert.Equal((200m, 2), (d.Cash.WaitingNonMoi, d.Cash.PeopleNonMoi));     // Ali's 150 and Boss's 50; the handed-over 100 and the online 700 are not waiting
        Assert.Equal((70m, 1), (d.Cash.WaitingMoi, d.Cash.PeopleMoi));
        Assert.Equal(270m, d.Cash.WaitingTotal);
        Assert.Equal((95m, 1), (d.Cash.ReceivedToday, d.Cash.HandoversToday));
        Assert.Equal(-5m, d.Cash.DifferenceThisMonth);                           // last month's shortfall is not counted
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
