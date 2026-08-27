using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs;

internal static class BoqNumbers
{
    internal const string Prefix = "JMG-";

    /// <summary>
    /// Next quote reference, e.g. "JMG-00007".
    ///
    /// One unbroken sequence, not restarted each year: a quote is referred to by
    /// its number alone, so the number has to be unique on its own.
    ///
    /// Derived from the highest already issued rather than from a count, so
    /// deleting one does not cause the next to reuse a reference that has
    /// already been circulated. A unique index backs it up for concurrent writes.
    /// </summary>
    internal static async Task<string> NextAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var used = await context.Boqs
            .AsNoTracking()
            .Where(x => x.BoqNumber.StartsWith(Prefix))
            .Select(x => x.BoqNumber)
            .ToListAsync(cancellationToken);

        var highest = used
            .Select(number => int.TryParse(number[Prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefix}{highest + 1:D5}";
    }
}
