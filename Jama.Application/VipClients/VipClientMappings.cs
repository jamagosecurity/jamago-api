using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients;

/// <summary>
/// Shared projection and loading for the VIP queries, so the admin detail view
/// and the client portal can never render the same project differently.
/// </summary>
internal static class VipClientMappings
{
    public static VipClientDetailDto ToDetail(
        VipClient entity,
        IReadOnlyDictionary<Guid, string> uploaderNames) =>
        new(
            entity.Id,
            entity.ClientName,
            entity.ProjectName,
            entity.FolderName,
            entity.Account.Email,
            entity.IsActive,
            entity.Account.IsActive,
            entity.CreatedAt,
            entity.Folders
                .OrderBy(f => f.DisplayOrder)
                .Select(f => new VipClientFolderDto(
                    f.Id,
                    f.Kind,
                    f.Name,
                    f.DisplayOrder,
                    f.Documents
                        .OrderByDescending(d => d.CreatedAt)
                        .Select(d => new VipClientDocumentDto(
                            d.Id,
                            d.FileName,
                            d.ContentType,
                            d.SizeBytes,
                            d.CreatedAt,
                            uploaderNames.GetValueOrDefault(d.UploadedById)))
                        .ToList()))
                .ToList());

    public static Task<VipClient?> LoadWithFoldersAsync(
        IApplicationDbContext context,
        System.Linq.Expressions.Expression<Func<VipClient, bool>> predicate,
        CancellationToken cancellationToken) =>
        context.VipClients
            .AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Folders.OrderBy(f => f.DisplayOrder))
                .ThenInclude(f => f.Documents)
            .FirstOrDefaultAsync(predicate, cancellationToken);

    public static async Task<IReadOnlyDictionary<Guid, string>> UploaderNamesAsync(
        IApplicationDbContext context,
        VipClient entity,
        CancellationToken cancellationToken)
    {
        var ids = entity.Folders
            .SelectMany(f => f.Documents)
            .Select(d => d.UploadedById)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await context.AdminUsers.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }
}
