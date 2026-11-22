namespace Jama.Domain.Entities;

/// <summary>
/// End of the day: a person who collected cash hands it to the accountant, who counts it and records it here. One row
/// covers everything that person had collected and not yet handed over, for one kind of job ("MOI" or "NonMOI" — they are
/// handed over separately). The cash payments it covers point back to it, so a payment is handed over once and only once.
/// The names are copied, so the record reads the same even if someone is renamed or removed.
/// </summary>
public class CashHandover : BaseEntity
{
    /// <summary>"MOI" or "NonMOI".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Who took the cash. Null means it was received at the office.</summary>
    public Guid? CollectorId { get; set; }
    public string CollectorName { get; set; } = string.Empty;

    /// <summary>What the system said they should hand over (the cash payments recorded for them).</summary>
    public decimal ExpectedAmount { get; set; }

    /// <summary>What the accountant actually counted. Different from expected: missing or extra, with a note saying why.</summary>
    public decimal ReceivedAmount { get; set; }

    public string? Note { get; set; }

    public Guid ReceivedById { get; set; }
    public string ReceivedByName { get; set; } = string.Empty;
}
