using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.CashHandovers;
using Jama.Application.NonMoiJobs;
using Jama.Application.Options;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Dashboard;

/// <summary>Kind is "MOI" or "NonMOI"; the two kinds of job sit side by side on the dashboard.</summary>
public sealed record DashboardJobDto(
    Guid Id, string Kind, string JobNumber, string CustomerName, string Location, DateTime? ScheduledAt,
    Guid TechnicianId, string TechnicianName, string Status, DateTime? CompletedAt, decimal AmountPayable);

public sealed record DashboardTechnicianDto(
    Guid Id, string Name, string State, string? Kind, string? JobNumber, string? Location, DateTime? At,
    int JobsToday, int DoneToday);

public sealed record DashboardInvoiceDto(
    Guid Id, string Kind, string JobNumber, string CustomerName, decimal AmountPayable, string Status,
    DateTime? SentAt, string? ClientMessage);

public sealed record DashboardMailboxDto(string Key, string Address, int Unread);

public sealed record DashboardApprovalsDto(int Quotations, int Drawings, int ClientRegistrations, int JobsToClose, int MoiJobsToClose)
{
    public int Total => Quotations + Drawings + ClientRegistrations + JobsToClose + MoiJobsToClose;
}

/// <summary>The accountant's side: cash people still hold (per kind), what was received today, and the missing/extra this month.</summary>
public sealed record DashboardCashDto(
    decimal WaitingNonMoi, int PeopleNonMoi, decimal WaitingMoi, int PeopleMoi,
    decimal ReceivedToday, int HandoversToday, decimal DifferenceThisMonth)
{
    public decimal WaitingTotal => WaitingNonMoi + WaitingMoi;
}

public sealed record DashboardDto(
    DateTime Now,
    decimal RevenueThisMonth, decimal RevenueLastMonth, int PaymentsThisMonth, decimal CashThisMonth, decimal OnlineThisMonth,
    decimal QuotationsApprovedThisMonth, int QuotationsApprovedCount,
    decimal RevenueNonMoiThisMonth, decimal RevenueMoiThisMonth, decimal UnpaidTotal, int UnpaidJobs,
    DashboardCashDto Cash,
    decimal OpenInvoicesTotal, IReadOnlyList<DashboardInvoiceDto> OpenInvoices,
    IReadOnlyList<DashboardJobDto> TodayJobs, int OverdueJobs,
    IReadOnlyList<DashboardTechnicianDto> Technicians,
    int UnreadMail, IReadOnlyList<DashboardMailboxDto> Mailboxes,
    DashboardApprovalsDto Approvals);

/// <summary>
/// The super admin's one-page view: today's jobs (MOI and Non-MOI) and where each technician is, invoices still waiting
/// on the client, what clients still owe, cash the team holds and what the accountant received, unread company mail,
/// everything waiting for a decision, and this month's revenue.
/// Days and months are Doha days (UTC+3). Revenue = money actually received for MOI and Non-MOI jobs in the month (cash and online payments).
/// </summary>
public sealed record GetDashboardQuery : IRequest<ApiResult<DashboardDto>>;

public sealed class GetDashboardQueryHandler(IApplicationDbContext context, IOptions<MailSettings> mail, TimeProvider clock)
    : IRequestHandler<GetDashboardQuery, ApiResult<DashboardDto>>
{
    private static readonly TimeSpan Doha = TimeSpan.FromHours(3);

    public async Task<ApiResult<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var local = now + Doha;
        var dayStart = local.Date - Doha;                                   // today 00:00 Doha, in UTC
        var dayEnd = dayStart.AddDays(1);
        var monthStart = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc) - Doha;
        var lastMonthStart = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1) - Doha;

        var nonJobs = context.NonMoiJobs.AsNoTracking();
        var moiJobs = context.MoiJobs.AsNoTracking();

        // ----- Revenue: money received (cash collected + online transfers), by the day it came in -----
        var firstOfMonth = new DateOnly(local.Year, local.Month, 1);
        var firstOfLast = firstOfMonth.AddMonths(-1);
        var received = (await context.NonMoiJobPayments.AsNoTracking().Where(p => p.ReceivedOn >= firstOfLast)
            .Select(p => new { p.ReceivedOn, p.Amount, p.Method }).ToListAsync(ct))
            .Select(p => (Kind: CashKinds.NonMoi, p.ReceivedOn, p.Amount, p.Method)).ToList();
        received.AddRange((await context.MoiJobPayments.AsNoTracking().Where(p => p.ReceivedOn >= firstOfLast)
            .Select(p => new { p.ReceivedOn, p.Amount, p.Method }).ToListAsync(ct))
            .Select(p => (Kind: CashKinds.Moi, p.ReceivedOn, p.Amount, p.Method)));
        var thisMonth = received.Where(a => a.ReceivedOn >= firstOfMonth).ToList();
        var revenue = thisMonth.Sum(a => a.Amount);
        var revenueLast = received.Where(a => a.ReceivedOn < firstOfMonth).Sum(a => a.Amount);
        var revenueNon = thisMonth.Where(a => a.Kind == CashKinds.NonMoi).Sum(a => a.Amount);
        var revenueMoi = thisMonth.Where(a => a.Kind == CashKinds.Moi).Sum(a => a.Amount);

        var quotes = await context.Boqs.AsNoTracking()
            .Where(b => b.Status == BoqStatus.Approved && b.ApprovedAt >= monthStart)
            .Select(b => b.GrandTotal).ToListAsync(ct);

        // ----- Still owed: bills the client approved that are not fully paid yet -----
        var owed = (await nonJobs.Where(j => j.Status == "Closed")
                .Select(j => new { j.AmountPayable, Paid = j.Payments.Sum(p => p.Amount) }).ToListAsync(ct))
            .Select(j => j.AmountPayable - j.Paid)
            .Concat((await moiJobs.Where(j => j.Status == "Closed")
                .Select(j => new { j.AmountPayable, Paid = j.Payments.Sum(p => p.Amount) }).ToListAsync(ct))
                .Select(j => j.AmountPayable - j.Paid))
            .Where(balance => balance > 0).ToList();

        // ----- Cash: what people still hold, what the accountant received today, and the missing/extra this month -----
        var holdNon = await context.NonMoiJobPayments.AsNoTracking().Where(p => p.Method == "Cash" && p.CashHandoverId == null)
            .Select(p => new { Holder = p.CollectedById ?? p.RecordedById, p.Amount }).ToListAsync(ct);
        var holdMoi = await context.MoiJobPayments.AsNoTracking().Where(p => p.Method == "Cash" && p.CashHandoverId == null)
            .Select(p => new { Holder = p.CollectedById ?? p.RecordedById, p.Amount }).ToListAsync(ct);
        var handoversToday = await context.CashHandovers.AsNoTracking()
            .Where(h => h.CreatedAt >= dayStart && h.CreatedAt < dayEnd).Select(h => h.ReceivedAmount).ToListAsync(ct);
        var differenceMonth = await context.CashHandovers.AsNoTracking()
            .Where(h => h.CreatedAt >= monthStart).Select(h => h.ReceivedAmount - h.ExpectedAmount).ToListAsync(ct);
        var cash = new DashboardCashDto(
            holdNon.Sum(x => x.Amount), holdNon.Select(x => x.Holder).Distinct().Count(),
            holdMoi.Sum(x => x.Amount), holdMoi.Select(x => x.Holder).Distinct().Count(),
            handoversToday.Sum(), handoversToday.Count, differenceMonth.Sum());

        // ----- Open invoices: sent and not yet approved, or sent back by the client (MOI and Non-MOI) -----
        var open = (await nonJobs.Where(j => j.Status == "Sent" || j.Status == "ChangesRequested")
                .Select(j => new DashboardInvoiceDto(j.Id, CashKinds.NonMoi, j.JobNumber, j.CustomerName, j.AmountPayable, j.Status,
                    j.EmailSentAt ?? j.WhatsAppSentAt, j.ClientMessage)).ToListAsync(ct))
            .Concat(await moiJobs.Where(j => j.Status == "Sent" || j.Status == "ChangesRequested")
                .Select(j => new DashboardInvoiceDto(j.Id, CashKinds.Moi, j.JobNumber, j.CustomerName, j.AmountPayable, j.Status,
                    j.EmailSentAt ?? j.WhatsAppSentAt, j.ClientMessage)).ToListAsync(ct))
            .OrderBy(i => i.SentAt).ToList();

        // ----- Today's jobs (by visit time), both kinds -----
        var today = (await nonJobs
                .Where(j => (j.ScheduledAt >= dayStart && j.ScheduledAt < dayEnd) || (j.CompletedAt >= dayStart && j.CompletedAt < dayEnd))
                .Select(j => new DashboardJobDto(j.Id, CashKinds.NonMoi, j.JobNumber, j.CustomerName, j.Location, j.ScheduledAt,
                    j.AssignedToId, j.AssignedToName, j.Status, j.CompletedAt, j.AmountPayable))
                .ToListAsync(ct))
            .Concat(await moiJobs
                .Where(j => (j.ScheduledAt >= dayStart && j.ScheduledAt < dayEnd) || (j.CompletedAt >= dayStart && j.CompletedAt < dayEnd))
                .Select(j => new DashboardJobDto(j.Id, CashKinds.Moi, j.JobNumber, j.CustomerName, j.Location, j.ScheduledAt,
                    j.AssignedToId, j.AssignedToName, j.Status, j.CompletedAt, j.AmountPayable))
                .ToListAsync(ct))
            .OrderBy(j => j.ScheduledAt).ToList();
        var overdue = await nonJobs.CountAsync(j => j.Status == "Assigned" && j.ScheduledAt < dayStart, ct)
            + await moiJobs.CountAsync(j => j.Status == "Assigned" && j.ScheduledAt < dayStart, ct);

        // ----- Technicians: what each one is doing now, across both kinds of job -----
        var techs = await NonMoiJobRules.Technicians(context)
            .OrderBy(s => s.FullName)
            .Select(s => new { Id = s.AdminUserId!.Value, s.FullName })
            .ToListAsync(ct);
        var techIds = techs.Select(t => t.Id).ToList();
        var assigned = (await nonJobs.Where(j => techIds.Contains(j.AssignedToId) && j.Status == "Assigned")
                .Select(j => new { j.AssignedToId, j.JobNumber, j.Location, j.ScheduledAt }).ToListAsync(ct))
            .Select(j => (j.AssignedToId, Kind: CashKinds.NonMoi, j.JobNumber, j.Location, j.ScheduledAt))
            .Concat((await moiJobs.Where(j => techIds.Contains(j.AssignedToId) && j.Status == "Assigned")
                .Select(j => new { j.AssignedToId, j.JobNumber, j.Location, j.ScheduledAt }).ToListAsync(ct))
                .Select(j => (j.AssignedToId, Kind: CashKinds.Moi, j.JobNumber, j.Location, j.ScheduledAt)))
            .ToList();

        var technicians = techs.Select(t =>
        {
            var mine = assigned.Where(a => a.AssignedToId == t.Id).OrderBy(a => a.ScheduledAt).ToList();
            var jobsToday = today.Count(j => j.TechnicianId == t.Id);
            var doneToday = today.Count(j => j.TechnicianId == t.Id && j.Status != "Assigned");
            var late = mine.FirstOrDefault(a => a.ScheduledAt < dayStart);
            var now0 = mine.LastOrDefault(a => a.ScheduledAt >= dayStart && a.ScheduledAt <= now);
            var next = mine.FirstOrDefault(a => a.ScheduledAt > now && a.ScheduledAt < dayEnd);
            var (state, pick) = late.JobNumber is not null ? ("Overdue", late)
                : now0.JobNumber is not null ? ("On job", now0)
                : next.JobNumber is not null ? ("Next job", next)
                : ("Free", default);
            return new DashboardTechnicianDto(t.Id, t.FullName, state, pick.Kind, pick.JobNumber, pick.Location, pick.ScheduledAt,
                jobsToday, doneToday);
        }).ToList();

        // ----- Unread mail, only mailboxes still on the list -----
        var boxes = mail.Value.Accounts;
        var keys = boxes.Select(b => b.Key).ToList();
        var unread = await context.MailMessages.AsNoTracking()
            .Where(m => keys.Contains(m.AccountKey) && m.Folder == "Inbox" && !m.IsRead)
            .GroupBy(m => m.AccountKey).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var mailboxes = boxes
            .Select(b => new DashboardMailboxDto(b.Key, b.Address, unread.FirstOrDefault(u => u.Key == b.Key)?.N ?? 0))
            .Where(b => b.Unread > 0).OrderByDescending(b => b.Unread).ToList();

        // ----- Waiting for a decision -----
        var approvals = new DashboardApprovalsDto(
            await context.Boqs.CountAsync(b => b.Status == BoqStatus.Submitted, ct),
            await context.Drawings.CountAsync(d => d.Status == DrawingStatus.Submitted, ct),
            await context.ClientRegistrations.CountAsync(r => r.Status == ClientRegistrationStatus.Pending, ct),
            await nonJobs.CountAsync(j => j.Status == "Completed", ct),
            await moiJobs.CountAsync(j => j.Status == "Completed", ct));

        return ApiResult<DashboardDto>.Success(new DashboardDto(
            now, revenue, revenueLast, thisMonth.Count, thisMonth.Where(a => a.Method == "Cash").Sum(a => a.Amount),
            thisMonth.Where(a => a.Method == "Online").Sum(a => a.Amount), quotes.Sum(), quotes.Count,
            revenueNon, revenueMoi, owed.Sum(), owed.Count, cash,
            open.Sum(o => o.AmountPayable), open, today, overdue, technicians,
            mailboxes.Sum(m => m.Unread), mailboxes, approvals));
    }
}
