using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.UpdateBoq;

public sealed record UpdateBoqCommand : IRequest<ApiResult<BoqDto>>, IBoqWrite
{
    /// <summary>Set from the route by the endpoint, so a mismatched body id
    /// cannot redirect the write at another BOQ.</summary>
    public Guid Id { get; init; }

    public string? ProjectName { get; init; }
    public string? SiteLocation { get; init; }
    public string? ClientName { get; init; }
    public string? ContactNumber { get; init; }
    public DateOnly? IssueDate { get; init; }
    public string? Notes { get; init; }

    /// <summary>A lump sum off the finished quotation, in QAR.</summary>
    public decimal SpecialDiscount { get; init; }
    public IReadOnlyList<BoqSectionInput> Sections { get; init; } = [];

    /// <summary>Why an already-approved quotation is being changed. Required
    /// only when it actually is one — nothing on the request itself can say
    /// that; see UpdateBoqCommandHandler, which is where amending is
    /// actually known, for the check.</summary>
    public string? AmendmentNote { get; init; }
}

public sealed class UpdateBoqCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateBoqCommand, ApiResult<BoqDto>>
{
    public async Task<ApiResult<BoqDto>> Handle(
        UpdateBoqCommand request,
        CancellationToken cancellationToken)
    {
        // Sections only — see BoqWriter for why the lines must stay untracked.
        var boq = await context.Boqs
            .Include(x => x.Sections)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null)
            return ApiResult<BoqDto>.Failure("BOQ not found.");

        // Only an approval closes a quotation to edits. Up to that point — draft,
        // waiting in the queue, or come back rejected — whoever is building it
        // may keep correcting it, as often as the job needs.
        //
        // The super administrator and anyone holding BoqAmend are the exception
        // to the close. Somebody has to be able to correct a mistake on a
        // document that has already been signed off; otherwise the only route
        // is to delete it and rebuild it, which loses the number, the trail and
        // the approval together. BoqAmend lets an admin extend that ability to
        // a specific account instead of it being the root account alone, and
        // the amendment is recorded below either way.
        var amending = !BoqWorkflow.IsEditable(boq.Status);

        if (amending && !actor.IsSuperAdmin && !actor.Has(Permissions.BoqAmend))
            return ApiResult<BoqDto>.Failure(
                "An approved quotation can only be edited by the super administrator or someone granted that permission.");

        // An amendment needs a reason on the record — the same accountability
        // a rejection already requires, and for the same reason: touching
        // something everyone already signed off on without saying why is
        // exactly what this whole rule exists to prevent. Checked here, not
        // in the validator — only this handler knows the quotation is
        // actually being amended, since that depends on its current status.
        var amendmentNote = request.AmendmentNote?.Trim();
        if (amending && (string.IsNullOrWhiteSpace(amendmentNote) || amendmentNote.Length < 5))
            return ApiResult<BoqDto>.Failure("Say why this approved quotation is being changed.");

        // What the quotation held before this save, for the Revised/Amended
        // note below — read separately, AsNoTracking, so it never joins the
        // tracked graph BoqWriter and the section swap further down depend on
        // staying exactly Sections-only. Only worth the query when it will
        // actually be used.
        var wasRejected = boq.Status == BoqStatus.Rejected;
        var previousItems = (wasRejected || amending)
            ? await context.BoqLines
                .AsNoTracking()
                .Where(l => l.Section.BoqId == boq.Id)
                .Select(l => new LineIdentity(l.CameraId, l.ItemName))
                .ToListAsync(cancellationToken)
            : null;

        // The number and who prepared it are set once. Neither is rewritten here:
        // the reference may already be circulating, and authorship is a fact.
        var (error, sections) = await BoqWriter.BuildAsync(
            boq, request, context, timeProvider, cancellationToken);

        if (error is not null)
            return ApiResult<BoqDto>.Failure(error);

        // Old rows out, new rows in — both through the DbSet, never by mutating
        // boq.Sections. Clearing a tracked collection left EF reconciling
        // half-orphaned children against rows the cascade had already removed,
        // which surfaced as "expected to affect 1 row, actually affected 0".
        // The lines are deliberately not loaded; the database cascade takes them
        // with their section.
        context.BoqSections.RemoveRange(boq.Sections);
        context.BoqSections.AddRange(sections);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Appended, never in place of the decision: the approval stands, and so
        // does the fact that the document changed after it. A reader can see
        // both and judge for themselves.
        if (amending)
        {
            // The admin's own reason leads; what actually moved item-wise
            // follows in parentheses when there is any — the same summary
            // the Revised trail below uses, so "what changed" reads the same
            // way regardless of which kind of edit caused it.
            var itemChanges = SummarizeItemChanges(previousItems!, sections);
            var combined = itemChanges is null ? amendmentNote! : $"{amendmentNote} ({itemChanges})";

            const int reasonMaxLength = 1000;
            if (combined.Length > reasonMaxLength)
                combined = combined[..(reasonMaxLength - 1)] + "…";

            BoqWorkflow.Record(context, boq, BoqApprovalAction.Amended, actor, now, combined);
        }
        // Every save made while reworking a rejection is its own step, not
        // folded into the eventual re-submission — an approver (or the super
        // administrator) can see exactly how many passes it took, when each
        // landed, and — via the note — which items actually moved, rather than
        // a bare timestamp that says a save happened.
        else if (wasRejected)
            BoqWorkflow.Record(
                context, boq, BoqApprovalAction.Revised, actor, now,
                SummarizeItemChanges(previousItems!, sections));

        boq.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        // Re-read rather than mapping the entity: its Sections navigation still
        // holds the rows just deleted.
        var saved = await context.Boqs
            .AsNoTracking()
            .Include(x => x.Sections)
            .ThenInclude(x => x.Lines)
            .Include(x => x.ApprovalEvents)
            .FirstAsync(x => x.Id == boq.Id, cancellationToken);

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(saved));
    }

    /// <summary>What a line was, for comparing before and after a rework — just
    /// enough to tell whether it is the same catalogue item, and what to call it
    /// if not.</summary>
    private sealed record LineIdentity(Guid? CameraId, string ItemName);

    /// <summary>
    /// What actually moved during a rework, in one line for the trail.
    ///
    /// Matched by CameraId — the one thing that still identifies "the same
    /// item" even after a rate or quantity edit — never by name, since two
    /// different items can share one. A line with no CameraId is a retired
    /// item carried forward unchanged; it can be neither added nor removed by
    /// this save; either both snapshots list a retired item deleted, or
    /// carrying it forward wasn't a choice this save made. Capped so a
    /// quotation with dozens of lines added at once still fits the column.
    /// </summary>
    private static string? SummarizeItemChanges(IReadOnlyList<LineIdentity> before, List<BoqSection> after)
    {
        const int MaxNamed = 6;

        var beforeIds = before
            .Where(l => l.CameraId.HasValue)
            .Select(l => l.CameraId!.Value)
            .ToHashSet();

        var afterLines = after.SelectMany(s => s.Lines).ToList();
        var afterIds = afterLines
            .Where(l => l.CameraId.HasValue)
            .Select(l => l.CameraId!.Value)
            .ToHashSet();

        var added = afterLines
            .Where(l => l.CameraId.HasValue && !beforeIds.Contains(l.CameraId.Value))
            .Select(l => l.ItemName)
            .Distinct()
            .ToList();

        var removed = before
            .Where(l => l.CameraId.HasValue && !afterIds.Contains(l.CameraId!.Value))
            .Select(l => l.ItemName)
            .Distinct()
            .ToList();

        if (added.Count == 0 && removed.Count == 0)
            return null;

        var parts = new List<string>();
        if (added.Count > 0) parts.Add($"Added {Describe(added, MaxNamed)}");
        if (removed.Count > 0) parts.Add($"Removed {Describe(removed, MaxNamed)}");
        var note = string.Join(". ", parts);

        // The column is 1000 chars — capping the item count above keeps this
        // far under that in the ordinary case, but a handful of catalogue
        // names near their own length limit could still add up. A truncated
        // note is still useful; a save that fails because the note was one
        // character too long is not.
        const int reasonMaxLength = 1000;
        return note.Length <= reasonMaxLength ? note : note[..(reasonMaxLength - 1)] + "…";
    }

    private static string Describe(List<string> names, int max) =>
        names.Count <= max
            ? string.Join(", ", names)
            : string.Join(", ", names.Take(max)) + $", and {names.Count - max} more";
}
