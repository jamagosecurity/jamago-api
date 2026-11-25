using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;

namespace Jama.Application.Proformas.Queries.GetProformaSheet;

public sealed record ProformaPaymentDto(string AccountHolder, string AccountNumber, string Bank, string Branch, string SwiftCode);

public sealed record ProformaCompanyDto(string Name, string Address, string Mobiles);

/// <param name="Document">Exactly what the PDF prints now.</param>
/// <param name="Proforma">What is saved for it, or null when no proforma was made yet.</param>
/// <param name="Quotation">The quotation's own customer details, to show what was changed and to go back to.</param>
public sealed record ProformaSheetDto(
    Guid BoqId,
    BoqStatus Status,
    ProformaModel Document,
    ProformaDto? Proforma,
    ProformaCustomer Quotation,
    string AmountInWords,
    IReadOnlyList<string> Terms,
    ProformaPaymentDto Payment,
    ProformaCompanyDto Company);

/// <summary>A quotation opened as its proforma invoice on screen — the same document the PDF prints.</summary>
public sealed record GetProformaSheetQuery(Guid BoqId) : IRequest<ApiResult<ProformaSheetDto>>;

public sealed class GetProformaSheetQueryHandler(IApplicationDbContext context, TimeProvider clock)
    : IRequestHandler<GetProformaSheetQuery, ApiResult<ProformaSheetDto>>
{
    public async Task<ApiResult<ProformaSheetDto>> Handle(GetProformaSheetQuery r, CancellationToken ct)
    {
        var source = await ProformaDocument.LoadAsync(context, r.BoqId, ct);
        if (source is null) return ApiResult<ProformaSheetDto>.Failure("Quotation not found.");

        var boq = source.Boq;
        var document = ProformaDocument.Build(source, clock);
        return ApiResult<ProformaSheetDto>.Success(new ProformaSheetDto(
            boq.Id,
            boq.Status,
            document,
            source.Kept is { } kept ? ProformaRules.ToDto(kept) : null,
            new ProformaCustomer(boq.ClientName, boq.ProjectName, boq.ContactNumber, boq.SiteLocation),
            MoneyWords.English(document.NetTotal),
            ProformaText.Terms,
            new ProformaPaymentDto(
                CompanyPaymentDetails.AccountHolder, CompanyPaymentDetails.AccountNumber, CompanyPaymentDetails.Bank,
                CompanyPaymentDetails.Branch, CompanyPaymentDetails.SwiftCode),
            new ProformaCompanyDto(ProformaText.CompanyName, ProformaText.CompanyAddress, ProformaText.CompanyMobiles)));
    }
}
