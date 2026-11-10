using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Queries.GetMaintenanceRequest;

public sealed record GetMaintenanceRequestQuery(Guid Id) : IRequest<ApiResult<MaintenanceRequestDto>>;

public sealed class GetMaintenanceRequestQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetMaintenanceRequestQuery, ApiResult<MaintenanceRequestDto>>
{
    public async Task<ApiResult<MaintenanceRequestDto>> Handle(
        GetMaintenanceRequestQuery request, CancellationToken cancellationToken)
    {
        var ticket = await context.MaintenanceRequests
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ticket is null || !MaintenanceVisibility.CanSee(ticket.ReportedById, ticket.AssignedToId, actor))
            return ApiResult<MaintenanceRequestDto>.Failure("Ticket not found.");

        return ApiResult<MaintenanceRequestDto>.Success(MaintenanceMappings.ToDto(ticket));
    }
}
