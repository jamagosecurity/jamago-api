using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.ResolveMaintenanceRequest;

public sealed record ResolveMaintenanceRequestCommand(Guid Id, string Notes) : IRequest<ApiResult<MaintenanceRequestDto>>;

public sealed class ResolveMaintenanceRequestCommandValidator : AbstractValidator<ResolveMaintenanceRequestCommand>
{
    public ResolveMaintenanceRequestCommandValidator()
    {
        RuleFor(x => x.Notes).NotEmpty().WithMessage("Say what was done to fix it.").MaximumLength(2000);
    }
}

public sealed class ResolveMaintenanceRequestCommandHandler(
    IApplicationDbContext context, ICurrentUser actor, TimeProvider timeProvider)
    : IRequestHandler<ResolveMaintenanceRequestCommand, ApiResult<MaintenanceRequestDto>>
{
    public async Task<ApiResult<MaintenanceRequestDto>> Handle(
        ResolveMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var ticket = await context.MaintenanceRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ticket is null)
            return ApiResult<MaintenanceRequestDto>.Failure("Ticket not found.");

        if (ticket.Status is MaintenanceStatus.Resolved or MaintenanceStatus.Cancelled)
            return ApiResult<MaintenanceRequestDto>.Failure("This ticket is already closed.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        ticket.Status = MaintenanceStatus.Resolved;
        ticket.ResolutionNotes = request.Notes.Trim();
        ticket.ResolvedById = actor.UserId;
        ticket.ResolvedByName = actor.DisplayName;
        ticket.ResolvedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceRequestDto>.Success(MaintenanceMappings.ToDto(ticket));
    }
}
