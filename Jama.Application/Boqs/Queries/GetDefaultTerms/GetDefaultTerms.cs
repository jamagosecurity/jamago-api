using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.Boqs.Queries.GetDefaultTerms;

/// <summary>Both standard terms pages as plain text, for the quotation
/// editor to pre-fill its English and Arabic "Terms &amp; Conditions" boxes
/// with — see IBoqPdfGenerator.DefaultTermsText/DefaultTermsTextArabic for
/// why neither can ever drift from what actually prints.</summary>
public sealed record DefaultTermsDto(string English, string Arabic);

public sealed record GetDefaultTermsQuery : IRequest<ApiResult<DefaultTermsDto>>;

public sealed class GetDefaultTermsQueryHandler(IBoqPdfGenerator generator)
    : IRequestHandler<GetDefaultTermsQuery, ApiResult<DefaultTermsDto>>
{
    public Task<ApiResult<DefaultTermsDto>> Handle(GetDefaultTermsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(ApiResult<DefaultTermsDto>.Success(
            new DefaultTermsDto(generator.DefaultTermsText(), generator.DefaultTermsTextArabic())));
}
