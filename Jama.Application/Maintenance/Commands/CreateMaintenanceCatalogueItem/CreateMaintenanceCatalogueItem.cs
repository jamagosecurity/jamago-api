using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.CreateMaintenanceCatalogueItem;

/// <summary>Adds a service to the Non-MOI price list, at the end of its category — live at once for every job.
/// The office or any Non-MOI technician may add one (AuthorizationPolicies.NonMoiCatalogueEdit); who added it is
/// kept on the item and in the change log.</summary>
public sealed record CreateMaintenanceCatalogueItemCommand : IRequest<ApiResult<MaintenanceIssueOptionDto>>
{
    public string? Category { get; init; }
    public string? Name { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public decimal Price { get; init; }
}

public sealed class CreateMaintenanceCatalogueItemCommandValidator : AbstractValidator<CreateMaintenanceCatalogueItemCommand>
{
    public CreateMaintenanceCatalogueItemCommandValidator()
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

public sealed class CreateMaintenanceCatalogueItemCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<CreateMaintenanceCatalogueItemCommand, ApiResult<MaintenanceIssueOptionDto>>
{
    public async Task<ApiResult<MaintenanceIssueOptionDto>> Handle(
        CreateMaintenanceCatalogueItemCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name!.Trim();
        var category = await MaintenanceCatalogueRules.ResolveCategoryAsync(context, request.Category, cancellationToken);
        if (await MaintenanceCatalogueRules.NameTakenAsync(context, category, name, null, cancellationToken))
            return ApiResult<MaintenanceIssueOptionDto>.Failure($"\"{name}\" is already on the list in this category.");

        var last = await context.MaintenanceCatalogueItems
            .Where(i => i.Category == category)
            .MaxAsync(i => (int?)i.SortOrder, cancellationToken);

        var item = new MaintenanceCatalogueItem
        {
            Id = Guid.CreateVersion7(),
            Category = category,
            Name = name,
            DescriptionEn = MaintenanceCatalogueRules.Clean(request.DescriptionEn),
            DescriptionAr = MaintenanceCatalogueRules.Clean(request.DescriptionAr),
            Price = request.Price,
            SortOrder = (last ?? -1) + 1,
            AddedById = actor.UserId,
            AddedByName = actor.DisplayName,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };

        context.MaintenanceCatalogueItems.Add(item);
        MaintenanceCatalogueRules.Log(context, item, CatalogueChangeAction.Added, actor, item.CreatedAt, newPrice: item.Price);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceIssueOptionDto>.Success(MaintenanceCatalogueRules.ToDto(item));
    }
}
