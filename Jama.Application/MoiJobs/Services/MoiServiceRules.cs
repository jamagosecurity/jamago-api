using Jama.Application.Maintenance;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

internal static class MoiServiceRules
{
    internal const decimal PriceMax = 999_999.99m;

    /// <summary>Where the API serves a service picture's bytes from.</summary>
    internal static string ImageUrl(Guid imageId) => $"/api/moijobs/services/images/{imageId}";

    internal static MaintenanceImageDto ToDto(MoiServiceImage image) =>
        new(image.Id, image.FileName, image.ContentType, image.SizeBytes, image.SortOrder, ImageUrl(image.Id));

    /// <summary>Needs <c>Images</c> loaded, or the service shows none.</summary>
    internal static MaintenanceIssueOptionDto ToDto(MoiServiceItem item) =>
        new(item.Id, item.Category, item.Name, item.DescriptionEn, item.DescriptionAr, item.Price,
            item.Images.OrderBy(i => i.SortOrder).Select(ToDto).ToList());

    /// <summary>The stored category for what the caller typed or picked — a
    /// custom name takes the casing of the one already on the list.</summary>
    internal static async Task<string> ResolveCategoryAsync(
        IApplicationDbContext context, string? raw, CancellationToken cancellationToken)
    {
        var known = await context.MoiServiceItems
            .Select(i => i.Category).Distinct().ToListAsync(cancellationToken);
        return MoiServiceCategories.Normalize(raw, known);
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
        return context.MoiServiceItems.AnyAsync(
            i => i.Category == category && i.Name.ToLower() == lowered && i.Id != exceptId,
            cancellationToken);
    }
}
