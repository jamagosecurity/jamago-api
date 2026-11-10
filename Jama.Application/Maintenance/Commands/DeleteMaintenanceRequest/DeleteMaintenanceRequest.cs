using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.DeleteMaintenanceRequest;

/// <summary>Removes a ticket outright — a mistaken or duplicate entry.
/// Only while still Open: once assigned or closed, Cancel is the
/// withdrawal route instead, which keeps the record.</summary>
public sealed record DeleteMaintenanceRequestCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class DeleteMaintenanceRequestCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteMaintenanceRequestCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(DeleteMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var ticket = await context.MaintenanceRequests
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ticket is null)
            return ApiResult<Guid>.Failure("Ticket not found.");

        if (ticket.Status != MaintenanceStatus.Open)
            return ApiResult<Guid>.Failure("Only an open, unassigned ticket can be deleted. Cancel it instead.");

        context.MaintenanceRequests.Remove(ticket);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<Guid>.Success(ticket.Id);
    }
}
