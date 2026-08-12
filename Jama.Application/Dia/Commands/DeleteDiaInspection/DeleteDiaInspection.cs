using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia.Commands.DeleteDiaInspection;

/// <summary>
/// Permanent removal, as against the reversible Archive in
/// <c>ChangeDiaInspectionStateCommand</c>. It exists to clear out test rows and
/// records created by mistake, so anything carrying real inspection evidence is
/// refused rather than cascaded away.
///
/// Deliberately not folded into <c>DiaMutation</c>: the lifecycle mutations all
/// leave the record in place with an audit entry describing the change, and this
/// one leaves nothing behind to attach an audit entry to.
/// </summary>
public sealed record DeleteDiaInspectionCommand(Guid Id) : IRequest<ApiResult<Guid>>
{
    /// <summary>
    /// Lets the endpoint answer 404 for a missing record and 409 for a refusal,
    /// without matching on message text.
    /// </summary>
    public const string NotFoundError = "DIA inspection not found.";
}

public sealed class DeleteDiaInspectionHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteDiaInspectionCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteDiaInspectionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DiaInspections
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return ApiResult<Guid>.Failure(DeleteDiaInspectionCommand.NotFoundError);
        }

        // Archive first. That makes deletion two deliberate steps, and means a
        // record can only be destroyed once it has already left the active
        // register — where removing the wrong one is still reversible.
        if (!entity.IsArchived)
        {
            return ApiResult<Guid>.Failure(
                "Archive this DIA record before deleting it permanently.");
        }

        // Submitted inspections are the evidence the quarterly MOI process runs
        // on, so their existence vetoes the delete outright. TechnicianInspection
        // has an IsDeleted flag but no query filter, and a soft-deleted
        // submission is still a submission — so this counts every row.
        var submittedInspections = await context.TechnicianInspections
            .CountAsync(x => x.DiaInspectionId == entity.Id, cancellationToken);

        if (submittedInspections > 0)
        {
            return ApiResult<Guid>.Failure(
                $"This DIA record has {submittedInspections} submitted inspection(s) and cannot be "
                + "deleted. Leave it archived instead.");
        }

        // Invoices hang off the inspections above, so this should be unreachable
        // — but an invoice outliving its inspection is exactly the sort of thing
        // worth refusing rather than discovering afterwards.
        var invoices = await context.InspectionInvoices
            .CountAsync(x => x.DiaInspectionId == entity.Id, cancellationToken);

        if (invoices > 0)
        {
            return ApiResult<Guid>.Failure(
                $"This DIA record has {invoices} invoice(s) raised against it and cannot be deleted. "
                + "Leave it archived instead.");
        }

        // The audit rows exist to explain this record; once it is gone they
        // describe nothing. Their foreign key is Restrict, so they must go first
        // or SaveChanges fails on the constraint.
        var history = await context.DiaInspectionHistory
            .Where(x => x.DiaInspectionId == entity.Id)
            .ToListAsync(cancellationToken);

        context.DiaInspectionHistory.RemoveRange(history);
        context.DiaInspections.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<Guid>.Success(entity.Id);
    }
}
