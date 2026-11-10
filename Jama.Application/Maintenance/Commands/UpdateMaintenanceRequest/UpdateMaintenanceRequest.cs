using FluentValidation;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.UpdateMaintenanceRequest;

/// <summary>
/// Corrects a ticket's details and its lines — including each line's fee,
/// which only maintenance.manage may move off the catalogue price. Gated on
/// that grant at the endpoint: once filed, only staff edit a ticket, the same
/// way a client cannot edit their registration after submitting one.
///
/// The lines are replaced wholesale rather than diffed, as UpdateBoq does
/// with its sections: the editor always sends the complete set.
/// </summary>
public sealed record UpdateMaintenanceRequestCommand : IRequest<ApiResult<MaintenanceRequestDto>>
{
    public Guid Id { get; init; }
    public IReadOnlyList<MaintenanceLineInput> Lines { get; init; } = [];
    public string? Description { get; init; }
    public string? SiteLocation { get; init; }
    public string? ClientName { get; init; }
    public string? ClientEmail { get; init; }
    public string? ContactNumber { get; init; }

    /// <summary>Lump sum off the total, in QAR.</summary>
    public decimal? Discount { get; init; }
}

public sealed class UpdateMaintenanceRequestCommandValidator : AbstractValidator<UpdateMaintenanceRequestCommand>
{
    public UpdateMaintenanceRequestCommandValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("A ticket needs at least one issue.")
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

public sealed class UpdateMaintenanceRequestCommandHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<UpdateMaintenanceRequestCommand, ApiResult<MaintenanceRequestDto>>
{
    public async Task<ApiResult<MaintenanceRequestDto>> Handle(
        UpdateMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var ticket = await context.MaintenanceRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ticket is null)
            return ApiResult<MaintenanceRequestDto>.Failure("Ticket not found.");

        if (ticket.Status is MaintenanceStatus.Resolved or MaintenanceStatus.Cancelled)
            return ApiResult<MaintenanceRequestDto>.Failure("This ticket is closed and can no longer be edited.");

        var canSetFee = currentUser.Has(Permissions.MaintenanceManage);
        var catalogue = await context.MaintenanceCatalogueItems.AsNoTracking().ToListAsync(cancellationToken);
        var lines = MaintenanceLines.Build(request.Lines, canSetFee, catalogue);

        if (MaintenanceLines.ApplyTotals(ticket, lines, request.Discount, canSetFee) is { } error)
            return ApiResult<MaintenanceRequestDto>.Failure(error);

        context.MaintenanceRequestLines.RemoveRange(ticket.Lines);
        foreach (var line in lines)
        {
            line.MaintenanceRequestId = ticket.Id;
            context.MaintenanceRequestLines.Add(line);
        }

        ticket.Lines = lines;
        ticket.Description = Clean(request.Description);
        ticket.SiteLocation = Clean(request.SiteLocation);
        ticket.ClientName = Clean(request.ClientName);
        ticket.ClientEmail = request.ClientEmail?.Trim() ?? string.Empty;
        ticket.ContactNumber = Clean(request.ContactNumber);
        ticket.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceRequestDto>.Success(MaintenanceMappings.ToDto(ticket));
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
