using Jama.Domain.Entities;

namespace Jama.Application.Maintenance;

/// <summary>Both mappings read <see cref="MaintenanceRequest.Lines"/>, so every
/// query or command that maps a ticket has to Include them first.</summary>
internal static class MaintenanceMappings
{
    internal static MaintenanceRequestDto ToDto(MaintenanceRequest request) => new(
        request.Id,
        request.RequestNumber,
        MaintenanceLines.Ordered(request.Lines)
            .Select(l => new MaintenanceLineDto(
                l.Id, l.Category, l.IssueType, l.DescriptionEn, l.DescriptionAr,
                l.Quantity, l.UnitFee, l.CatalogueFee, l.LineTotal))
            .ToList(),
        request.Description,
        request.SiteLocation,
        request.ClientName,
        request.ClientEmail,
        request.ContactNumber,
        request.Status,
        request.Subtotal,
        request.Discount,
        request.ServiceFee,
        request.ReportedById,
        request.ReportedByName,
        request.AssignedToId,
        request.AssignedToName,
        request.AssignedAt,
        request.ResolutionNotes,
        request.ResolvedByName,
        request.ResolvedAt,
        request.CancellationReason,
        request.CreatedAt,
        request.UpdatedAt);

    internal static MaintenanceRequestListItemDto ToListItemDto(MaintenanceRequest request)
    {
        var lines = MaintenanceLines.Ordered(request.Lines).ToList();
        var summary = lines.Count switch
        {
            0 => "No issues listed",
            1 => lines[0].IssueType,
            _ => $"{lines[0].IssueType} + {lines.Count - 1} more",
        };

        return new MaintenanceRequestListItemDto(
            request.Id,
            request.RequestNumber,
            summary,
            lines.Select(l => l.Category).Distinct().ToList(),
            lines.Count,
            request.SiteLocation,
            request.ClientName,
            request.Status,
            request.ServiceFee,
            request.ReportedByName,
            request.AssignedToName,
            request.CreatedAt);
    }
}
