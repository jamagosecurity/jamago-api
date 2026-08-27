using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Quotations;

internal static class QuoteNumbers
{
    internal const string Prefix = "QT";

    /// <summary>
    /// Next reference for the current year, e.g. "QT-2026-0007".
    ///
    /// Derived from the highest number already issued this year rather than from
    /// a count, so deleting a quotation does not cause the next one to reuse a
    /// reference that has already been sent to a customer. A unique index on the
    /// column is the backstop for two writers racing.
    /// </summary>
    internal static async Task<string> NextAsync(
        IApplicationDbContext context,
        int year,
        CancellationToken cancellationToken)
    {
        var yearPrefix = $"{Prefix}-{year}-";

        var used = await context.Quotations
            .AsNoTracking()
            .Where(x => x.QuoteNumber.StartsWith(yearPrefix))
            .Select(x => x.QuoteNumber)
            .ToListAsync(cancellationToken);

        var highest = used
            .Select(number => int.TryParse(number[yearPrefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{yearPrefix}{highest + 1:D4}";
    }
}
