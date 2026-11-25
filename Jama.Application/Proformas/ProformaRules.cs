using System.Globalization;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.Proformas;

/// <summary>
/// A proforma invoice is the client-facing bill for an approved quotation. Only an approved quotation gets a
/// number; any other can still be downloaded as a preview, stamped DRAFT and without a number, so the
/// administrator can look at it without burning a PI number on work that may change.
/// </summary>
public static class ProformaRules
{
    public static bool CanIssue(BoqStatus status) => status == BoqStatus.Approved;

    public static DateOnly DohaToday(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));

    public static async Task<string> NextNumberAsync(IApplicationDbContext context, CancellationToken ct)
    {
        var next = await context.NextSequenceValueAsync(DocumentSequences.ProformaNumbers, ct);
        return string.Create(CultureInfo.InvariantCulture, $"PI-JG-{next:00000}");
    }

    public static ProformaDto ToDto(ProformaInvoice p) =>
        new(p.Number, p.IssuedOn, p.IssuedByName, p.PoNumber, p.PoDate, p.CustomerName, p.ProjectName, p.ContactNumber, p.Location);

    /// <summary>What to keep for a typed detail: null when it is empty or the same as the quotation's, so it keeps
    /// following the quotation; otherwise the typed text.</summary>
    public static string? Changed(string? typed, string? quotation)
    {
        var value = string.IsNullOrWhiteSpace(typed) ? null : typed.Trim();
        return value is null || value == quotation?.Trim() ? null : value;
    }

    public const string NotApproved = "Only an approved quotation gets a proforma invoice number. You can still download a draft preview.";
    public const string ChangedMeanwhile = "Someone else saved this proforma just now. Refresh and try again.";
}
