using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.ClientRegistrations.Commands.RejectClientRegistration;

public sealed record RejectClientRegistrationCommand : IRequest<ApiResult<ClientRegistrationDto>>
{
    /// <summary>Set from the route by the endpoint.</summary>
    public Guid Id { get; init; }

    public string? Reason { get; init; }
}

public sealed class RejectClientRegistrationCommandValidator : AbstractValidator<RejectClientRegistrationCommand>
{
    public RejectClientRegistrationCommandValidator()
    {
        // Same bar as rejecting a quotation: a few real words, so the next
        // admin to open it knows what was wrong without asking around.
        RuleFor(x => x.Reason).Cascade(CascadeMode.Stop)
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 5)
            .WithMessage("Say why this registration is being rejected — at least a few words.")
            .MaximumLength(1000).WithMessage("Reason must be 1000 characters or fewer.");
    }
}

public sealed class RejectClientRegistrationCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<RejectClientRegistrationCommand, ApiResult<ClientRegistrationDto>>
{
    public async Task<ApiResult<ClientRegistrationDto>> Handle(
        RejectClientRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var registration = await context.ClientRegistrations
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (registration is null)
            return ApiResult<ClientRegistrationDto>.Failure("Registration not found.");

        // Only something still waiting can be turned down. An accepted client
        // already has a working login (and maybe a DIA record); undoing that is
        // a deliberate account change, not a rejection.
        if (registration.Status != ClientRegistrationStatus.Pending)
            return ApiResult<ClientRegistrationDto>.Failure(
                registration.Status == ClientRegistrationStatus.Approved
                    ? "This registration has already been accepted."
                    : "This registration has already been rejected.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        registration.Status = ClientRegistrationStatus.Rejected;
        registration.RejectionReason = request.Reason!.Trim();
        registration.ReviewedById = actor.UserId;
        registration.ReviewedByName = actor.DisplayName;
        registration.ReviewedAt = now;
        registration.UpdatedAt = now;

        // The account was never switched on, and stays off — login tells the
        // client their registration was not approved (see LoginCommandHandler).
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<ClientRegistrationDto>.Success(ClientRegistrationMappings.ToDto(registration));
    }
}
