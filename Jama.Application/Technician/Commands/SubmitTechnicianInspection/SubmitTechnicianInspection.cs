using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician.Commands.SubmitTechnicianInspection;

public sealed record SubmitTechnicianInspectionCommand(Guid InspectionId)
    : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed class SubmitTechnicianInspectionHandler(
    ITechnicianInspectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitTechnicianInspectionCommand, ApiResult<TechnicianInspectionDto>>
{
    /// <summary>
    /// Submitting a quarter is what generates its invoice, so the number format
    /// belongs to this use case rather than to the shared helpers.
    /// </summary>
    private static string GenerateInvoiceNumber(DiaInspection dia, int quarter, DateTime generatedAt) =>
        $"INV-{dia.DiaNumber}-{quarter:D1}-{generatedAt:yyyyMMddHHmmss}";

    public async Task<ApiResult<TechnicianInspectionDto>> Handle(
        SubmitTechnicianInspectionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.FindInspectionAsync(request.InspectionId, cancellationToken);
        if (entity is null)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection not found.");

        if (entity.Status == TechnicianInspectionStatus.Submitted)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection is already submitted.");

        var dia = await repository.FindActiveDiaAsync(entity.DiaInspectionId, cancellationToken);
        if (dia is null)
            return ApiResult<TechnicianInspectionDto>.Failure("Activated DIA inspection not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var before = TechnicianSupport.Snapshot(entity);
        entity.Status = TechnicianInspectionStatus.Submitted;
        entity.SubmittedAt = now;
        entity.UpdatedAt = now;

        var invoiceNumber = GenerateInvoiceNumber(dia, entity.Quarter, now);
        repository.AddInvoice(new InspectionInvoice
        {
            Id = Guid.CreateVersion7(),
            TechnicianInspectionId = entity.Id,
            DiaInspectionId = dia.Id,
            Quarter = entity.Quarter,
            InvoiceNumber = invoiceNumber,
            GeneratedAt = now,
            CreatedAt = now,
        });

        repository.AddHistory(TechnicianSupport.Audit(
            entity, TechnicianInspectionAction.Submit, actor, before, TechnicianSupport.Snapshot(entity)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}
