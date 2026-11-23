using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.TravelClaims.Queries.GetLunchRates;
using Jama.Domain.Entities;
using MediatR;

namespace Jama.Application.TravelClaims.Commands.SetLunchRate;

/// <summary>A new site-lunch rate per person from a day on. Claims already made keep the rate they were made with.</summary>
public sealed record SetLunchRateCommand(decimal AmountPerPerson, DateOnly? EffectiveFrom) : IRequest<ApiResult<LunchRatesDto>>;

public sealed class SetLunchRateCommandValidator : AbstractValidator<SetLunchRateCommand>
{
    public SetLunchRateCommandValidator()
    {
        RuleFor(x => x.AmountPerPerson).GreaterThan(0).LessThanOrEqualTo(1000)
            .Must(a => decimal.Round(a, 2) == a).WithMessage("Enter the lunch amount per person, like 12 or 15.50.");
    }
}

public sealed class SetLunchRateCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<SetLunchRateCommand, ApiResult<LunchRatesDto>>
{
    public async Task<ApiResult<LunchRatesDto>> Handle(SetLunchRateCommand r, CancellationToken ct)
    {
        var today = TravelClaimRules.Today(clock);
        var from = r.EffectiveFrom ?? today;
        if (from < today.AddDays(-TravelClaimRules.MaxTripAgeDays)) return ApiResult<LunchRatesDto>.Failure("The start day is too far back — check it.");
        context.LunchRates.Add(new LunchRate
        {
            Id = Guid.CreateVersion7(), AmountPerPerson = r.AmountPerPerson, EffectiveFrom = from,
            SetById = actor.UserId, SetByName = actor.DisplayName ?? "Accountant", CreatedAt = clock.GetUtcNow().UtcDateTime,
        });
        await context.SaveChangesAsync(ct);
        return await new GetLunchRatesQueryHandler(context, clock).Handle(new GetLunchRatesQuery(), ct);
    }
}
