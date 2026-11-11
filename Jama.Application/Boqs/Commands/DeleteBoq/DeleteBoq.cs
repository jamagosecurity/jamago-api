using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.DeleteBoq;

public sealed record DeleteBoqCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class DeleteBoqCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider timeProvider)
    : IRequestHandler<DeleteBoqCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteBoqCommand request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (boq is null)
            return ApiResult<Guid>.Failure("BOQ not found.");

        // Soft delete: hidden everywhere, kept in the database with who removed it and when. Its sections, lines and
        // approval trail stay as they were, and its number is never reused.
        boq.IsDeleted = true;
        boq.DeletedAt = timeProvider.GetUtcNow().UtcDateTime;
        boq.DeletedById = actor.UserId;
        boq.DeletedByName = actor.DisplayName;
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<Guid>.Success(boq.Id);
    }
}
