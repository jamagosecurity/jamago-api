using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<TypedResult<UserSummaryDto>>
{
    public Guid UserId { get; init; }
}

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, TypedResult<UserSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCurrentUserQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<UserSummaryDto>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return TypedResult<UserSummaryDto>.Failure("User not found.");
        }

        return TypedResult<UserSummaryDto>.Success(
            new UserSummaryDto(user.Id, user.Email, user.FullName, user.Role));
    }
}
