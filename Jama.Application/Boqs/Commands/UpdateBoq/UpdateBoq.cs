using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
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
        // The super administrator is the exception to the close. Somebody has to
        // be able to correct a mistake on a document that has already been signed
        // off; otherwise the only route is to delete it and rebuild it, which
        // loses the number, the trail and the approval together. It is one
        // account, not a permission an administrator can hand out, and the
        // amendment is recorded below.
        var amending = !BoqWorkflow.IsEditable(boq.Status);

        if (amending && !actor.IsSuperAdmin)
            return ApiResult<BoqDto>.Failure(
                "An approved quotation can only be edited by the super administrator.");

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
            BoqWorkflow.Record(context, boq, BoqApprovalAction.Amended, actor, now);

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
}
