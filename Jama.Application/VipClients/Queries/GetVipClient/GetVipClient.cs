using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.VipClients.Queries.GetVipClient;

public sealed record GetVipClientQuery(Guid Id) : IRequest<ApiResult<VipClientDetailDto>>;

public sealed class GetVipClientQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetVipClientQuery, ApiResult<VipClientDetailDto>>
{
    public async Task<ApiResult<VipClientDetailDto>> Handle(
        GetVipClientQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await VipClientMappings.LoadWithFoldersAsync(
            context, x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return ApiResult<VipClientDetailDto>.Failure("VIP client not found.");

        var names = await VipClientMappings.UploaderNamesAsync(context, entity, cancellationToken);
        return ApiResult<VipClientDetailDto>.Success(VipClientMappings.ToDetail(entity, names));
    }
}
