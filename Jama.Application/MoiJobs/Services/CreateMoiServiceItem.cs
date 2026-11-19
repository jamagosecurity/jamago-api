using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Application.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

/// <summary>Adds a service to the MOI price list, at the end of its
/// category. Gated on maintenance.manage at the endpoint.</summary>
public sealed record CreateMoiServiceItemCommand : IRequest<ApiResult<MaintenanceIssueOptionDto>>
{
    public string? Category { get; init; }
    public string? Name { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public decimal Price { get; init; }
}

public sealed class CreateMoiServiceItemCommandValidator : AbstractValidator<CreateMoiServiceItemCommand>
{
    public CreateMoiServiceItemCommandValidator()
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

public sealed class CreateMoiServiceItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateMoiServiceItemCommand, ApiResult<MaintenanceIssueOptionDto>>
{
    public async Task<ApiResult<MaintenanceIssueOptionDto>> Handle(
        CreateMoiServiceItemCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name!.Trim();
        var category = await MoiServiceRules.ResolveCategoryAsync(context, request.Category, cancellationToken);
        if (await MoiServiceRules.NameTakenAsync(context, category, name, null, cancellationToken))
            return ApiResult<MaintenanceIssueOptionDto>.Failure($"\"{name}\" is already on the list in this category.");

        var last = await context.MoiServiceItems
            .Where(i => i.Category == category)
            .MaxAsync(i => (int?)i.SortOrder, cancellationToken);

        var item = new MoiServiceItem
        {
            Id = Guid.CreateVersion7(),
            Category = category,
            Name = name,
            DescriptionEn = MoiServiceRules.Clean(request.DescriptionEn),
            DescriptionAr = MoiServiceRules.Clean(request.DescriptionAr),
            Price = request.Price,
            SortOrder = (last ?? -1) + 1,
        };

        context.MoiServiceItems.Add(item);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceIssueOptionDto>.Success(MoiServiceRules.ToDto(item));
    }
}
