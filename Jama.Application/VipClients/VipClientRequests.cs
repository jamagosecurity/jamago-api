using FluentValidation;
using Jama.Application.Auth;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients;

/// <summary>Archived projects are excluded by default; true lists only those.</summary>
public sealed record GetVipClientsQuery(bool Archived = false)
    : IRequest<ApiResult<IReadOnlyList<VipClientListItemDto>>>;

public sealed record GetVipClientQuery(Guid Id) : IRequest<ApiResult<VipClientDetailDto>>;

/// <summary>The signed-in client's own project. Resolved from the token, never an id.</summary>
public sealed record GetMyVipProjectQuery : IRequest<ApiResult<VipClientDetailDto>>;

public sealed record CreateVipClientCommand : IRequest<ApiResult<Guid>>
{
    public string? ClientName { get; init; }
    public string? ProjectName { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
    /// <summary>Optional. Defaults to "{ClientName} - {ProjectName}".</summary>
    public string? FolderName { get; init; }
}

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

public sealed record DeleteVipClientCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class CreateVipClientValidator : AbstractValidator<CreateVipClientCommand>
{
    public CreateVipClientValidator()
    {
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProjectName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FolderName).MaximumLength(400);
    }
}

public sealed class UpdateVipClientValidator : AbstractValidator<UpdateVipClientCommand>
{
    public UpdateVipClientValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProjectName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).MinimumLength(8).When(x => !string.IsNullOrWhiteSpace(x.Password));
        RuleFor(x => x.FolderName).MaximumLength(400);
    }
}

internal static class VipClientMapping
{
    public static VipClientDetailDto ToDetail(VipClient entity, IReadOnlyDictionary<Guid, string> uploaderNames) =>
        new(
            entity.Id,
            entity.ClientName,
            entity.ProjectName,
            entity.FolderName,
            entity.Account.Email,
            entity.IsActive,
            entity.Account.IsActive,
            entity.CreatedAt,
            entity.Folders
                .OrderBy(f => f.DisplayOrder)
                .Select(f => new VipClientFolderDto(
                    f.Id,
                    f.Kind,
                    f.Name,
                    f.DisplayOrder,
                    f.Documents
                        .OrderByDescending(d => d.CreatedAt)
                        .Select(d => new VipClientDocumentDto(
                            d.Id,
                            d.FileName,
                            d.ContentType,
                            d.SizeBytes,
                            d.CreatedAt,
                            uploaderNames.GetValueOrDefault(d.UploadedById)))
                        .ToList()))
                .ToList());
}

public sealed class GetVipClientsHandler(IApplicationDbContext context)
    : IRequestHandler<GetVipClientsQuery, ApiResult<IReadOnlyList<VipClientListItemDto>>>
{
    public async Task<ApiResult<IReadOnlyList<VipClientListItemDto>>> Handle(
        GetVipClientsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await context.VipClients
            .AsNoTracking()
            .Where(x => x.IsArchived == request.Archived)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new VipClientListItemDto(
                x.Id,
                x.ClientName,
                x.ProjectName,
                x.FolderName,
                x.Account.Email,
                x.IsActive,
                x.Account.IsActive,
                x.Folders.SelectMany(f => f.Documents).Count(),
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return ApiResult<IReadOnlyList<VipClientListItemDto>>.Success(items);
    }
}

public sealed class GetVipClientHandler(IApplicationDbContext context)
    : IRequestHandler<GetVipClientQuery, ApiResult<VipClientDetailDto>>
{
    public async Task<ApiResult<VipClientDetailDto>> Handle(
        GetVipClientQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await VipClientQueries.WithFoldersAsync(context, x => x.Id == request.Id, cancellationToken);
        if (entity is null)
            return ApiResult<VipClientDetailDto>.Failure("VIP client not found.");

        var names = await VipClientQueries.UploaderNamesAsync(context, entity, cancellationToken);
        return ApiResult<VipClientDetailDto>.Success(VipClientMapping.ToDetail(entity, names));
    }
}

public sealed class GetMyVipProjectHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyVipProjectQuery, ApiResult<VipClientDetailDto>>
{
    public async Task<ApiResult<VipClientDetailDto>> Handle(
        GetMyVipProjectQuery request,
        CancellationToken cancellationToken)
    {
        // Scoped by the signed-in account, so a client can only ever resolve
        // their own project — there is no id to tamper with.
        var entity = await VipClientQueries.WithFoldersAsync(
            context, x => x.AdminUserId == currentUser.UserId && !x.IsArchived, cancellationToken);

        if (entity is null)
            return ApiResult<VipClientDetailDto>.Failure("No project is linked to this account.");

        var names = await VipClientQueries.UploaderNamesAsync(context, entity, cancellationToken);
        return ApiResult<VipClientDetailDto>.Success(VipClientMapping.ToDetail(entity, names));
    }
}

internal static class VipClientQueries
{
    public static Task<VipClient?> WithFoldersAsync(
        IApplicationDbContext context,
        System.Linq.Expressions.Expression<Func<VipClient, bool>> predicate,
        CancellationToken cancellationToken) =>
        context.VipClients
            .AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Folders.OrderBy(f => f.DisplayOrder))
                .ThenInclude(f => f.Documents)
            .FirstOrDefaultAsync(predicate, cancellationToken);

    public static async Task<IReadOnlyDictionary<Guid, string>> UploaderNamesAsync(
        IApplicationDbContext context,
        VipClient entity,
        CancellationToken cancellationToken)
    {
        var ids = entity.Folders
            .SelectMany(f => f.Documents)
            .Select(d => d.UploadedById)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await context.AdminUsers.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }
}

public sealed class CreateVipClientHandler(
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

        // The four folders are seeded here so a project is never in a state
        // where it exists but has nowhere to put documents.
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

public sealed class UpdateVipClientHandler(
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

public sealed class DeleteVipClientHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteVipClientCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteVipClientCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.VipClients
            .Include(x => x.Account)
            .Include(x => x.Folders)
                .ThenInclude(f => f.Documents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return ApiResult<Guid>.Failure("VIP client not found.");

        var keys = entity.Folders.SelectMany(f => f.Documents).Select(d => d.StorageKey).ToList();

        // Rows first: if a file delete fails we would rather have an orphaned
        // file on disk than a row pointing at content that no longer exists.
        context.VipClients.Remove(entity);
        context.AdminUsers.Remove(entity.Account);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var key in keys)
            await storage.DeleteAsync(key, cancellationToken);

        return ApiResult<Guid>.Success(entity.Id);
    }
}
