using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Dia;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Commands;

/// <summary>Issues a MOI job to a technician. Gated on maintenance.manage at the endpoint.</summary>
public sealed record CreateMoiJobCommand : IRequest<ApiResult<MoiJobDto>>, IMoiJobRequest
{
    public string? CustomerName { get; init; }
    public string? CustomerNumber { get; init; }
    public string? Location { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public Guid AssignedToId { get; init; }
    public string? CustomerEmail { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
}

public sealed class CreateMoiJobCommandValidator : AbstractValidator<CreateMoiJobCommand>
{
    public CreateMoiJobCommandValidator() => MoiJobRules.AddRulesTo(this);
}

public sealed class CreateMoiJobCommandHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<CreateMoiJobCommand, ApiResult<MoiJobDto>>
{
    public async Task<ApiResult<MoiJobDto>> Handle(CreateMoiJobCommand request, CancellationToken cancellationToken)
    {
        var (technician, error) = await MoiJobRules.FindTechnicianAsync(context, request.AssignedToId, cancellationToken);
        if (technician is null)
            return ApiResult<MoiJobDto>.Failure(error!);

        var job = new MoiJob
        {
            Id = Guid.CreateVersion7(),
            JobNumber = await MoiJobRules.NextNumberAsync(context, cancellationToken),
            CustomerName = request.CustomerName!.Trim(),
            CustomerNumber = request.CustomerNumber!.Trim(),
            Location = request.Location!.Trim(),
            Latitude = SiteCoordinates.Round(request.Latitude),
            Longitude = SiteCoordinates.Round(request.Longitude),
            DescriptionEn = request.DescriptionEn!.Trim(),
            DescriptionAr = MoiJobRules.Clean(request.DescriptionAr),
            AssignedToId = technician.AdminUserId!.Value,
            AssignedToName = technician.FullName,
            CustomerEmail = MoiJobRules.Clean(request.CustomerEmail),
            ScheduledAt = request.ScheduledAt?.UtcDateTime,
            // Who issued it, taken from the token: only this person (or an administrator) can close the deal.
            AssignedById = actor.UserId,
            AssignedByName = actor.DisplayName,
        };

        context.MoiJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<MoiJobDto>.Success(MoiJobRules.ToDto(job));
    }
}

public sealed record UpdateMoiJobCommand : IRequest<ApiResult<MoiJobDto>>, IMoiJobRequest
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
    public string? CustomerEmail { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
}

public sealed class UpdateMoiJobCommandValidator : AbstractValidator<UpdateMoiJobCommand>
{
    public UpdateMoiJobCommandValidator() => MoiJobRules.AddRulesTo(this);
}

public sealed class UpdateMoiJobCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<UpdateMoiJobCommand, ApiResult<MoiJobDto>>
{
    public async Task<ApiResult<MoiJobDto>> Handle(UpdateMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.MoiJobs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null)
            return ApiResult<MoiJobDto>.Failure("Job not found.");
        if (job.Status is "Sent" or "Closed")
            return ApiResult<MoiJobDto>.Failure("This job's bill is with the client, so it cannot be changed.");

        // Re-checked only when the job is being handed to someone else, so a
        // technician who has since left does not block a typo fix.
        if (job.AssignedToId != request.AssignedToId)
        {
            var (technician, error) = await MoiJobRules.FindTechnicianAsync(context, request.AssignedToId, cancellationToken);
            if (technician is null)
                return ApiResult<MoiJobDto>.Failure(error!);
            job.AssignedToId = technician.AdminUserId!.Value;
            job.AssignedToName = technician.FullName;
        }

        job.CustomerName = request.CustomerName!.Trim();
        job.CustomerNumber = request.CustomerNumber!.Trim();
        job.Location = request.Location!.Trim();
        job.Latitude = SiteCoordinates.Round(request.Latitude);
        job.Longitude = SiteCoordinates.Round(request.Longitude);
        job.DescriptionEn = request.DescriptionEn!.Trim();
        job.DescriptionAr = MoiJobRules.Clean(request.DescriptionAr);
        job.CustomerEmail = MoiJobRules.Clean(request.CustomerEmail);
        job.ScheduledAt = request.ScheduledAt?.UtcDateTime;
        job.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<MoiJobDto>.Success(MoiJobRules.ToDto(job));
    }
}

public sealed record DeleteMoiJobCommand(Guid Id) : IRequest<ApiResult<string>>;

public sealed class DeleteMoiJobCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteMoiJobCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(DeleteMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.MoiJobs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null)
            return ApiResult<string>.Failure("Job not found.");
        if (job.Status is "Sent" or "Closed")
            return ApiResult<string>.Failure("This job's bill has been sent to the client, so it cannot be removed.");

        context.MoiJobs.Remove(job);
        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<string>.Success("Job removed.");
    }
}
