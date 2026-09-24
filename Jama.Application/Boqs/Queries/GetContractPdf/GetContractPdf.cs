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

        var model = new ContractPdfModel(
            boq.BoqNumber,
            boq.ApprovedAt!.Value,
            boq.ProjectName,
            boq.SiteLocation,
            boq.ClientName,
            boq.ContactNumber,
            boq.GrandTotal);

        return ApiResult<ContractPdfDto>.Success(
            new ContractPdfDto(generator.Generate(model), $"{boq.BoqNumber} Contract.pdf"));
    }
}
