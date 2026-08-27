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
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public ICollection<QuotationLine> Lines { get; set; } = [];
}
