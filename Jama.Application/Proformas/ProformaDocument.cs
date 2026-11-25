using System.Globalization;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Proformas;

/// <summary>Values typed on screen and not saved yet (for a preview). Empty customer details = as on the quotation.</summary>
public sealed record ProformaInput(string? PoNumber, DateOnly? PoDate, ProformaCustomer Customer);

/// <summary>A quotation with its proforma (if made) and today's name of who prepared it.</summary>
internal sealed record ProformaSource(Boq Boq, ProformaInvoice? Proforma, string? Ref)
{
    public bool Draft => !ProformaRules.CanIssue(Boq.Status);

    /// <summary>A proforma counts only while its quotation is approved; one revised back to draft prints as a draft.</summary>
    public ProformaInvoice? Kept => Draft ? null : Proforma;
}

/// <summary>
/// The proforma of a quotation exactly as it prints — built in one place for the PDF and for the on-screen invoice,
/// so the two can never disagree.
/// </summary>
internal static class ProformaDocument
{
    public static async Task<ProformaSource?> LoadAsync(IApplicationDbContext context, Guid boqId, CancellationToken ct)
    {
        var boq = await context.Boqs.AsNoTracking()
            .Include(b => b.Sections).ThenInclude(s => s.Lines)
            .FirstOrDefaultAsync(b => b.Id == boqId, ct);
        if (boq is null) return null;

        var proforma = await context.ProformaInvoices.AsNoTracking().FirstOrDefaultAsync(p => p.BoqId == boqId, ct);

        // Ref# is the person who prepared the quotation, by the name their account has today.
        var preparer = await context.AdminUsers.AsNoTracking()
            .Where(u => u.Id == boq.PreparedById)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
        var reference = string.IsNullOrWhiteSpace(preparer) ? StripEmail(boq.PreparedByName) : preparer.Trim();

        return new ProformaSource(boq, proforma, reference);
    }

    /// <param name="typed">Values typed on screen (a preview); null prints what is saved.</param>
    public static ProformaModel Build(ProformaSource source, TimeProvider clock, ProformaInput? typed = null)
    {
        var boq = source.Boq;
        var kept = source.Kept;
        if (source.Draft) typed = null;   // a draft prints the quotation as it is

        var poNumber = typed is not null ? (string.IsNullOrWhiteSpace(typed.PoNumber) ? null : typed.PoNumber.Trim()) : kept?.PoNumber;
        var poDate = poNumber is null ? null : typed is not null ? typed.PoDate : kept?.PoDate;

        // Customer details: as changed for this proforma (typed, or saved), else the quotation's.
        string? Pick(string? typedValue, string? keptValue, string? quotation) =>
            typed is not null ? ProformaRules.Changed(typedValue, quotation) ?? quotation : keptValue ?? quotation;

        var number = 0;
        var sections = boq.Sections
            .OrderBy(s => s.SortOrder)
            .Select(s => new ProformaModelSection(
                s.Title,
                s.Lines.OrderBy(l => l.SortOrder)
                    .Select(l => new ProformaModelLine(++number, l.ItemName, l.Brand, l.ModelNo, ProformaText.Unit(l.Uom.ToString()),
                        l.Quantity, l.UnitRate, l.LineTotal))
                    .ToList()))
            .Where(s => s.Lines.Count > 0)
            .ToList();

        var quoteNumber = boq.RevisionNumber > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{boq.BoqNumber} Rev {boq.RevisionNumber}")
            : boq.BoqNumber;

        return new ProformaModel(
            kept?.Number,
            kept?.IssuedOn ?? ProformaRules.DohaToday(clock),
            quoteNumber,
            boq.IssueDate,
            source.Ref,
            Pick(typed?.Customer.Name, kept?.CustomerName, boq.ClientName),
            Pick(typed?.Customer.Project, kept?.ProjectName, boq.ProjectName) ?? boq.ProjectName,
            Pick(typed?.Customer.ContactNumber, kept?.ContactNumber, boq.ContactNumber),
            Pick(typed?.Customer.Location, kept?.Location, boq.SiteLocation),
            sections,
            boq.Total,
            boq.SpecialDiscount,
            boq.GrandTotal,
            poNumber,
            poDate,
            source.Draft);
    }

    /// <summary>"Name &lt;email&gt;" → "Name".</summary>
    private static string? StripEmail(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var lt = name.IndexOf('<');
        return (lt > 0 ? name[..lt] : name).Trim();
    }
}
