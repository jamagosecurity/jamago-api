using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings;

internal static class DrawingNumbers
{
    internal const string Prefix = "DRW-";

    /// <summary>
    /// Next drawing reference, e.g. "DRW-00007". Mirrors BoqNumbers: one
    /// unbroken sequence derived from the highest already issued, so deleting a
    /// drawing never frees its number for reuse.
    /// </summary>
    internal static async Task<string> NextAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var used = await context.Drawings
            .AsNoTracking()
            .Where(x => x.DrawingNumber.StartsWith(Prefix))
            .Select(x => x.DrawingNumber)
            .ToListAsync(cancellationToken);

        var highest = used
            .Select(number => int.TryParse(number[Prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefix}{highest + 1:D5}";
    }
}
