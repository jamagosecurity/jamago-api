using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Commands.UpdateCamera;

public sealed record UpdateCameraCommand : IRequest<ApiResult<CameraDto>>, ICameraWrite
{
    /// <summary>Set from the route by the endpoint, so a mismatched body id cannot
    /// redirect the write at another row.</summary>
    public Guid Id { get; init; }

    public string? ItemName { get; init; }
    public string? Brand { get; init; }
    public CameraType Type { get; init; }
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

public sealed class UpdateCameraCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<UpdateCameraCommand, ApiResult<CameraDto>>
{
    public async Task<ApiResult<CameraDto>> Handle(
        UpdateCameraCommand request,
        CancellationToken cancellationToken)
    {
        // Images are included so the response carries the gallery: the client
        // replaces its row with what comes back, and projecting an empty list
        // would make the pictures vanish until the next reload.
        var camera = await context.Cameras
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (camera is null)
            return ApiResult<CameraDto>.Failure("Camera not found.");

        var brand = CameraRules.NormalizeBrand(request.Brand);
        var modelNo = CameraRules.NormalizeModelNo(request.ModelNo);

        // Excludes this row, so saving a line without changing its brand, type or
        // model is not rejected as a duplicate of itself.
        if (await CameraRules.ExistsAsync(context, brand, request.Type, modelNo, camera.Id, cancellationToken))
            return ApiResult<CameraDto>.Failure(CameraRules.DuplicateMessage(brand, request.Type, modelNo));

        CameraWriter.Apply(camera, request);
        camera.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<CameraDto>.Success(CameraMappings.ToDto(camera));
    }
}
