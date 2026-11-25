using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs;

/// <summary>
/// A quotation copies the staff member's name — "Name &lt;email&gt;", the same text as the signed-in user's
/// display name — when they prepare, approve, reject or delete it, and each history entry copies it too.
/// When that account is renamed or its email changes, <see cref="RenameAsync"/> brings all of that person's
/// quotations and quotation history up to date, so the list, the page and the PDF show the new name.
/// </summary>
public static class BoqNames
{
    /// <summary>The name exactly as a quotation stores it; same rules as the signed-in user's display name.</summary>
    public static string? Format(string? fullName, string? email) => (fullName?.Trim(), email?.Trim()) switch
    {
        ({ Length: > 0 } n, { Length: > 0 } e) => $"{n} <{e}>",
        ({ Length: > 0 } n, _) => n,
        (_, { Length: > 0 } e) => e,
        _ => null,
    };

    /// <summary>Puts <paramref name="user"/>'s current name on every quotation (deleted ones included) and
    /// history entry that person is recorded on. Changes are tracked; the caller saves them.</summary>
    public static async Task RenameAsync(IApplicationDbContext context, AdminUser user, CancellationToken ct)
    {
        var id = user.Id;
        var name = Format(user.FullName, user.Email);

        var boqs = await context.Boqs.IgnoreQueryFilters()
            .Where(b => b.PreparedById == id || b.ApprovedById == id || b.RejectedById == id || b.DeletedById == id)
            .ToListAsync(ct);
        foreach (var b in boqs)
        {
            if (b.PreparedById == id) b.PreparedByName = name;
            if (b.ApprovedById == id) b.ApprovedByName = name;
            if (b.RejectedById == id) b.RejectedByName = name;
            if (b.DeletedById == id) b.DeletedByName = name;
        }

        var events = await context.BoqApprovalEvents.IgnoreQueryFilters().Where(e => e.ActorId == id).ToListAsync(ct);
        foreach (var e in events) e.ActorName = name;

        if (name is not null)
        {
            var proformas = await context.ProformaInvoices.IgnoreQueryFilters().Where(p => p.IssuedById == id).ToListAsync(ct);
            foreach (var p in proformas) p.IssuedByName = name;
        }
    }
}
