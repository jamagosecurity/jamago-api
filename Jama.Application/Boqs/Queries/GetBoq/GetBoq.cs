using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Queries.GetBoq;

public sealed record GetBoqQuery(Guid Id) : IRequest<ApiResult<BoqDto>>;

public sealed class GetBoqQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetBoqQuery, ApiResult<BoqDto>>
{
    public async Task<ApiResult<BoqDto>> Handle(
        GetBoqQuery request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .AsNoTracking()
            .Include(x => x.Sections)
            .ThenInclude(x => x.Lines)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        // Not found rather than forbidden: probing another account's draft id
        // must never confirm that it exists.
        if (boq is null || !BoqVisibility.CanSee(boq.Status, boq.PreparedById, actor))
            return ApiResult<BoqDto>.Failure("BOQ not found.");

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(boq));
    }
}
