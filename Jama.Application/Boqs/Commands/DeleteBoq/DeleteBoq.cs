using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.DeleteBoq;

public sealed record DeleteBoqCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class DeleteBoqCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteBoqCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteBoqCommand request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (boq is null)
            return ApiResult<Guid>.Failure("BOQ not found.");

        // Sections and their lines go with it through the configured cascade.
        context.Boqs.Remove(boq);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<Guid>.Success(boq.Id);
    }
}
