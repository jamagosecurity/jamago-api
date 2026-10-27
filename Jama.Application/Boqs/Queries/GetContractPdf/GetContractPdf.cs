using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Queries.GetContractPdf;

public sealed record ContractPdfDto(byte[] Content, string FileName);

public sealed record GetContractPdfQuery(Guid Id) : IRequest<ApiResult<ContractPdfDto>>;

public sealed class GetContractPdfQueryHandler(
    IApplicationDbContext context,
    IContractPdfGenerator generator,
    IContractTextTemplate textTemplate,
    ICurrentUser actor)
    : IRequestHandler<GetContractPdfQuery, ApiResult<ContractPdfDto>>
{
    public async Task<ApiResult<ContractPdfDto>> Handle(
        GetContractPdfQuery request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null || !BoqVisibility.CanSee(boq.Status, boq.PreparedById, actor))
            return ApiResult<ContractPdfDto>.Failure("BOQ not found.");

        // A contract represents an agreement, so it does not exist for a
        // quotation nobody has agreed to yet — unlike the plain PDF, there is
        // no watermarked "draft contract" to fall back to here.
        if (boq.Status != BoqStatus.Approved)
            return ApiResult<ContractPdfDto>.Failure(
                "A contract can only be generated once this quotation is approved.");

        // Staff's own wording if the contract screen was ever saved for this
        // quotation, otherwise the same default that screen pre-fills with —
        // see GetDefaultContractTextQueryHandler, which builds it the
        // identical way so the two never drift apart.
        var body = boq.ContractText ?? textTemplate.BuildDefault(new ContractTextModel(
            boq.BoqNumber,
            boq.RevisionNumber,
            boq.ApprovedAt!.Value,
            boq.ProjectName,
            boq.SiteLocation,
            boq.ClientName,
            boq.GrandTotal));

        var model = new ContractPdfModel(boq.BoqNumber, boq.RevisionNumber, boq.ApprovedAt!.Value, body);

        return ApiResult<ContractPdfDto>.Success(
            new ContractPdfDto(
                generator.Generate(model),
                $"{boq.BoqNumber} Rev{boq.RevisionNumber} Contract.pdf"));
    }
}
