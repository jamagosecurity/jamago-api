using Jama.Domain.Enums;

namespace Jama.Application.Quotations;

public sealed record QuotationLineDto(
    Guid Id,
    Guid? CameraId,
    string ItemName,
    string? ModelNo,
    string? Brand,
    string? Description,
    decimal Quantity,
    decimal UnitRate,
    decimal DiscountPercent,
    decimal TaxPercent,
    decimal LineTotal,
    int SortOrder);

public sealed record QuotationDto(
    Guid Id,
    string QuoteNumber,
    string CustomerName,
    string? CustomerCompany,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CustomerAddress,
    DateOnly IssueDate,
    DateOnly? ValidUntil,
    QuotationStatus Status,
    string? Notes,
    string? Terms,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    IReadOnlyList<QuotationLineDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Row shape for the list — the lines themselves are not needed there,
/// only how many there are.</summary>
public sealed record QuotationListItemDto(
    Guid Id,
    string QuoteNumber,
    string CustomerName,
    string? CustomerCompany,
    DateOnly IssueDate,
    DateOnly? ValidUntil,
    QuotationStatus Status,
    decimal GrandTotal,
    int LineCount,
    DateTime CreatedAt);

public sealed record QuotationSummaryDto(
    int TotalQuotations,
    int DraftCount,
    int SentCount,
    int AcceptedCount,
    /// <summary>Value of everything accepted — the figure that actually matters.</summary>
    decimal AcceptedValue,
    /// <summary>Value still awaiting an answer: drafts and sent quotes.</summary>
    decimal OpenValue);
