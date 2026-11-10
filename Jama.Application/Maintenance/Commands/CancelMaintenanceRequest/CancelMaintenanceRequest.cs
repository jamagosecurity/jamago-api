using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.CancelMaintenanceRequest;

public sealed record CancelMaintenanceRequestCommand(Guid Id, string Reason) : IRequest<ApiResult<MaintenanceRequestDto>>;

public sealed class CancelMaintenanceRequestCommandValidator : AbstractValidator<CancelMaintenanceRequestCommand>
{
    public CancelMaintenanceRequestCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why this ticket is being withdrawn.").MaximumLength(500);
    }
}

public sealed class CancelMaintenanceRequestCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CancelMaintenanceRequestCommand, ApiResult<MaintenanceRequestDto>>
{
    public async Task<ApiResult<MaintenanceRequestDto>> Handle(
        CancelMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var ticket = await context.MaintenanceRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ticket is null)
            return ApiResult<MaintenanceRequestDto>.Failure("Ticket not found.");

        if (ticket.Status is MaintenanceStatus.Resolved or MaintenanceStatus.Cancelled)
            return ApiResult<MaintenanceRequestDto>.Failure("This ticket is already closed.");

        ticket.Status = MaintenanceStatus.Cancelled;
        ticket.CancellationReason = request.Reason.Trim();

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceRequestDto>.Success(MaintenanceMappings.ToDto(ticket));
    }
}
