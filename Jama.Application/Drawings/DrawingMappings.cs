using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.Drawings;

internal static class DrawingMappings
{
    internal static DrawingDto ToDto(Drawing entity)
    {
        var events = entity.ApprovalEvents.OrderBy(e => e.CreatedAt).ToList();

        return new DrawingDto(
            entity.Id,
            entity.DrawingNumber,
            entity.ProjectName,
            entity.ClientName,
            entity.SiteLocation,
            entity.ContactNumber,
            entity.Notes,
            entity.Status,
            entity.PreparedById,
            entity.PreparedByName,
            entity.SubmittedAt,
            entity.ApprovedByName,
            entity.ApprovedAt,
            entity.RejectedByName,
            entity.RejectedAt,
            entity.RejectionReason,
            DrawingWorkflow.IsEditable(entity.Status),
            events.Count(e => e.Action == DrawingApprovalAction.Rejected),
            events.Count(e => e.Action == DrawingApprovalAction.Submitted),
            entity.Files
                .OrderBy(f => f.CreatedAt)
                .Select(ToFileDto)
                .ToList(),
            events
                .Select(e => new DrawingApprovalEventDto(
                    e.Id, e.Action, e.ActorId, e.ActorName, e.Reason, e.CreatedAt))
                .ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    internal static DrawingFileDto ToFileDto(DrawingFile file) =>
        new(file.Id, file.FileName, file.ContentType, file.SizeBytes, file.UploadedByName, file.CreatedAt);
}
