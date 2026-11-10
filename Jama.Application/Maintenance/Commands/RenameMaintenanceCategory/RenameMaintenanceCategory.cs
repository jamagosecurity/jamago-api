using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.RenameMaintenanceCategory;

/// <summary>Renames a category admin added. Its services and the lines of
/// tickets already raised move with it, so an old ticket reads under the new
/// name rather than a section nobody can see on the price list. Built-in
/// sections cannot be renamed. Gated on maintenance.manage at the endpoint.</summary>
public sealed record RenameMaintenanceCategoryCommand : IRequest<ApiResult<string>>
{
    public string? From { get; init; }
    public string? To { get; init; }
}

public sealed class RenameMaintenanceCategoryCommandValidator : AbstractValidator<RenameMaintenanceCategoryCommand>
{
    public RenameMaintenanceCategoryCommandValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Give the category a name.")
            .MaximumLength(MaintenanceCategories.MaxLength)
            .WithMessage($"A category name must be {MaintenanceCategories.MaxLength} characters or fewer.");
    }
}

public sealed class RenameMaintenanceCategoryCommandHandler(IApplicationDbContext context)
    : IRequestHandler<RenameMaintenanceCategoryCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(
        RenameMaintenanceCategoryCommand request, CancellationToken cancellationToken)
    {
        var known = await context.MaintenanceCatalogueItems
            .Select(i => i.Category).Distinct().ToListAsync(cancellationToken);
        var from = MaintenanceCategories.Normalize(request.From, known);
        if (MaintenanceCategories.IsBuiltIn(from))
            return ApiResult<string>.Failure("The built-in categories cannot be renamed.");

        var items = await context.MaintenanceCatalogueItems
            .Where(i => i.Category == from).ToListAsync(cancellationToken);
        if (items.Count == 0)
            return ApiResult<string>.Failure("Category not found.");

        var to = MaintenanceCategories.Normalize(request.To, known);

        // Same name in another case is a re-spelling of this category, which is
        // fine; any other existing name — a built-in's, or another added one —
        // would merge two sections, which is not what a rename means.
        var isRespelling = string.Equals(to, from, StringComparison.OrdinalIgnoreCase);
        if (!isRespelling && (MaintenanceCategories.IsBuiltIn(to)
            || known.Any(k => string.Equals(k, to, StringComparison.OrdinalIgnoreCase))))
            return ApiResult<string>.Failure($"A category called \"{to}\" already exists.");
        if (isRespelling)
            to = request.To!.Trim();

        foreach (var item in items)
            item.Category = to;

        var lines = await context.MaintenanceRequestLines
            .Where(l => l.Category == from).ToListAsync(cancellationToken);
        foreach (var line in lines)
            line.Category = to;

        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<string>.Success(to);
    }
}
