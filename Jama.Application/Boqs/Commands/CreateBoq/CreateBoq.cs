using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;

namespace Jama.Application.Boqs.Commands.CreateBoq;

public sealed record CreateBoqCommand : IRequest<ApiResult<BoqDto>>, IBoqWrite
{
    public string? ProjectName { get; init; }
    public string? SiteLocation { get; init; }
    public string? ClientName { get; init; }
    public string? ContactNumber { get; init; }
    public DateOnly? IssueDate { get; init; }
    public BoqStatus Status { get; init; } = BoqStatus.Draft;
    public string? Notes { get; init; }
    public IReadOnlyList<BoqSectionInput> Sections { get; init; } = [];
}

public sealed class CreateBoqCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<CreateBoqCommand, ApiResult<BoqDto>>
{
    public async Task<ApiResult<BoqDto>> Handle(
        CreateBoqCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var boq = new Boq
        {
            Id = Guid.CreateVersion7(),
            BoqNumber = await BoqNumbers.NextAsync(context, cancellationToken),
            // Taken from the token, never the request: who prepared a bill is not
            // something the caller gets to assert.
            PreparedById = actor.UserId,
            PreparedByName = actor.DisplayName,
            CreatedAt = now,
        };

        var (error, sections) = await BoqWriter.BuildAsync(
            boq, request, context, timeProvider, actor.Has(Permissions.BoqPrice), cancellationToken);

        if (error is not null)
            return ApiResult<BoqDto>.Failure(error);

        // Nothing is tracked yet, so the graph can be attached wholesale.
        foreach (var section in sections)
            boq.Sections.Add(section);

        context.Boqs.Add(boq);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(boq));
    }
}
