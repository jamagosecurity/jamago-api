using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// A priced offer to a customer, built from stock items.
///
/// Totals are stored rather than derived on read. They are still computed on the
/// server from the lines on every write — see QuotationMath — but keeping the
/// result means the list can show a value per row without loading every line of
/// every quotation, and a historical quote keeps the figure it was sent with.
/// </summary>
public class Quotation : BaseEntity
{
    /// <summary>Human reference, e.g. "QT-2026-0007". Unique.</summary>
    public string QuoteNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCompany { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }

    public DateOnly IssueDate { get; set; }

    /// <summary>Last day the prices hold. Null when the offer has no stated expiry.</summary>
    public DateOnly? ValidUntil { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    public string? Notes { get; set; }
    public string? Terms { get; set; }

    // Money, all in QAR and all server-computed from the lines.
    public decimal Subtotal { get; set; }

    /// <summary>What the per-line discount percentages come to.</summary>
    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    /// <summary>
    /// A lump sum knocked off the finished quotation, in QAR — the discount
    /// agreed with the customer once the lines are settled, on top of whatever
    /// <see cref="DiscountTotal"/> the individual lines already carry.
    ///
    /// Held as an amount rather than a percentage because that is how it is
    /// negotiated: a round number off the total, not a rate applied to it.
    /// </summary>
    public decimal SpecialDiscount { get; set; }

    /// <summary>What the customer pays: the lines, less their own discounts, plus
    /// tax, less <see cref="SpecialDiscount"/>.</summary>
    public decimal GrandTotal { get; set; }

    public ICollection<QuotationLine> Lines { get; set; } = [];
}
