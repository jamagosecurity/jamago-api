using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Dia;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.NonMoiJobs.Commands;

/// <summary>Issues a Non-MOI job to a technician. Gated on maintenance.manage at the endpoint.</summary>
public sealed record CreateNonMoiJobCommand : IRequest<ApiResult<NonMoiJobDto>>, INonMoiJobRequest
{
    public string? CustomerName { get; init; }
    public string? CustomerNumber { get; init; }
    public string? Location { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public Guid AssignedToId { get; init; }
}

public sealed class CreateNonMoiJobCommandValidator : AbstractValidator<CreateNonMoiJobCommand>
{
    public CreateNonMoiJobCommandValidator() => NonMoiJobRules.AddRulesTo(this);
}

public sealed class CreateNonMoiJobCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateNonMoiJobCommand, ApiResult<NonMoiJobDto>>
{
    public async Task<ApiResult<NonMoiJobDto>> Handle(CreateNonMoiJobCommand request, CancellationToken cancellationToken)
    {
        var (technician, error) = await NonMoiJobRules.FindTechnicianAsync(context, request.AssignedToId, cancellationToken);
        if (technician is null)
            return ApiResult<NonMoiJobDto>.Failure(error!);

        var job = new NonMoiJob
        {
            Id = Guid.CreateVersion7(),
            JobNumber = await NonMoiJobRules.NextNumberAsync(context, cancellationToken),
            CustomerName = request.CustomerName!.Trim(),
            CustomerNumber = request.CustomerNumber!.Trim(),
            Location = request.Location!.Trim(),
            Latitude = SiteCoordinates.Round(request.Latitude),
            Longitude = SiteCoordinates.Round(request.Longitude),
            DescriptionEn = request.DescriptionEn!.Trim(),
            DescriptionAr = NonMoiJobRules.Clean(request.DescriptionAr),
            AssignedToId = technician.AdminUserId!.Value,
            AssignedToName = technician.FullName,
        };

        context.NonMoiJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<NonMoiJobDto>.Success(NonMoiJobRules.ToDto(job));
    }
}

public sealed record UpdateNonMoiJobCommand : IRequest<ApiResult<NonMoiJobDto>>, INonMoiJobRequest
{
    /// <summary>Set from the route by the endpoint.</summary>
    public Guid Id { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerNumber { get; init; }
    public string? Location { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public Guid AssignedToId { get; init; }
}

public sealed class UpdateNonMoiJobCommandValidator : AbstractValidator<UpdateNonMoiJobCommand>
{
    public UpdateNonMoiJobCommandValidator() => NonMoiJobRules.AddRulesTo(this);
}

public sealed class UpdateNonMoiJobCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<UpdateNonMoiJobCommand, ApiResult<NonMoiJobDto>>
{
    public async Task<ApiResult<NonMoiJobDto>> Handle(UpdateNonMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.NonMoiJobs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null)
            return ApiResult<NonMoiJobDto>.Failure("Job not found.");

        // Re-checked only when the job is being handed to someone else, so a
        // technician who has since left does not block a typo fix.
        if (job.AssignedToId != request.AssignedToId)
        {
            var (technician, error) = await NonMoiJobRules.FindTechnicianAsync(context, request.AssignedToId, cancellationToken);
            if (technician is null)
                return ApiResult<NonMoiJobDto>.Failure(error!);
            job.AssignedToId = technician.AdminUserId!.Value;
            job.AssignedToName = technician.FullName;
        }

        job.CustomerName = request.CustomerName!.Trim();
        job.CustomerNumber = request.CustomerNumber!.Trim();
        job.Location = request.Location!.Trim();
        job.Latitude = SiteCoordinates.Round(request.Latitude);
        job.Longitude = SiteCoordinates.Round(request.Longitude);
        job.DescriptionEn = request.DescriptionEn!.Trim();
        job.DescriptionAr = NonMoiJobRules.Clean(request.DescriptionAr);
        job.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<NonMoiJobDto>.Success(NonMoiJobRules.ToDto(job));
    }
}

public sealed record DeleteNonMoiJobCommand(Guid Id) : IRequest<ApiResult<string>>;

public sealed class DeleteNonMoiJobCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteNonMoiJobCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(DeleteNonMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.NonMoiJobs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null)
            return ApiResult<string>.Failure("Job not found.");

        context.NonMoiJobs.Remove(job);
        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<string>.Success("Job removed.");
    }
}
