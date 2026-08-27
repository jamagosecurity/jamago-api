using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Queries.GetBoq;

public sealed record GetBoqQuery(Guid Id) : IRequest<ApiResult<BoqDto>>;

public sealed class GetBoqQueryHandler(IApplicationDbContext context)
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
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        return boq is null
            ? ApiResult<BoqDto>.Failure("BOQ not found.")
            : ApiResult<BoqDto>.Success(BoqMappings.ToDto(boq));
    }
}
