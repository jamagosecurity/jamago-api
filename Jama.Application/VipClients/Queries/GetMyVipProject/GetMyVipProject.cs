using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.VipClients.Queries.GetMyVipProject;

/// <summary>The signed-in client's own project. Resolved from the token, never an id.</summary>
public sealed record GetMyVipProjectQuery : IRequest<ApiResult<VipClientDetailDto>>;

public sealed class GetMyVipProjectQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyVipProjectQuery, ApiResult<VipClientDetailDto>>
{
    public async Task<ApiResult<VipClientDetailDto>> Handle(
        GetMyVipProjectQuery request,
        CancellationToken cancellationToken)
    {
        // Scoped by the signed-in account, so a client can only ever resolve
        // their own project — there is no id to tamper with.
        var entity = await VipClientMappings.LoadWithFoldersAsync(
            context, x => x.AdminUserId == currentUser.UserId && !x.IsArchived, cancellationToken);

        if (entity is null)
            return ApiResult<VipClientDetailDto>.Failure("No project is linked to this account.");

        var names = await VipClientMappings.UploaderNamesAsync(context, entity, cancellationToken);
        return ApiResult<VipClientDetailDto>.Success(VipClientMappings.ToDetail(entity, names));
    }
}
