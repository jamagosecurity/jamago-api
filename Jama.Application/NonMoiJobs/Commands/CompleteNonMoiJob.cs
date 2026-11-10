using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.NonMoiJobs.Commands;

public sealed record CompleteJobLine(string? Category, string? IssueType, int Quantity);

/// <summary>The technician finishes his own job: which issues he fixed and how many of each.
/// Prices are looked up here from the Non-MOI price list — the technician never sends one.</summary>
public sealed record CompleteNonMoiJobCommand(Guid Id, IReadOnlyList<CompleteJobLine>? Lines, string? Notes)
    : IRequest<ApiResult<NonMoiJobDto>>;

public sealed class CompleteNonMoiJobCommandValidator : AbstractValidator<CompleteNonMoiJobCommand>
{
    public CompleteNonMoiJobCommandValidator()
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

public sealed class CompleteNonMoiJobCommandHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<CompleteNonMoiJobCommand, ApiResult<NonMoiJobDto>>
{
    public async Task<ApiResult<NonMoiJobDto>> Handle(CompleteNonMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.NonMoiJobs.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        // Someone else's job looks exactly like a missing one.
        if (job is null || job.AssignedToId != actor.UserId)
            return ApiResult<NonMoiJobDto>.Failure("Job not found.");
        if (job.Status != "Assigned")
            return ApiResult<NonMoiJobDto>.Failure("This job is already completed.");

        var catalogue = await context.MaintenanceCatalogueItems.AsNoTracking().ToListAsync(cancellationToken);

        var lines = new List<NonMoiJobLine>();
        var order = 0;
        foreach (var asked in request.Lines!)
        {
            var item = catalogue.FirstOrDefault(c =>
                string.Equals(c.Category, asked.Category!.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Name, asked.IssueType!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (item is null)
                return ApiResult<NonMoiJobDto>.Failure($"\"{asked.IssueType}\" is not on the price list any more. Refresh and try again.");

            lines.Add(new NonMoiJobLine
            {
                Id = Guid.CreateVersion7(),
                NonMoiJobId = job.Id,
                Category = item.Category,
                IssueType = item.Name,
                Quantity = asked.Quantity,
                UnitFee = item.Price,
                LineTotal = item.Price * asked.Quantity,
                SortOrder = order++,
            });
        }

        context.NonMoiJobLines.AddRange(lines);
        job.Status = "Completed";
        job.CompletedAt = DateTime.UtcNow;
        job.WorkTotal = lines.Sum(l => l.LineTotal);
        job.CompletionNotes = NonMoiJobRules.Clean(request.Notes);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines).FirstAsync(x => x.Id == job.Id, cancellationToken);
        return ApiResult<NonMoiJobDto>.Success(NonMoiJobRules.ToDto(saved));
    }
}
