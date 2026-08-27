namespace Jama.Domain.Entities;

/// <summary>
/// A heading within a bill of quantities — "Ground floor", "Car park". The
/// numbering a reader sees (1.1, 1.2, 2.1) comes from section and line order, so
/// it is never stored and can never drift from the actual sequence.
/// </summary>
public class BoqSection : BaseEntity
{
    public Guid BoqId { get; set; }
    public Boq Boq { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<BoqLine> Lines { get; set; } = [];
}
