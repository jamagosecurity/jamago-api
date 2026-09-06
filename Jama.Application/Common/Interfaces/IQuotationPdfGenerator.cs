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
    /// <summary>The "Total" line: the lines less their own discounts, plus tax,
    /// before the discount given on the finished quote.</summary>
    decimal TotalBeforeDiscount,
    /// <summary>The lump sum off the finished quote. Zero prints no discount row.</summary>
    decimal SpecialDiscount,
    /// <summary>The "Final amount" line — what the customer pays.</summary>
    decimal GrandTotal,
    IReadOnlyList<QuotationPdfLine> Lines);

public interface IQuotationPdfGenerator
{
    byte[] Generate(QuotationPdfModel model);
}
