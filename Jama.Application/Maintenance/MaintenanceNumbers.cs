using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance;

internal static class MaintenanceNumbers
{
    internal const string Prefix = "MNT-";

    /// <summary>Next ticket reference, e.g. "MNT-00007". Same scheme as
    /// BoqNumbers — derived from the highest already issued so a deleted
    /// ticket never causes the next one to reuse a number.</summary>
    internal static async Task<string> NextAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var used = await context.MaintenanceRequests
            .AsNoTracking()
            .Where(x => x.RequestNumber.StartsWith(Prefix))
            .Select(x => x.RequestNumber)
            .ToListAsync(cancellationToken);

        var highest = used
            .Select(number => int.TryParse(number[Prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefix}{highest + 1:D5}";
    }
}
