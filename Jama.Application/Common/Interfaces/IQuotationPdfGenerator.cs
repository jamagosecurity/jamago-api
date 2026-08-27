namespace Jama.Application.Common.Interfaces;

public sealed record QuotationPdfLine(
    int Number,
    string ItemName,
    string? ModelNo,
    string? Brand,
    string? Description,
    decimal Quantity,
    decimal UnitRate,
    decimal DiscountPercent,
    decimal TaxPercent,
    decimal LineTotal);

public sealed record QuotationPdfModel(
    string QuoteNumber,
    string CustomerName,
    string? CustomerCompany,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CustomerAddress,
    DateOnly IssueDate,
    DateOnly? ValidUntil,
    string Status,
    string? Notes,
    string? Terms,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    IReadOnlyList<QuotationPdfLine> Lines);

public interface IQuotationPdfGenerator
{
    byte[] Generate(QuotationPdfModel model);
}
