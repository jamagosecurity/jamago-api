using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Dia;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.ClientRegistrations.Commands.ApproveClientRegistration;

/// <summary>
/// Accepting a registration: classify it MOI or Non-MOI, allot its teams, and
/// switch the login on. An MOI client's DIA inspection record is opened in the
/// same save, so there is never an accepted MOI client with no DIA behind it.
/// </summary>
public sealed record ApproveClientRegistrationCommand : IRequest<ApiResult<ClientRegistrationDto>>
{
    /// <summary>Set from the route by the endpoint.</summary>
    public Guid Id { get; init; }

    public ClientCategory? Category { get; init; }
    public IReadOnlyList<string> Teams { get; init; } = [];

    /// <summary>MOI only — the DIA inspection's own reference, unique across
    /// every DIA record.</summary>
    public string? DiaNumber { get; init; }

    /// <summary>MOI only, optional. The DIA record's client number and location
    /// default to the mobile and site location the client registered with;
    /// these let the admin record something else (a reference number instead of
    /// a phone, a tidier address) without editing the DIA afterwards.</summary>
    public string? DiaClientNumber { get; init; }
    public string? DiaClientLocation { get; init; }
}

public sealed class ApproveClientRegistrationCommandValidator : AbstractValidator<ApproveClientRegistrationCommand>
{
    public ApproveClientRegistrationCommandValidator()
    {
        RuleFor(x => x.Category)
            .NotNull().WithMessage("Choose MOI or Non-MOI.")
            .IsInEnum().WithMessage("Choose MOI or Non-MOI.");

        RuleFor(x => x.Teams).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Allot at least one team.")
            .Must(teams => teams.All(ClientTeams.IsValid)).WithMessage("Choose teams from the list.");

        When(x => x.Category == ClientCategory.Moi, () =>
        {
            RuleFor(x => x.DiaNumber).Cascade(CascadeMode.Stop)
                .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Enter the DIA number for this MOI client.")
                .MaximumLength(100).WithMessage("DIA number must be 100 characters or fewer.");

            RuleFor(x => x.DiaClientNumber)
                .MaximumLength(100).WithMessage("Client number must be 100 characters or fewer.");

            RuleFor(x => x.DiaClientLocation)
                .MaximumLength(300).WithMessage("Client location must be 300 characters or fewer.");
        });
    }
}

public sealed class ApproveClientRegistrationCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<ApproveClientRegistrationCommand, ApiResult<ClientRegistrationDto>>
{
    private const string DiaNumberTaken = "A DIA inspection with this DIA number already exists.";

    public async Task<ApiResult<ClientRegistrationDto>> Handle(
        ApproveClientRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var registration = await context.ClientRegistrations
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (registration is null)
            return ApiResult<ClientRegistrationDto>.Failure("Registration not found.");

        // A rejection can be reversed — admins turn people down by mistake —
        // but an acceptance is final here: it has already opened a login and,
        // for MOI, a DIA record that other people may now be working from.
        if (registration.Status == ClientRegistrationStatus.Approved)
            return ApiResult<ClientRegistrationDto>.Failure("This registration has already been accepted.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        DiaInspection? dia = null;

        if (request.Category == ClientCategory.Moi)
        {
            var diaNumber = request.DiaNumber!.Trim();
            var normalized = DiaRequestSupport.Normalize(diaNumber);

            if (await context.DiaInspections.AnyAsync(x => x.NormalizedDiaNumber == normalized, cancellationToken))
                return ApiResult<ClientRegistrationDto>.Failure(DiaNumberTaken);

            dia = new DiaInspection
            {
                Id = Guid.CreateVersion7(),
                DiaNumber = diaNumber,
                NormalizedDiaNumber = normalized,
                ClientName = registration.FullName,
                ClientNumber = OrDefault(request.DiaClientNumber, registration.Mobile),
                ClientLocation = OrDefault(request.DiaClientLocation, registration.SiteLocation),
                // Inactive, exactly like one created by hand from the DIA page:
                // activating starts the quarterly inspection clock, and that is
                // still the DIA team's call to make, not a side effect of signing
                // a client up.
                CreatedById = actor.UserId,
                CreatedAt = now,
            };

            context.DiaInspections.Add(dia);
            context.DiaInspectionHistory.Add(DiaRequestSupport.Audit(
                dia, DiaInspectionAction.Create, actor, null, DiaRequestSupport.Snapshot(dia)));
        }

        registration.Status = ClientRegistrationStatus.Approved;
        registration.Category = request.Category;
        registration.Teams = ClientTeams.Normalize(request.Teams);
        registration.DiaInspectionId = dia?.Id;
        registration.DiaInspection = dia;
        registration.ReviewedById = actor.UserId;
        registration.ReviewedByName = actor.DisplayName;
        registration.ReviewedAt = now;
        registration.RejectionReason = null;
        registration.UpdatedAt = now;

        registration.Account.IsActive = true;
        registration.Account.UpdatedAt = now;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (dia is not null)
        {
            // The pre-check races with a DIA created at the same moment; the
            // unique index is the real guard, so a violation means the same.
            return ApiResult<ClientRegistrationDto>.Failure(DiaNumberTaken);
        }

        return ApiResult<ClientRegistrationDto>.Success(ClientRegistrationMappings.ToDto(registration));
    }

    private static string OrDefault(string? preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();
}
