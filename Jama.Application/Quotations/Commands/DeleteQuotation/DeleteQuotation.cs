using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Quotations.Commands.DeleteQuotation;

public sealed record DeleteQuotationCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class DeleteQuotationCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteQuotationCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteQuotationCommand request,
        CancellationToken cancellationToken)
    {
        var quotation = await context.Quotations
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (quotation is null)
            return ApiResult<Guid>.Failure("Quotation not found.");

        // Lines go with it through the cascade configured on the relationship.
        context.Quotations.Remove(quotation);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<Guid>.Success(quotation.Id);
    }
}
