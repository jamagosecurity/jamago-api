using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Queries.GetTravelPeople;

/// <summary>Who can be picked as "came with me": every other active staff member and technician.</summary>
public sealed record GetTravelPeopleQuery : IRequest<ApiResult<IReadOnlyList<TravelPersonOption>>>;

public sealed class GetTravelPeopleQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetTravelPeopleQuery, ApiResult<IReadOnlyList<TravelPersonOption>>>
{
    public async Task<ApiResult<IReadOnlyList<TravelPersonOption>>> Handle(GetTravelPeopleQuery r, CancellationToken ct)
    {
        if (!TravelClaimRules.IsStaff(actor)) return ApiResult<IReadOnlyList<TravelPersonOption>>.Failure("Only staff can claim Uber bills.");
        IReadOnlyList<TravelPersonOption> people = await context.AdminUsers.AsNoTracking()
            .Where(u => u.IsActive && u.Role != Roles.Client && u.Id != actor.UserId)
            .OrderBy(u => u.FullName).Select(u => new TravelPersonOption(u.Id, u.FullName)).ToListAsync(ct);
        return ApiResult<IReadOnlyList<TravelPersonOption>>.Success(people);
    }
}
