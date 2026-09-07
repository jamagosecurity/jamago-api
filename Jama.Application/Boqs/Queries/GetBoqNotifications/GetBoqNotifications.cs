using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Queries.GetBoqNotifications;

/// <summary>
/// One decision, as the people it concerns need to see it: what was decided, on
/// whose quotation, by whom, why — and how many times this document had been
/// sent back before it got here.
/// </summary>
public sealed record BoqNotificationDto(
    Guid Id,
    Guid BoqId,
    string BoqNumber,
    string ProjectName,
    string? ClientName,
    decimal Amount,
    BoqApprovalAction Action,
    /// <summary>Who decided.</summary>
    string? ActorName,
    /// <summary>Whose quotation it was — the person whose work was answered.</summary>
    string? PreparedByName,
    string? Reason,
    DateTime At,
    /// <summary>Decided since this account last looked.</summary>
    bool IsUnread,
    /// <summary>Which submission this decision answered. An approval with
    /// Attempt 3 was approved on the third time of asking.</summary>
    int Attempt,
    /// <summary>How many times this quotation has been sent back, all told.</summary>
    int RejectionCount,
    /// <summary>Whether this account built the quotation, as opposed to reading
    /// about somebody else's. Changes how the line is worded, nothing more.</summary>
    bool IsMine);

public sealed record BoqNotificationsDto(
    IReadOnlyList<BoqNotificationDto> Items,
    int UnreadCount);

/// <summary>
/// Approvals and rejections the signed-in account should be told about, newest
/// first.
///
/// Who sees what:
///   • an administrator sees every decision, on anybody's quotation;
///   • whoever BUILT a quotation sees decisions on it — being told your work was
///     approved, or sent back and why, is the whole point of the workflow;
///   • whoever MADE a decision sees it, as a record of what they answered.
///
/// Read from the approval trail rather than a notifications table of its own. A
/// second copy of the same facts is a second thing to keep in step, and it would
/// be the copy that goes wrong — the trail is written in the same transaction as
/// the decision.
/// </summary>
public sealed record GetBoqNotificationsQuery(int Take = 20)
    : IRequest<ApiResult<BoqNotificationsDto>>;

public sealed class GetBoqNotificationsQueryHandler(
    IApplicationDbContext context,
    ICurrentUser actor)
    : IRequestHandler<GetBoqNotificationsQuery, ApiResult<BoqNotificationsDto>>
{
    /// <summary>Enough to see what has happened lately without turning the panel
    /// into a second history screen — that is what the trail on the quotation is
    /// for.</summary>
    private const int MaxTake = 50;

    public async Task<ApiResult<BoqNotificationsDto>> Handle(
        GetBoqNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var me = actor.UserId;

        var seenAt = await context.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == me)
            .Select(u => u.NotificationsSeenAt)
            .FirstOrDefaultAsync(cancellationToken);

        var decisions = context.BoqApprovalEvents
            .AsNoTracking()
            .Where(e => e.Action == BoqApprovalAction.Approved
                || e.Action == BoqApprovalAction.Rejected);

        // An administrator is told about everything; everybody else about their
        // own work and their own decisions. Applied here rather than in the
        // endpoint because it is not "may you call this" — it is "which of these
        // are yours", and the answer differs per row.
        if (actor.Role != Roles.Admin)
            decisions = decisions.Where(e => e.Boq.PreparedById == me || e.ActorId == me);

        // Counted over everything the account may see, not just the page: a badge
        // that stops at the page size says twenty when there are two hundred.
        var unreadCount = seenAt.HasValue
            ? await decisions.CountAsync(e => e.CreatedAt > seenAt.Value, cancellationToken)
            : await decisions.CountAsync(cancellationToken);

        var take = Math.Clamp(request.Take, 1, MaxTake);

        var page = await decisions
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new
            {
                e.Id,
                e.BoqId,
                e.Boq.BoqNumber,
                e.Boq.ProjectName,
                e.Boq.ClientName,
                Amount = e.Boq.GrandTotal,
                e.Action,
                e.ActorName,
                e.Boq.PreparedByName,
                e.Boq.PreparedById,
                e.Reason,
                e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // How often each of these documents went round: one grouped read for the
        // whole page rather than two counts per row.
        var boqIds = page.Select(x => x.BoqId).Distinct().ToList();

        var tallies = await context.BoqApprovalEvents
            .AsNoTracking()
            .Where(e => boqIds.Contains(e.BoqId))
            .GroupBy(e => e.BoqId)
            .Select(g => new
            {
                BoqId = g.Key,
                Rejections = g.Count(e => e.Action == BoqApprovalAction.Rejected),
                Submissions = g.Count(e => e.Action == BoqApprovalAction.Submitted),
            })
            .ToDictionaryAsync(x => x.BoqId, cancellationToken);

        var items = page
            .Select(x =>
            {
                tallies.TryGetValue(x.BoqId, out var tally);

                return new BoqNotificationDto(
                    x.Id,
                    x.BoqId,
                    x.BoqNumber,
                    x.ProjectName,
                    x.ClientName,
                    x.Amount,
                    x.Action,
                    x.ActorName,
                    x.PreparedByName,
                    x.Reason,
                    x.CreatedAt,
                    seenAt == null || x.CreatedAt > seenAt.Value,
                    // A submission the decision answered. At least one: a decision
                    // cannot exist without the submission that asked for it.
                    Math.Max(tally?.Submissions ?? 1, 1),
                    tally?.Rejections ?? 0,
                    x.PreparedById == me);
            })
            .ToList();

        return ApiResult<BoqNotificationsDto>.Success(
            new BoqNotificationsDto(items, unreadCount));
    }
}
