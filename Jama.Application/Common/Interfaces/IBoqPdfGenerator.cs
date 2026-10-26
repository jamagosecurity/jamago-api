namespace Jama.Application.Common.Interfaces;

public sealed record BoqPdfLine(
    string Number,
    string ItemName,
    string? ModelNo,
    string? Brand,
    /// <summary>Form factor as the line recorded it — printed beside the model.</summary>
    string? Type,
    string Uom,
    decimal Quantity,
    decimal UnitRate,
    decimal LineTotal)
{
    /// <summary>Longer sales description, printed under the item name. Separate
    /// from the name because the client's layout gives description its own
    /// column.</summary>
    public string? Description { get; init; }

    /// <summary>The Arabic description. Held separately from the English so it
    /// can be laid out right-to-left; concatenating them would make that
    /// impossible.</summary>
    public string? DescriptionAr { get; init; }

    /// <summary>The item's first catalogue photo, already read into memory.
    /// Null when the item has no image, or when its file has gone missing from
    /// storage — a quotation must still print either way.</summary>
    public byte[]? Image { get; init; }
}

public sealed record BoqPdfSection(
    string Number,
    string Title,
    decimal Subtotal,
    IReadOnlyList<BoqPdfLine> Lines);

/// <summary>Which language(s) the terms letter prints in. English is the
/// default everywhere this is chosen — Arabic only ever prints because
/// someone deliberately asked for it.</summary>
public enum BoqTermsLanguage
{
    English,
    Arabic,
    Both,
}

public sealed record BoqPdfModel(
    string BoqNumber,
    string ProjectName,
    string? SiteLocation,
    string? ClientName,
    string? ContactNumber,
    DateOnly IssueDate,
    string Status,
    string? Notes,
    string? PreparedByName,
    /// <summary>Sum of the lines, before any discount.</summary>
    decimal Total,
    /// <summary>The lump sum agreed off the total. Zero prints nothing at all —
    /// a "Discount: 0.00" line on an undiscounted quotation invites the question
    /// of what was refused.</summary>
    decimal SpecialDiscount,
    /// <summary>What is payable: <see cref="Total"/> less
    /// <see cref="SpecialDiscount"/>.</summary>
    decimal GrandTotal,
    IReadOnlyList<BoqPdfSection> Sections)
{
    /// <summary>True for every status but Approved. Stamps a diagonal
    /// "DRAFT — NOT APPROVED" mark over the document so a pre-approval preview
    /// can never pass for the real thing if it is forwarded by mistake.</summary>
    public bool Watermark { get; init; }

    /// <summary>Whether to print the commercial-offer letter (page 2 — offer
    /// validity, payment terms, delivery, scope, warranty). On by default;
    /// some clients already have these terms agreed elsewhere and just want
    /// the cover and the priced items. Page numbers adjust themselves either
    /// way since QuestPDF counts whatever pages actually render.</summary>
    public bool IncludeTermsPage { get; init; } = true;

    /// <summary>Replaces the standard numbered terms (offer validity, payment
    /// split, delivery, scope, warranty) with this quotation's own wording —
    /// e.g. a 30% advance instead of the usual 50%. Null prints the standard
    /// terms. Has no effect when <see cref="IncludeTermsPage"/> is false.</summary>
    public string? TermsOverride { get; init; }

    /// <summary>The Arabic counterpart to <see cref="TermsOverride"/> — typed
    /// independently by staff, never derived from the English. Null prints
    /// the standard Arabic wording when Arabic is requested at all.</summary>
    public string? TermsOverrideAr { get; init; }

    /// <summary>Which language the terms letter prints in. Defaults to
    /// English regardless of whether Arabic wording exists — Arabic only
    /// ever prints because the download specifically asked for it.</summary>
    public BoqTermsLanguage TermsLanguage { get; init; } = BoqTermsLanguage.English;
}

public interface IBoqPdfGenerator
{
    byte[] Generate(BoqPdfModel model);

    /// <summary>The standard terms page, as plain text — the single source
    /// the editor pre-fills its "Terms &amp; Conditions" box with, so what
    /// staff see to edit is exactly what would otherwise print. Never drifts
    /// from the PDF because both read the same data.</summary>
    string DefaultTermsText();

    /// <summary>The Arabic counterpart to <see cref="DefaultTermsText"/> —
    /// same role, same round-trip guarantee, for the Arabic box.</summary>
    string DefaultTermsTextArabic();
}
