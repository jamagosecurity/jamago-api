using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.ClientRegistrations.Queries.GetPendingClientRegistrationCount;

/// <summary>Just the number waiting — for the sidebar badge, which is on every
/// admin screen and should not pull the whole list to draw one number.</summary>
public sealed record GetPendingClientRegistrationCountQuery : IRequest<ApiResult<int>>;

public sealed class GetPendingClientRegistrationCountQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPendingClientRegistrationCountQuery, ApiResult<int>>
{
    public async Task<ApiResult<int>> Handle(
        GetPendingClientRegistrationCountQuery request,
        CancellationToken cancellationToken) =>
        ApiResult<int>.Success(await context.ClientRegistrations
            .CountAsync(x => x.Status == ClientRegistrationStatus.Pending, cancellationToken));
}
