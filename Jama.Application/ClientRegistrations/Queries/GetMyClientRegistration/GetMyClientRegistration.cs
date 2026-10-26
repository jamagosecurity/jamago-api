using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.ClientRegistrations.Queries.GetMyClientRegistration;

/// <summary>The signed-in client's own registration, resolved from the token
/// — never an id, so there is nothing to tamper with. Fails for an account
/// that never registered (a VIP client created by an admin); the portal reads
/// that as "no registration to show", not as an error.</summary>
public sealed record GetMyClientRegistrationQuery : IRequest<ApiResult<MyClientRegistrationDto>>;

public sealed class GetMyClientRegistrationQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyClientRegistrationQuery, ApiResult<MyClientRegistrationDto>>
{
    public async Task<ApiResult<MyClientRegistrationDto>> Handle(
        GetMyClientRegistrationQuery request,
        CancellationToken cancellationToken)
    {
        var registration = await context.ClientRegistrations
            .AsNoTracking()
            .Include(x => x.DiaInspection)
            .FirstOrDefaultAsync(x => x.AdminUserId == currentUser.UserId, cancellationToken);

        if (registration is null)
            return ApiResult<MyClientRegistrationDto>.Failure("No registration is linked to this account.");

        return ApiResult<MyClientRegistrationDto>.Success(new MyClientRegistrationDto(
            registration.FullName,
            registration.Email,
            registration.Mobile,
            registration.SiteLocation,
            registration.Status,
            registration.Category,
            ClientTeams.Describe(registration.Teams),
            registration.DiaInspection?.DiaNumber,
            registration.DiaInspection?.IsActive ?? false,
            registration.CreatedAt,
            registration.ReviewedAt));
    }
}
