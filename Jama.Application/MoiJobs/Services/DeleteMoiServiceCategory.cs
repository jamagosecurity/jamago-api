using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

/// <summary>Removes a category admin added, with every service listed under it
/// (and their pictures). Tickets already raised keep their own copy of each
/// line, so they are unaffected. Built-in sections cannot be removed.</summary>
public sealed record DeleteMoiServiceCategoryCommand(string? Name) : IRequest<ApiResult<int>>;

public sealed class DeleteMoiServiceCategoryCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteMoiServiceCategoryCommand, ApiResult<int>>
{
    public async Task<ApiResult<int>> Handle(
        DeleteMoiServiceCategoryCommand request, CancellationToken cancellationToken)
    {
        var known = await context.MoiServiceItems
            .Select(i => i.Category).Distinct().ToListAsync(cancellationToken);
        var name = MoiServiceCategories.Normalize(request.Name, known);
        if (name.Length == 0)
            return ApiResult<int>.Failure("Category not found.");
        if (MoiServiceCategories.IsBuiltIn(name))
            return ApiResult<int>.Failure("The built-in categories cannot be deleted.");

        var items = await context.MoiServiceItems
            .Include(i => i.Images)
            .Where(i => i.Category == name)
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
            return ApiResult<int>.Failure("Category not found.");

        var storageKeys = items.SelectMany(i => i.Images).Select(i => i.StorageKey).ToList();

        context.MoiServiceItems.RemoveRange(items);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var key in storageKeys)
            await storage.DeleteAsync(key, cancellationToken);

        return ApiResult<int>.Success(items.Count);
    }
}
