namespace Jama.Application.Common.Interfaces;

public sealed record BoqPdfLine(
    string Number,
    string ItemName,
    string? ModelNo,
    string? Brand,
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
    decimal Total,
    IReadOnlyList<BoqPdfSection> Sections);

public interface IBoqPdfGenerator
{
    byte[] Generate(BoqPdfModel model);
}
