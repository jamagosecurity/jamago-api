using System.Linq.Expressions;
using Jama.Domain.Entities;

namespace Jama.Application.Quotations;

internal static class QuotationMappings
{
    /// <summary>List projection — deliberately without the lines, which the list
    /// does not show and which would multiply the rows fetched by every quote's
    /// length.</summary>
    internal static readonly Expression<Func<Quotation, QuotationListItemDto>> ListProjection =
        entity => new QuotationListItemDto(
            entity.Id,
            entity.QuoteNumber,
            entity.CustomerName,
            entity.CustomerCompany,
            entity.IssueDate,
            entity.ValidUntil,
            entity.Status,
            entity.GrandTotal,
            entity.Lines.Count,
            entity.CreatedAt);

    internal static QuotationDto ToDto(Quotation entity) =>
        new(
            entity.Id,
            entity.QuoteNumber,
            entity.CustomerName,
            entity.CustomerCompany,
            entity.CustomerEmail,
            entity.CustomerPhone,
            entity.CustomerAddress,
            entity.IssueDate,
            entity.ValidUntil,
            entity.Status,
            entity.Notes,
            entity.Terms,
            entity.Subtotal,
            entity.DiscountTotal,
            entity.TaxTotal,
            entity.GrandTotal,
            entity.Lines
                .OrderBy(line => line.SortOrder)
                .Select(ToDto)
                .ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);

    internal static QuotationLineDto ToDto(QuotationLine line) =>
        new(
            line.Id,
            line.CameraId,
            line.ItemName,
            line.ModelNo,
            line.Brand,
            line.Description,
            line.Quantity,
            line.UnitRate,
            line.DiscountPercent,
            line.TaxPercent,
            line.LineTotal,
            line.SortOrder);
}
