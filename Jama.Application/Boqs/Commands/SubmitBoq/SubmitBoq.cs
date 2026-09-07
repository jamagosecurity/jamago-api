using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.SubmitBoq;

/// <summary>
/// Hands a quotation to whoever may approve it.
///
/// Open to anyone who can build one — submitting is the last step of building,
/// not a decision about the document. What they cannot do is answer it.
/// </summary>
public sealed record SubmitBoqCommand(Guid Id) : IRequest<ApiResult<BoqDto>>;

public sealed class SubmitBoqCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitBoqCommand, ApiResult<BoqDto>>
{
    public async Task<ApiResult<BoqDto>> Handle(
        SubmitBoqCommand request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .Include(x => x.Sections).ThenInclude(x => x.Lines)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null)
            return ApiResult<BoqDto>.Failure("Quotation not found.");

        if (!BoqWorkflow.CanSubmit(boq.Status))
            return ApiResult<BoqDto>.Failure(
                boq.Status == BoqStatus.Submitted
                    ? "This quotation is already waiting for approval."
                    : "An approved quotation cannot be submitted again.");

        // Nothing to decide on otherwise, and an approver opening an empty
        // document has been sent something nobody meant to send.
        if (boq.Sections.All(section => section.Lines.Count == 0))
            return ApiResult<BoqDto>.Failure("Add at least one line before submitting for approval.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        BoqWorkflow.Submit(boq, now);
        BoqWorkflow.Record(context, boq, BoqApprovalAction.Submitted, actor, now);
        boq.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(boq));
    }
}
