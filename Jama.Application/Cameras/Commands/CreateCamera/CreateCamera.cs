using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;

namespace Jama.Application.Cameras.Commands.CreateCamera;

public sealed record CreateCameraCommand : IRequest<ApiResult<CameraDto>>, ICameraWrite
{
    public string? ItemName { get; init; }
    public string? Brand { get; init; }
    public string? Type { get; init; }
    public string? ModelNo { get; init; }
    public ProductCategory Category { get; init; }
    public string? SearchKey { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }

    public decimal? SupplierCost { get; init; }
    public decimal? Margin { get; init; }
    public decimal? Rate { get; init; }
    public decimal? Discount { get; init; }
    public int Quantity { get; init; }
    public int? LowStock { get; init; }
    public string? HsnCode { get; init; }
    public UnitOfMeasurement Uom { get; init; }
    public CameraResolution Resolution { get; init; }
    public decimal? BitrateMbps { get; init; }
    public decimal? TaxRate { get; init; }
    public ItemType ItemType { get; init; }

    public int? WarrantyValue { get; init; }
    public WarrantyUnit? WarrantyUnit { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateCameraCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<CreateCameraCommand, ApiResult<CameraDto>>
{
    public async Task<ApiResult<CameraDto>> Handle(
        CreateCameraCommand request,
        CancellationToken cancellationToken)
    {
        var brand = CameraRules.NormalizeBrand(request.Brand);
        var modelNo = CameraRules.NormalizeModelNo(request.ModelNo);

        if (await CameraRules.ExistsAsync(context, brand, request.Type, modelNo, null, cancellationToken))
            return ApiResult<CameraDto>.Failure(CameraRules.DuplicateMessage(brand, request.Type, modelNo));

        var camera = new Camera
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        CameraWriter.Apply(camera, request);

        context.Cameras.Add(camera);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<CameraDto>.Success(CameraMappings.ToDto(camera));
    }
}
