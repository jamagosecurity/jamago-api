using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Quotations.Commands.UpdateQuotation;

public sealed record UpdateQuotationCommand : IRequest<ApiResult<QuotationDto>>, IQuotationWrite
{
    /// <summary>Set from the route by the endpoint, so a mismatched body id cannot
    /// redirect the write at another quotation.</summary>
    public Guid Id { get; init; }

    public string? CustomerName { get; init; }
    public string? CustomerCompany { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }
    public string? CustomerAddress { get; init; }
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public QuotationStatus Status { get; init; } = QuotationStatus.Draft;
    public string? Notes { get; init; }
    public string? Terms { get; init; }
    public decimal SpecialDiscount { get; init; }
    public IReadOnlyList<QuotationLineInput> Lines { get; init; } = [];
}

public sealed class UpdateQuotationCommandHandler(
    IApplicationDbContext context,
    TimeProvider timeProvider,
    IWhatsAppSender whatsAppSender)
    : IRequestHandler<UpdateQuotationCommand, ApiResult<QuotationDto>>
{
    public async Task<ApiResult<QuotationDto>> Handle(
        UpdateQuotationCommand request,
        CancellationToken cancellationToken)
    {
        var quotation = await context.Quotations
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (quotation is null)
            return ApiResult<QuotationDto>.Failure("Quotation not found.");

        // Captured before Apply overwrites it, so the WhatsApp notification
        // fires once — on the edit that first moves the quote to Sent — rather
        // than on every subsequent edit of an already-sent quote.
        var wasSent = quotation.Status == QuotationStatus.Sent;

        // The quote number is issued once and never rewritten: it may already be
        // on a document in a customer's inbox.
        QuotationWriter.Apply(quotation, request, timeProvider);
        quotation.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync(cancellationToken);

        if (!wasSent && quotation.Status == QuotationStatus.Sent && !string.IsNullOrWhiteSpace(quotation.CustomerPhone))
        {
            await whatsAppSender.SendQuotationSentAsync(
                quotation.CustomerPhone,
                quotation.CustomerName,
                quotation.QuoteNumber,
                quotation.GrandTotal,
                quotation.Id,
                cancellationToken);
        }

        return ApiResult<QuotationDto>.Success(QuotationMappings.ToDto(quotation));
    }
}
