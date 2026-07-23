using System.Security.Cryptography;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Technician;

public sealed record InvoicePdfResult(byte[] Content, string FileName);

public sealed record InvoiceShareLinkDto(string Token, DateTime ExpiresAtUtc);

public sealed record GetInvoicePdfQuery(Guid InvoiceId) : IRequest<ApiResult<InvoicePdfResult>>;

public sealed record CreateInvoiceShareLinkCommand(Guid InvoiceId) : IRequest<ApiResult<InvoiceShareLinkDto>>;

public sealed record GetSharedInvoicePdfQuery(string Token) : IRequest<ApiResult<InvoicePdfResult>>;

internal static class InvoicePdfSupport
{
    public static async Task<InvoicePdfResult> BuildAsync(
        InspectionInvoice invoice,
        ITechnicianInspectionRepository repository,
        IApplicationDbContext context,
        IInvoicePdfGenerator pdfGenerator,
        CancellationToken cancellationToken)
    {
        var dia = await context.DiaInspections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == invoice.DiaInspectionId, cancellationToken);

        var inspection = await repository.Inspections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == invoice.TechnicianInspectionId, cancellationToken);

        string? technicianName = null;
        if (inspection is not null)
        {
            technicianName = await context.AdminUsers.AsNoTracking()
                .Where(x => x.Id == inspection.TechnicianId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var model = new InvoicePdfModel(
            invoice.InvoiceNumber,
            dia?.DiaNumber ?? "-",
            dia?.ClientName ?? "-",
            dia?.ClientNumber ?? "-",
            dia?.ClientLocation ?? "-",
            invoice.Quarter,
            invoice.GeneratedAt,
            technicianName ?? "-");

        return new InvoicePdfResult(pdfGenerator.Generate(model), $"{invoice.InvoiceNumber}.pdf");
    }

    public static string GenerateShareToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

public sealed class GetInvoicePdfHandler(
    ITechnicianInspectionRepository repository,
    IApplicationDbContext context,
    IInvoicePdfGenerator pdfGenerator)
    : IRequestHandler<GetInvoicePdfQuery, ApiResult<InvoicePdfResult>>
{
    public async Task<ApiResult<InvoicePdfResult>> Handle(
        GetInvoicePdfQuery request,
        CancellationToken cancellationToken)
    {
        var invoice = await repository.Invoices.FirstOrDefaultAsync(x => x.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return ApiResult<InvoicePdfResult>.Failure("Invoice not found.");

        var result = await InvoicePdfSupport.BuildAsync(invoice, repository, context, pdfGenerator, cancellationToken);
        return ApiResult<InvoicePdfResult>.Success(result);
    }
}

public sealed class CreateInvoiceShareLinkHandler(
    ITechnicianInspectionRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateInvoiceShareLinkCommand, ApiResult<InvoiceShareLinkDto>>
{
    public async Task<ApiResult<InvoiceShareLinkDto>> Handle(
        CreateInvoiceShareLinkCommand request,
        CancellationToken cancellationToken)
    {
        var invoice = await repository.FindInvoiceForUpdateAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return ApiResult<InvoiceShareLinkDto>.Failure("Invoice not found.");

        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(7);
        invoice.ShareToken = InvoicePdfSupport.GenerateShareToken();
        invoice.ShareTokenExpiresAtUtc = expiresAt;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResult<InvoiceShareLinkDto>.Success(new(invoice.ShareToken, expiresAt));
    }
}

public sealed class GetSharedInvoicePdfHandler(
    ITechnicianInspectionRepository repository,
    IApplicationDbContext context,
    IInvoicePdfGenerator pdfGenerator,
    TimeProvider timeProvider)
    : IRequestHandler<GetSharedInvoicePdfQuery, ApiResult<InvoicePdfResult>>
{
    private const string InvalidLinkMessage = "This link is invalid or has expired.";

    public async Task<ApiResult<InvoicePdfResult>> Handle(
        GetSharedInvoicePdfQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return ApiResult<InvoicePdfResult>.Failure(InvalidLinkMessage);

        var invoice = await repository.Invoices
            .FirstOrDefaultAsync(x => x.ShareToken == request.Token, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (invoice is null || invoice.ShareTokenExpiresAtUtc is null || invoice.ShareTokenExpiresAtUtc < now)
            return ApiResult<InvoicePdfResult>.Failure(InvalidLinkMessage);

        var result = await InvoicePdfSupport.BuildAsync(invoice, repository, context, pdfGenerator, cancellationToken);
        return ApiResult<InvoicePdfResult>.Success(result);
    }
}
