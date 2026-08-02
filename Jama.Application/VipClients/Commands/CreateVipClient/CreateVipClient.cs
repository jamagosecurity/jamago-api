using Jama.Application.Auth;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients.Commands.CreateVipClient;

public sealed record CreateVipClientCommand : IRequest<ApiResult<Guid>>
{
    public string? ClientName { get; init; }
    public string? ProjectName { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
    /// <summary>Optional. Defaults to "{ClientName} - {ProjectName}".</summary>
    public string? FolderName { get; init; }
}

public sealed class CreateVipClientCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser)
    : IRequestHandler<CreateVipClientCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        CreateVipClientCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email!.Trim().ToLowerInvariant();
        if (await context.AdminUsers.AnyAsync(u => u.Email == email, cancellationToken))
            return ApiResult<Guid>.Failure("An account with this email already exists.");

        var clientName = request.ClientName!.Trim();
        var projectName = request.ProjectName!.Trim();

        var account = new AdminUser
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            FullName = clientName,
            Role = Roles.Client,
            IsActive = true,
        };
        account.PasswordHash = passwordHasher.Hash(account, request.Password!);

        var vipClient = new VipClient
        {
            Id = Guid.CreateVersion7(),
            ClientName = clientName,
            ProjectName = projectName,
            FolderName = string.IsNullOrWhiteSpace(request.FolderName)
                ? VipFolders.BuildFolderName(clientName, projectName)
                : request.FolderName.Trim(),
            AdminUserId = account.Id,
            Account = account,
            CreatedById = currentUser.UserId,
        };

        // Seeded in the same transaction so a project is never in a state where
        // it exists but has nowhere to put documents.
        var order = 0;
        foreach (var (kind, name) in VipFolders.Defaults)
        {
            vipClient.Folders.Add(new VipClientFolder
            {
                Id = Guid.CreateVersion7(),
                VipClientId = vipClient.Id,
                Kind = kind,
                Name = name,
                DisplayOrder = order++,
            });
        }

        context.AdminUsers.Add(account);
        context.VipClients.Add(vipClient);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<Guid>.Success(vipClient.Id);
    }
}
