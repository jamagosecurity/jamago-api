using Jama.Application.Auth;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients.Commands.UpdateVipClient;

public sealed record UpdateVipClientCommand : IRequest<ApiResult<Guid>>
{
    public Guid Id { get; init; }
    public string? ClientName { get; init; }
    public string? ProjectName { get; init; }
    public string? Email { get; init; }
    /// <summary>Blank keeps the current password.</summary>
    public string? Password { get; init; }
    public string? FolderName { get; init; }
    public bool IsActive { get; init; } = true;
    public bool CanSignIn { get; init; } = true;
}

public sealed class UpdateVipClientCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateVipClientCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        UpdateVipClientCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.VipClients
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return ApiResult<Guid>.Failure("VIP client not found.");

        var email = request.Email!.Trim().ToLowerInvariant();
        if (await context.AdminUsers.AnyAsync(
                u => u.Email == email && u.Id != entity.AdminUserId, cancellationToken))
        {
            return ApiResult<Guid>.Failure("An account with this email already exists.");
        }

        entity.ClientName = request.ClientName!.Trim();
        entity.ProjectName = request.ProjectName!.Trim();
        entity.FolderName = string.IsNullOrWhiteSpace(request.FolderName)
            ? VipFolders.BuildFolderName(entity.ClientName, entity.ProjectName)
            : request.FolderName.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedById = currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.Account.Email = email;
        entity.Account.FullName = entity.ClientName;
        entity.Account.IsActive = request.CanSignIn;
        entity.Account.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
            entity.Account.PasswordHash = passwordHasher.Hash(entity.Account, request.Password);

        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<Guid>.Success(entity.Id);
    }
}
