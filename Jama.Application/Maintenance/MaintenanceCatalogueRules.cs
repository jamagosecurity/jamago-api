using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance;

internal static class MaintenanceCatalogueRules
{
    internal const decimal PriceMax = 999_999.99m;

    /// <summary>Where the API serves a service picture's bytes from.</summary>
    internal static string ImageUrl(Guid imageId) => $"/api/maintenance/issue-catalogue/images/{imageId}";

    internal static MaintenanceImageDto ToDto(MaintenanceCatalogueImage image) =>
        new(image.Id, image.FileName, image.ContentType, image.SizeBytes, image.SortOrder, ImageUrl(image.Id));

    /// <summary>Needs <c>Images</c> loaded, or the service shows none.</summary>
    internal static MaintenanceIssueOptionDto ToDto(MaintenanceCatalogueItem item) =>
        new(item.Id, item.Category, item.Name, item.DescriptionEn, item.DescriptionAr, item.Price,
            item.Images.OrderBy(i => i.SortOrder).Select(ToDto).ToList());

    /// <summary>The stored category for what the caller typed or picked — a
    /// custom name takes the casing of the one already on the list.</summary>
    internal static async Task<string> ResolveCategoryAsync(
        IApplicationDbContext context, string? raw, CancellationToken cancellationToken)
    {
        var known = await context.MaintenanceCatalogueItems
            .Select(i => i.Category).Distinct().ToListAsync(cancellationToken);
        return MaintenanceCategories.Normalize(raw, known);
    }

    internal static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Whether another service in the category already has this name
    /// — compared case-insensitively, so "Cable cut" and "cable cut" cannot
    /// both sit on the list at different prices.</summary>
    internal static Task<bool> NameTakenAsync(
        IApplicationDbContext context, string category, string name, Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        return context.MaintenanceCatalogueItems.AnyAsync(
            i => i.Category == category && i.Name.ToLower() == lowered && i.Id != exceptId,
            cancellationToken);
    }
}
