using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// Money the accountant gave back to one person, settling one or more of their approved claims at once: when, how
/// (cash or bank transfer), how much, and who paid it.
/// </summary>
public class Reimbursement : BaseEntity
{
    /// <summary>"RB-00001".</summary>
    public string Number { get; set; } = string.Empty;

    public Guid PayeeId { get; set; }
    public string PayeeName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public ReimbursementMethod Method { get; set; }
    /// <summary>A bank reference or a note.</summary>
    public string? Reference { get; set; }

    public DateTime PaidAt { get; set; }
    public Guid PaidById { get; set; }
    public string PaidByName { get; set; } = string.Empty;

    public List<TravelClaim> Claims { get; set; } = [];
}
