using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

/// <summary>Renames or reprices a service on the MOI price list. Tickets
/// already raised keep the name and price they were written with — each line
/// copies them — so this only changes what new tickets are charged.</summary>
public sealed record UpdateMoiServiceItemCommand : IRequest<ApiResult<MaintenanceIssueOptionDto>>
{
    /// <summary>Set from the route by the endpoint.</summary>
    public Guid Id { get; init; }
    public string? Category { get; init; }
    public string? Name { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public decimal Price { get; init; }
}

public sealed class UpdateMoiServiceItemCommandValidator : AbstractValidator<UpdateMoiServiceItemCommand>
{
    public UpdateMoiServiceItemCommandValidator()
    {
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Choose a category, or create one.")
            .MaximumLength(MoiServiceCategories.MaxLength)
            .WithMessage($"A category name must be {MoiServiceCategories.MaxLength} characters or fewer.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the service a name.").MaximumLength(200);
        RuleFor(x => x.DescriptionEn).MaximumLength(500);
        RuleFor(x => x.DescriptionAr).MaximumLength(500);
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0m).WithMessage("A price cannot be negative.")
            .LessThanOrEqualTo(MoiServiceRules.PriceMax)
            .WithMessage($"A price must be {MoiServiceRules.PriceMax:N2} or less.");
    }
}

public sealed class UpdateMoiServiceItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateMoiServiceItemCommand, ApiResult<MaintenanceIssueOptionDto>>
{
    public async Task<ApiResult<MaintenanceIssueOptionDto>> Handle(
        UpdateMoiServiceItemCommand request, CancellationToken cancellationToken)
    {
        var item = await context.MoiServiceItems
            .Include(i => i.Images)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (item is null)
            return ApiResult<MaintenanceIssueOptionDto>.Failure("Service not found.");

        var name = request.Name!.Trim();
        var category = await MoiServiceRules.ResolveCategoryAsync(context, request.Category, cancellationToken);
        if (await MoiServiceRules.NameTakenAsync(context, category, name, item.Id, cancellationToken))
            return ApiResult<MaintenanceIssueOptionDto>.Failure($"\"{name}\" is already on the list in this category.");

        // Moved to another category: it joins the end of that one's list.
        if (item.Category != category)
        {
            var last = await context.MoiServiceItems
                .Where(i => i.Category == category)
                .MaxAsync(i => (int?)i.SortOrder, cancellationToken);
            item.Category = category;
            item.SortOrder = (last ?? -1) + 1;
        }

        item.Name = name;
        item.DescriptionEn = MoiServiceRules.Clean(request.DescriptionEn);
        item.DescriptionAr = MoiServiceRules.Clean(request.DescriptionAr);
        item.Price = request.Price;
        item.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceIssueOptionDto>.Success(MoiServiceRules.ToDto(item));
    }
}
