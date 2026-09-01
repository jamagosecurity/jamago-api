using Jama.Application.Common.Interfaces;
using Jama.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras;

/// <summary>
/// Rules shared by the create and update commands, so both answer "is this a
/// duplicate line?" the same way.
/// </summary>
internal static class CameraRules
{
    internal static string NormalizeBrand(string? brand) => brand?.Trim() ?? string.Empty;

    /// <summary>
    /// Blank and null collapse to the same empty string. Camera.ModelNo explains
    /// why the column never holds null.
    /// </summary>
    internal static string NormalizeModelNo(string? modelNo) => modelNo?.Trim() ?? string.Empty;

    /// <summary>Trimmed, with blank collapsing to null. For every optional text
    /// field that nothing indexes — descriptions, notes, search key, HSN code.</summary>
    internal static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// One line per brand, type and model: two rows for the same three would split
    /// a single stock figure across two places, and every later count would have
    /// to know to add them up. Two models of the same brand and type ARE separate
    /// lines, which is why the model number is part of the key.
    ///
    /// Compared case-insensitively so "hikvision" cannot slip past as a second
    /// line. A unique index backs this up for concurrent writes; the check here
    /// exists to return a readable message instead of a 500.
    /// </summary>
    internal static Task<bool> ExistsAsync(
        IApplicationDbContext context,
        string brand,
        string? type,
        string modelNo,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalizedBrand = brand.ToLower();
        var normalizedModel = modelNo.ToLower();
        // Type joined the key as free text, so "Dome" and "dome" have to be the
        // same item — otherwise the unique key stops catching duplicates.
        var normalizedType = (type ?? string.Empty).Trim().ToLower();

        var query = context.Cameras
            .AsNoTracking()
            .Where(x => x.Type.ToLower() == normalizedType
                && x.Brand.ToLower() == normalizedBrand
                && x.ModelNo.ToLower() == normalizedModel);

        // Applied in C# rather than as `id == null || x.Id != id` so the filter
        // is simply absent on create, instead of a constant EF has to translate.
        if (excludingId is { } id)
            query = query.Where(x => x.Id != id);

        return query.AnyAsync(cancellationToken);
    }

    internal static string DuplicateMessage(string brand, string? type, string modelNo)
    {
        // Naming the model only when there is one — "Hikvision (Dome, )" reads
        // like a bug to whoever gets the message.
        var line = string.IsNullOrEmpty(modelNo)
            ? $"{brand} ({type})"
            : $"{brand} {modelNo} ({type})";

        return $"{line} is already in the inventory. Edit that line to change its quantity.";
    }
}
