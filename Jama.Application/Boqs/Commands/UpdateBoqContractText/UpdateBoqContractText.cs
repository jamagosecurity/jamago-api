using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.UpdateBoqContractText;

/// <summary>
/// Saves the contract screen's own wording for a quotation — separate from
/// UpdateBoqCommand on purpose. The priced lines are closed once a quotation
/// is approved and changing them is an amendment with its own accountability
/// (see BoqAmend); the contract's paperwork wording is not a change to what
/// was agreed on, so it does not need a reason or the amend grant — anyone
/// who can build or approve quotations can fill it in.
/// </summary>
public sealed record UpdateBoqContractTextCommand(Guid Id, string? ContractText)
    : IRequest<ApiResult<string>>;

public sealed class UpdateBoqContractTextCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateBoqContractTextCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(
        UpdateBoqContractTextCommand request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null)
            return ApiResult<string>.Failure("BOQ not found.");

        if (boq.Status != BoqStatus.Approved)
            return ApiResult<string>.Failure(
                "A contract can only be edited once this quotation is approved.");

        var trimmed = request.ContractText?.Trim();
        boq.ContractText = string.IsNullOrEmpty(trimmed) ? null : trimmed;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<string>.Success(boq.Id.ToString());
    }
}
