using Jama.Application.Common;
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
    public BoqStatus Status { get; init; } = BoqStatus.Draft;
    public string? Notes { get; init; }
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

        // The number and who prepared it are set once. Neither is rewritten here:
        // the reference may already be circulating, and authorship is a fact.
        var (error, sections) = await BoqWriter.BuildAsync(
            boq, request, context, timeProvider, actor.Has(Permissions.BoqPrice), cancellationToken);

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

        boq.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);

        // Re-read rather than mapping the entity: its Sections navigation still
        // holds the rows just deleted.
        var saved = await context.Boqs
            .AsNoTracking()
            .Include(x => x.Sections)
            .ThenInclude(x => x.Lines)
            .FirstAsync(x => x.Id == boq.Id, cancellationToken);

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(saved));
    }
}
