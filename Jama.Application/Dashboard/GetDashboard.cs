using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.NonMoiJobs;
using Jama.Application.Options;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Dashboard;

public sealed record DashboardJobDto(
    Guid Id, string JobNumber, string CustomerName, string Location, DateTime? ScheduledAt,
    Guid TechnicianId, string TechnicianName, string Status, DateTime? CompletedAt, decimal AmountPayable);

public sealed record DashboardTechnicianDto(
    Guid Id, string Name, string State, string? JobNumber, string? Location, DateTime? At,
    int JobsToday, int DoneToday);

public sealed record DashboardInvoiceDto(
    Guid Id, string JobNumber, string CustomerName, decimal AmountPayable, string Status,
    DateTime? SentAt, string? ClientMessage);

public sealed record DashboardMailboxDto(string Key, string Address, int Unread);

public sealed record DashboardApprovalsDto(int Quotations, int Drawings, int ClientRegistrations, int JobsToClose)
{
    public int Total => Quotations + Drawings + ClientRegistrations + JobsToClose;
}

public sealed record DashboardDto(
    DateTime Now,
    decimal RevenueThisMonth, decimal RevenueLastMonth, int PaymentsThisMonth, decimal CashThisMonth, decimal OnlineThisMonth,
    decimal QuotationsApprovedThisMonth, int QuotationsApprovedCount,
    decimal OpenInvoicesTotal, IReadOnlyList<DashboardInvoiceDto> OpenInvoices,
    IReadOnlyList<DashboardJobDto> TodayJobs, int OverdueJobs,
    IReadOnlyList<DashboardTechnicianDto> Technicians,
    int UnreadMail, IReadOnlyList<DashboardMailboxDto> Mailboxes,
    DashboardApprovalsDto Approvals);

/// <summary>
/// The super admin's one-page view: today's Non-MOI jobs and where each technician is, invoices still waiting
/// on the client, unread company mail, everything waiting for a decision, and this month's revenue.
/// Days and months are Doha days (UTC+3). Revenue = money actually received for Non-MOI jobs in the month (cash and online payments).
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

        var jobs = context.NonMoiJobs.AsNoTracking();

        // ----- Revenue: money received (cash collected + online transfers), by the day it came in -----
        var firstOfMonth = new DateOnly(local.Year, local.Month, 1);
        var firstOfLast = firstOfMonth.AddMonths(-1);
        var received = await context.NonMoiJobPayments.AsNoTracking().Where(p => p.ReceivedOn >= firstOfLast)
            .Select(p => new { p.ReceivedOn, p.Amount, p.Method }).ToListAsync(ct);
        var thisMonth = received.Where(a => a.ReceivedOn >= firstOfMonth).ToList();
        var revenue = thisMonth.Sum(a => a.Amount);
        var revenueLast = received.Where(a => a.ReceivedOn < firstOfMonth).Sum(a => a.Amount);

        var quotes = await context.Boqs.AsNoTracking()
            .Where(b => b.Status == BoqStatus.Approved && b.ApprovedAt >= monthStart)
            .Select(b => b.GrandTotal).ToListAsync(ct);

        // ----- Open invoices: sent and not yet approved, or sent back by the client -----
        var open = await jobs.Where(j => j.Status == "Sent" || j.Status == "ChangesRequested")
            .OrderBy(j => j.EmailSentAt)
            .Select(j => new DashboardInvoiceDto(j.Id, j.JobNumber, j.CustomerName, j.AmountPayable, j.Status,
                j.EmailSentAt, j.ClientMessage))
            .ToListAsync(ct);

        // ----- Today's jobs (by visit time), plus anything still open from an earlier day -----
        var today = await jobs
            .Where(j => (j.ScheduledAt >= dayStart && j.ScheduledAt < dayEnd)
                     || (j.CompletedAt >= dayStart && j.CompletedAt < dayEnd))
            .OrderBy(j => j.ScheduledAt)
            .Select(j => new DashboardJobDto(j.Id, j.JobNumber, j.CustomerName, j.Location, j.ScheduledAt,
                j.AssignedToId, j.AssignedToName, j.Status, j.CompletedAt, j.AmountPayable))
            .ToListAsync(ct);
        var overdue = await jobs.CountAsync(j => j.Status == "Assigned" && j.ScheduledAt < dayStart, ct);

        // ----- Technicians: what each one is doing now -----
        var techs = await NonMoiJobRules.Technicians(context)
            .OrderBy(s => s.FullName)
            .Select(s => new { Id = s.AdminUserId!.Value, s.FullName })
            .ToListAsync(ct);
        var techIds = techs.Select(t => t.Id).ToList();
        var assigned = await jobs.Where(j => techIds.Contains(j.AssignedToId) && j.Status == "Assigned")
            .Select(j => new { j.AssignedToId, j.JobNumber, j.Location, j.ScheduledAt }).ToListAsync(ct);

        var technicians = techs.Select(t =>
        {
            var mine = assigned.Where(a => a.AssignedToId == t.Id).OrderBy(a => a.ScheduledAt).ToList();
            var jobsToday = today.Count(j => j.TechnicianId == t.Id);
            var doneToday = today.Count(j => j.TechnicianId == t.Id && j.Status != "Assigned");
            var late = mine.FirstOrDefault(a => a.ScheduledAt < dayStart);
            var now0 = mine.LastOrDefault(a => a.ScheduledAt >= dayStart && a.ScheduledAt <= now);
            var next = mine.FirstOrDefault(a => a.ScheduledAt > now && a.ScheduledAt < dayEnd);
            var (state, pick) = late is not null ? ("Overdue", late)
                : now0 is not null ? ("On job", now0)
                : next is not null ? ("Next job", next)
                : ("Free", null);
            return new DashboardTechnicianDto(t.Id, t.FullName, state, pick?.JobNumber, pick?.Location, pick?.ScheduledAt,
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
            await jobs.CountAsync(j => j.Status == "Completed", ct));

        return ApiResult<DashboardDto>.Success(new DashboardDto(
            now, revenue, revenueLast, thisMonth.Count, thisMonth.Where(a => a.Method == "Cash").Sum(a => a.Amount),
            thisMonth.Where(a => a.Method == "Online").Sum(a => a.Amount), quotes.Sum(), quotes.Count,
            open.Sum(o => o.AmountPayable), open, today, overdue, technicians,
            mailboxes.Sum(m => m.Unread), mailboxes, approvals));
    }
}
