using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Queries.GetDefaultContractText;

/// <summary>The default Arabic contract wording for one quotation, for the
/// contract editor to pre-fill its text box with on first open — see
/// IContractTextTemplate.BuildDefault.</summary>
public sealed record DefaultContractTextDto(string Text);

public sealed record GetDefaultContractTextQuery(Guid Id) : IRequest<ApiResult<DefaultContractTextDto>>;

public sealed class GetDefaultContractTextQueryHandler(
    IApplicationDbContext context,
    IContractTextTemplate textTemplate)
    : IRequestHandler<GetDefaultContractTextQuery, ApiResult<DefaultContractTextDto>>
{
    public async Task<ApiResult<DefaultContractTextDto>> Handle(
        GetDefaultContractTextQuery request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null)
            return ApiResult<DefaultContractTextDto>.Failure("BOQ not found.");

        // Same gate as the PDF itself — the default quotes the approval date,
        // which does not exist yet on anything still being decided.
        if (boq.Status != BoqStatus.Approved)
            return ApiResult<DefaultContractTextDto>.Failure(
                "A contract can only be generated once this quotation is approved.");

        var text = textTemplate.BuildDefault(new ContractTextModel(
            boq.BoqNumber,
            boq.RevisionNumber,
            boq.ApprovedAt!.Value,
            boq.ProjectName,
            boq.SiteLocation,
            boq.ClientName,
            boq.GrandTotal));

        return ApiResult<DefaultContractTextDto>.Success(new DefaultContractTextDto(text));
    }
}
