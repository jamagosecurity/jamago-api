namespace Jama.Domain.Entities;

/// <summary>The most the company pays per person for a site lunch, from a given day on. Adding a row changes the rate
/// for trips from that day; claims already made keep the rate they were made with.</summary>
public class LunchRate : BaseEntity
{
    public decimal AmountPerPerson { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public Guid SetById { get; set; }
    public string SetByName { get; set; } = string.Empty;
}
