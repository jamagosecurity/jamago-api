using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.Technician.Queries.GetTechnicianInspection;

public sealed record GetTechnicianInspectionQuery(Guid Id) : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed class GetTechnicianInspectionHandler(
    ITechnicianInspectionRepository repository,
    ICurrentUser actor)
    : IRequestHandler<GetTechnicianInspectionQuery, ApiResult<TechnicianInspectionDto>>
{
    public async Task<ApiResult<TechnicianInspectionDto>> Handle(
        GetTechnicianInspectionQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.FindInspectionAsync(request.Id, cancellationToken);

        // Same message whether it is missing or belongs to another technician —
        // a different one would confirm the id exists.
        if (entity is null || entity.TechnicianId != actor.UserId)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection not found.");

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}
