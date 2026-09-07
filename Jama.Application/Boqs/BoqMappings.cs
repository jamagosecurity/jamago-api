using Jama.Domain.Enums;
using System.Globalization;
using Jama.Domain.Entities;

namespace Jama.Application.Boqs;

internal static class BoqMappings
{
    internal static BoqDto ToDto(Boq entity)
    {
        var sections = entity.Sections.OrderBy(s => s.SortOrder).ToList();

        return new BoqDto(
            entity.Id,
            entity.BoqNumber,
            entity.ProjectName,
            entity.SiteLocation,
            entity.ClientName,
            entity.ContactNumber,
            entity.IssueDate,
            entity.Status,
            entity.Notes,
            entity.PreparedById,
            entity.PreparedByName,
            entity.Total,
            entity.SpecialDiscount,
            entity.GrandTotal,
            entity.SubmittedAt,
            entity.ApprovedByName,
            entity.ApprovedAt,
            entity.RejectedByName,
            entity.RejectedAt,
            entity.RejectionReason,
            BoqWorkflow.IsEditable(entity.Status),
            entity.ApprovalEvents.Count(e => e.Action == BoqApprovalAction.Rejected),
            entity.ApprovalEvents.Count(e => e.Action == BoqApprovalAction.Submitted),
            // Oldest first: a trail is read as a story, and the story starts at
            // the beginning.
            entity.ApprovalEvents
                .OrderBy(e => e.CreatedAt)
                .Select(e => new BoqApprovalEventDto(
                    e.Id, e.Action, e.ActorId, e.ActorName, e.Reason, e.CreatedAt))
                .ToList(),
            sections.Select((section, index) => ToDto(section, index + 1)).ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static BoqSectionDto ToDto(BoqSection section, int number)
    {
        var lines = section.Lines.OrderBy(l => l.SortOrder).ToList();

        return new BoqSectionDto(
            section.Id,
            section.Title,
            section.SortOrder,
            lines.Sum(l => l.LineTotal),
            lines.Select((line, index) => ToDto(line, number, index + 1)).ToList());
    }

    private static BoqLineDto ToDto(BoqLine line, int sectionNumber, int lineNumber) =>
        new(
            line.Id,
            line.CameraId,
            // "1.2" — section then line, both by position, so the numbering can
            // never disagree with the order the reader sees.
            string.Create(CultureInfo.InvariantCulture, $"{sectionNumber}.{lineNumber}"),
            line.ItemName,
            line.ModelNo,
            line.Brand,
            line.Type,
            line.Uom,
            line.Quantity,
            line.Resolution,
            line.BitrateMbps,
            line.UnitRate,
            line.CatalogueRate,
            line.LineTotal,
            line.SortOrder);
}
