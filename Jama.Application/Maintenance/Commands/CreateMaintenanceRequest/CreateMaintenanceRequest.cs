using FluentValidation;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.CreateMaintenanceRequest;

/// <summary>
/// Files a maintenance ticket. Open to any authenticated account — a client
/// reporting their own issues, or staff logging a phone-in report on someone
/// else's behalf — see the endpoint, which requires only authentication,
/// not a specific role or grant.
/// </summary>
public sealed record CreateMaintenanceRequestCommand : IRequest<ApiResult<MaintenanceRequestDto>>
{
    public IReadOnlyList<MaintenanceLineInput> Lines { get; init; } = [];
    public string? Description { get; init; }
    public string? SiteLocation { get; init; }
    public string? ClientName { get; init; }
    public string? ClientEmail { get; init; }
    public string? ContactNumber { get; init; }

    /// <summary>Lump sum off the total, in QAR. maintenance.manage only —
    /// anyone else's is dropped, see MaintenanceLines.ApplyTotals.</summary>
    public decimal? Discount { get; init; }
}

public sealed class CreateMaintenanceRequestCommandValidator : AbstractValidator<CreateMaintenanceRequestCommand>
{
    public CreateMaintenanceRequestCommandValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Add at least one issue.")
            .Must(lines => lines.Count <= MaintenanceLines.MaxLines)
            .WithMessage($"A ticket can hold at most {MaintenanceLines.MaxLines} issues.");
        RuleForEach(x => x.Lines).SetValidator(new MaintenanceLineInputValidator());
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SiteLocation).MaximumLength(400);
        RuleFor(x => x.ClientName).MaximumLength(200);
        RuleFor(x => x.ClientEmail).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Client email is required.")
            .MaximumLength(256)
            .EmailAddress().WithMessage("Enter a valid client email.");
        RuleFor(x => x.ContactNumber).MaximumLength(50);
        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0m).WithMessage("Discount cannot be negative.")
            .LessThanOrEqualTo(MaintenanceLines.DiscountMax)
            .WithMessage($"Discount must be {MaintenanceLines.DiscountMax:N2} or less.");
    }
}

public sealed class CreateMaintenanceRequestCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<CreateMaintenanceRequestCommand, ApiResult<MaintenanceRequestDto>>
{
    public async Task<ApiResult<MaintenanceRequestDto>> Handle(
        CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        // The permission check runs here, server-side: a client's request can
        // carry a UnitFee or Discount only by being edited by hand, and both are
        // still ignored.
        var canSetFee = currentUser.Has(Permissions.MaintenanceManage);
        var catalogue = await context.MaintenanceCatalogueItems.AsNoTracking().ToListAsync(cancellationToken);
        var lines = MaintenanceLines.Build(request.Lines, canSetFee, catalogue);

        var ticket = new MaintenanceRequest
        {
            Id = Guid.CreateVersion7(),
            Lines = lines,
            Description = Clean(request.Description),
            SiteLocation = Clean(request.SiteLocation),
            ClientName = Clean(request.ClientName),
            ClientEmail = request.ClientEmail?.Trim() ?? string.Empty,
            ContactNumber = Clean(request.ContactNumber),
            Status = MaintenanceStatus.Open,
            ReportedById = currentUser.UserId,
            ReportedByName = currentUser.DisplayName,
        };

        if (MaintenanceLines.ApplyTotals(ticket, lines, request.Discount, canSetFee) is { } error)
            return ApiResult<MaintenanceRequestDto>.Failure(error);

        ticket.RequestNumber = await MaintenanceNumbers.NextAsync(context, cancellationToken);
        context.MaintenanceRequests.Add(ticket);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceRequestDto>.Success(MaintenanceMappings.ToDto(ticket));
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
