using Jama.Application.MoiJobs.Services;
using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Commands;

public sealed record CompleteJobLine(string? Category, string? IssueType, int Quantity);

/// <summary>The technician finishes his own job: which issues he fixed and how many of each.
/// Prices are looked up here from the MOI price list — the technician never sends one.</summary>
public sealed record CompleteMoiJobCommand(Guid Id, IReadOnlyList<CompleteJobLine>? Lines, string? Notes)
    : IRequest<ApiResult<MoiJobDto>>;

public sealed class CompleteMoiJobCommandValidator : AbstractValidator<CompleteMoiJobCommand>
{
    public CompleteMoiJobCommandValidator()
    {
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: > 0 }).WithMessage("Tick at least one issue you fixed.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Category).NotEmpty();
            line.RuleFor(l => l.IssueType).NotEmpty();
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 99).WithMessage("Quantity must be between 1 and 99.");
        });
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class CompleteMoiJobCommandHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<CompleteMoiJobCommand, ApiResult<MoiJobDto>>
{
    public async Task<ApiResult<MoiJobDto>> Handle(CompleteMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.MoiJobs.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        // Someone else's job looks exactly like a missing one.
        if (job is null || job.AssignedToId != actor.UserId)
            return ApiResult<MoiJobDto>.Failure("Job not found.");
        if (job.Status != "Assigned")
            return ApiResult<MoiJobDto>.Failure("This job is already completed.");

        var catalogue = await context.MoiServiceItems.AsNoTracking().ToListAsync(cancellationToken);

        var lines = new List<MoiJobLine>();
        var order = 0;
        foreach (var asked in request.Lines!)
        {
            var item = catalogue.FirstOrDefault(c =>
                string.Equals(c.Category, asked.Category!.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Name, asked.IssueType!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (item is null)
                return ApiResult<MoiJobDto>.Failure($"\"{asked.IssueType}\" is not on the price list any more. Refresh and try again.");

            lines.Add(new MoiJobLine
            {
                Id = Guid.CreateVersion7(),
                MoiJobId = job.Id,
                Category = item.Category,
                IssueType = item.Name,
                Quantity = asked.Quantity,
                UnitFee = item.Price,
                LineTotal = item.Price * asked.Quantity,
                SortOrder = order++,
            });
        }

        context.MoiJobLines.AddRange(lines);
        job.Status = "Completed";
        job.CompletedAt = DateTime.UtcNow;
        job.WorkTotal = lines.Sum(l => l.LineTotal);
        job.CompletionNotes = MoiJobRules.Clean(request.Notes);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await context.MoiJobs.AsNoTracking().Include(x => x.Lines).FirstAsync(x => x.Id == job.Id, cancellationToken);
        return ApiResult<MoiJobDto>.Success(MoiJobRules.ToDto(saved));
    }
}
