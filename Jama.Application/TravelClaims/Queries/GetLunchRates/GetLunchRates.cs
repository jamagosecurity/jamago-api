using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Queries.GetLunchRates;

/// <summary>Today's site-lunch rate per person and every rate that was ever set.</summary>
public sealed record GetLunchRatesQuery : IRequest<ApiResult<LunchRatesDto>>;

public sealed class GetLunchRatesQueryHandler(IApplicationDbContext context, TimeProvider clock) : IRequestHandler<GetLunchRatesQuery, ApiResult<LunchRatesDto>>
{
    public async Task<ApiResult<LunchRatesDto>> Handle(GetLunchRatesQuery r, CancellationToken ct)
    {
        var current = await TravelClaimRules.LunchRateAsync(context, TravelClaimRules.Today(clock), ct);
        IReadOnlyList<LunchRateDto> history = await context.LunchRates.AsNoTracking()
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt)
            .Select(x => new LunchRateDto(x.AmountPerPerson, x.EffectiveFrom, x.SetByName, x.CreatedAt)).ToListAsync(ct);
        return ApiResult<LunchRatesDto>.Success(new(current, history));
    }
}
