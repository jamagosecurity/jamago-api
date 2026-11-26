using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.UpdateMaintenanceCatalogueItem;

/// <summary>Renames or reprices a service on the Non-MOI price list. Tickets
/// already raised keep the name and price they were written with — each line
/// copies them — so this only changes what new tickets are charged.</summary>
public sealed record UpdateMaintenanceCatalogueItemCommand : IRequest<ApiResult<MaintenanceIssueOptionDto>>
{
    /// <summary>Set from the route by the endpoint.</summary>
    public Guid Id { get; init; }
    public string? Category { get; init; }
    public string? Name { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public decimal Price { get; init; }
}

public sealed class UpdateMaintenanceCatalogueItemCommandValidator : AbstractValidator<UpdateMaintenanceCatalogueItemCommand>
{
    public UpdateMaintenanceCatalogueItemCommandValidator()
    {
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Choose a category, or create one.")
            .MaximumLength(MaintenanceCategories.MaxLength)
            .WithMessage($"A category name must be {MaintenanceCategories.MaxLength} characters or fewer.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the service a name.").MaximumLength(200);
        RuleFor(x => x.DescriptionEn).MaximumLength(500);
        RuleFor(x => x.DescriptionAr).MaximumLength(500);
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0m).WithMessage("A price cannot be negative.")
            .LessThanOrEqualTo(MaintenanceCatalogueRules.PriceMax)
            .WithMessage($"A price must be {MaintenanceCatalogueRules.PriceMax:N2} or less.");
    }
}

public sealed class UpdateMaintenanceCatalogueItemCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<UpdateMaintenanceCatalogueItemCommand, ApiResult<MaintenanceIssueOptionDto>>
{
    public async Task<ApiResult<MaintenanceIssueOptionDto>> Handle(
        UpdateMaintenanceCatalogueItemCommand request, CancellationToken cancellationToken)
    {
        var item = await context.MaintenanceCatalogueItems
            .Include(i => i.Images)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (item is null)
            return ApiResult<MaintenanceIssueOptionDto>.Failure("Service not found.");

        var name = request.Name!.Trim();
        var category = await MaintenanceCatalogueRules.ResolveCategoryAsync(context, request.Category, cancellationToken);
        if (await MaintenanceCatalogueRules.NameTakenAsync(context, category, name, item.Id, cancellationToken))
            return ApiResult<MaintenanceIssueOptionDto>.Failure($"\"{name}\" is already on the list in this category.");

        // Moved to another category: it joins the end of that one's list.
        var categoryChanged = item.Category != category;
        if (categoryChanged)
        {
            var last = await context.MaintenanceCatalogueItems
                .Where(i => i.Category == category)
                .MaxAsync(i => (int?)i.SortOrder, cancellationToken);
            item.Category = category;
            item.SortOrder = (last ?? -1) + 1;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var descriptionEn = MaintenanceCatalogueRules.Clean(request.DescriptionEn);
        var descriptionAr = MaintenanceCatalogueRules.Clean(request.DescriptionAr);
        var detailsChanged = item.Name != name || categoryChanged || item.DescriptionEn != descriptionEn || item.DescriptionAr != descriptionAr;
        var oldPrice = item.Price;

        item.Name = name;
        item.DescriptionEn = descriptionEn;
        item.DescriptionAr = descriptionAr;
        item.UpdatedAt = now;

        // Every change is kept with who made it: the office and the technicians both keep this list up to date.
        if (oldPrice != request.Price)
        {
            item.Price = request.Price;
            item.PriceChangedById = actor.UserId;
            item.PriceChangedByName = actor.DisplayName;
            item.PriceChangedAt = now;
            MaintenanceCatalogueRules.Log(context, item, CatalogueChangeAction.PriceChanged, actor, now, oldPrice, request.Price);
        }
        if (detailsChanged)
            MaintenanceCatalogueRules.Log(context, item, CatalogueChangeAction.Edited, actor, now);

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceIssueOptionDto>.Success(MaintenanceCatalogueRules.ToDto(item));
    }
}
