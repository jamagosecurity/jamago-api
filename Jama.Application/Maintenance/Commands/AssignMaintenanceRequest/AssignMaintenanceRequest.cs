using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.AssignMaintenanceRequest;

/// <summary>Assigns (or reassigns) a ticket to a staff account, moving an
/// Open ticket to InProgress. Gated on maintenance.manage.</summary>
public sealed record AssignMaintenanceRequestCommand(Guid Id, Guid StaffId) : IRequest<ApiResult<MaintenanceRequestDto>>;

public sealed class AssignMaintenanceRequestCommandValidator : AbstractValidator<AssignMaintenanceRequestCommand>
{
    public AssignMaintenanceRequestCommandValidator()
    {
        RuleFor(x => x.StaffId).NotEmpty();
    }
}

public sealed class AssignMaintenanceRequestCommandHandler(
    IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<AssignMaintenanceRequestCommand, ApiResult<MaintenanceRequestDto>>
{
    public async Task<ApiResult<MaintenanceRequestDto>> Handle(
        AssignMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var ticket = await context.MaintenanceRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ticket is null)
            return ApiResult<MaintenanceRequestDto>.Failure("Ticket not found.");

        if (ticket.Status is MaintenanceStatus.Resolved or MaintenanceStatus.Cancelled)
            return ApiResult<MaintenanceRequestDto>.Failure("This ticket is closed and can no longer be assigned.");

        var staff = await context.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AdminUserId == request.StaffId, cancellationToken);

        if (staff is null)
            return ApiResult<MaintenanceRequestDto>.Failure("That staff account was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        ticket.AssignedToId = staff.AdminUserId;
        ticket.AssignedToName = staff.FullName;
        ticket.AssignedAt = now;
        if (ticket.Status == MaintenanceStatus.Open)
            ticket.Status = MaintenanceStatus.InProgress;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceRequestDto>.Success(MaintenanceMappings.ToDto(ticket));
    }
}
