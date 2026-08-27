using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;

namespace Jama.Application.Quotations.Commands.CreateQuotation;

public sealed record CreateQuotationCommand : IRequest<ApiResult<QuotationDto>>, IQuotationWrite
{
    public string? CustomerName { get; init; }
    public string? CustomerCompany { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }
    public string? CustomerAddress { get; init; }
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public QuotationStatus Status { get; init; } = QuotationStatus.Draft;
    public string? Notes { get; init; }
    public string? Terms { get; init; }
    public IReadOnlyList<QuotationLineInput> Lines { get; init; } = [];
}

public sealed class CreateQuotationCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<CreateQuotationCommand, ApiResult<QuotationDto>>
{
    public async Task<ApiResult<QuotationDto>> Handle(
        CreateQuotationCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var quotation = new Quotation
        {
            Id = Guid.CreateVersion7(),
            QuoteNumber = await QuoteNumbers.NextAsync(context, now.Year, cancellationToken),
            CreatedAt = now,
        };

        QuotationWriter.Apply(quotation, request, timeProvider);

        context.Quotations.Add(quotation);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<QuotationDto>.Success(QuotationMappings.ToDto(quotation));
    }
}
