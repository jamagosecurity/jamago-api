using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.SeenBoqNotifications;

/// <summary>
/// Marks everything decided up to now as seen by this administrator.
///
/// Per account, not global: one administrator opening the panel must not clear
/// the badge for another, who has not looked.
/// </summary>
public sealed record SeenBoqNotificationsCommand : IRequest<ApiResult<int>>;

public sealed class SeenBoqNotificationsCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<SeenBoqNotificationsCommand, ApiResult<int>>
{
    public async Task<ApiResult<int>> Handle(
        SeenBoqNotificationsCommand request,
        CancellationToken cancellationToken)
    {
        var user = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == actor.UserId, cancellationToken);

        if (user is null)
            return ApiResult<int>.Failure("Account not found.");

        user.NotificationsSeenAt = timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<int>.Success(0);
    }
}
