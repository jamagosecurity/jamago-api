using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Staffs;
using Jama.Application.Staffs.Commands.CreateStaff;
using Jama.Application.Staffs.Commands.DeleteStaff;
using Jama.Application.Staffs.Commands.SetStaffPermissions;
using Jama.Application.Staffs.Commands.UpdateMyStaffProfile;
using Jama.Application.Staffs.Commands.UpdateStaff;
using Jama.Application.Staffs.Queries.GetActiveStaff;
using Jama.Application.Staffs.Queries.GetAllStaff;
using Jama.Application.Staffs.Queries.GetMyStaffProfile;
using Jama.Application.Staffs.Queries.GetStaffById;
using Jama.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jama.Web.Endpoints;

public class Staff : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            // Admin-only, deliberately. StaffDto carries no email or account
            // state, but it does list every active employee's name, role and
            // department, and publishing the staff of a security company is a
            // decision the business made once and settled: see "Require admin
            // auth on GET /api/staff to stop public exposure of staff data".
            //
            // The consequence is that the "Our Team" section on the marketing
            // site renders empty for anonymous visitors — that is the accepted
            // cost, not an oversight. Restoring that section needs a separate
            // endpoint exposing only staff explicitly marked as public, rather
            // than reopening this one.
            .MapGet(GetActiveStaff, roles: Roles.Admin)
            .MapGet(GetAllStaff, "all", Roles.Admin)
            .MapGet(GetMyStaffProfile, "me", Roles.Staff)
            .MapGet(GetPermissionCatalogue, "permissions", Roles.Admin)
            .MapGet(GetStaff, "{id}", Roles.Admin)
            .MapPut(SetStaffPermissions, "{id}/permissions", Roles.Admin)
            .MapPost(CreateStaff, roles: Roles.Admin)
            .MapPut(UpdateMyStaffProfile, "me", Roles.Staff)
            .MapPut(UpdateStaff, "{id}", Roles.Admin)
            .MapDelete(DeleteStaff, "{id}", Roles.Admin);
    }

    public async Task<Ok<TypedResult<IReadOnlyList<StaffDto>>>> GetActiveStaff(ISender sender)
    {
        var result = await sender.Send(new GetActiveStaffQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<TypedResult<IReadOnlyList<AdminStaffDto>>>> GetAllStaff(ISender sender)
    {
        var result = await sender.Send(new GetAllStaffQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<TypedResult<AdminStaffDto>>, NotFound<TypedResult<AdminStaffDto>>>> GetStaff(
        ISender sender,
        Guid id)
    {
        var result = await sender.Send(new GetStaffByIdQuery { Id = id });
        if (!result.Succeeded)
        {
            return TypedResults.NotFound(result);
        }

        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<TypedResult<AdminStaffDto>>, UnauthorizedHttpResult, NotFound<TypedResult<AdminStaffDto>>>> GetMyStaffProfile(
        ISender sender,
        ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var result = await sender.Send(new GetMyStaffProfileQuery { UserId = userId });
        if (!result.Succeeded)
        {
            return TypedResults.NotFound(result);
        }

        return TypedResults.Ok(result);
    }

    /// <summary>Every permission an admin can grant, with display copy for the UI.</summary>
    public Ok<TypedResult<PermissionCatalogueDto>> GetPermissionCatalogue() =>
        TypedResults.Ok(TypedResult<PermissionCatalogueDto>.Success(
            new PermissionCatalogueDto(Permissions.All, Permissions.DefaultsByDepartment)));

    /// <summary>Replaces a staff member's granted permissions. Admin only.</summary>
    public async Task<Results<Ok<TypedResult<string>>, BadRequest<TypedResult<string>>, NotFound<TypedResult<string>>>> SetStaffPermissions(
        ISender sender,
        Guid id,
        SetStaffPermissionsCommand command)
    {
        var result = await sender.Send(command with { Id = id });
        if (result.Succeeded)
        {
            return TypedResults.Ok(result);
        }

        return result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase))
            ? TypedResults.NotFound(result)
            : TypedResults.BadRequest(result);
    }

    /// <summary>
    /// Updates the signed-in staff member's own profile. Scoped to safe fields only —
    /// role, department, email and active state stay under admin control.
    /// </summary>
    public async Task<Results<Ok<TypedResult<string>>, BadRequest<TypedResult<string>>, NotFound<TypedResult<string>>>> UpdateMyStaffProfile(
        ISender sender,
        ICurrentUser currentUser,
        UpdateMyStaffProfileCommand command)
    {
        var result = await sender.Send(command with { UserId = currentUser.UserId });
        if (result.Succeeded)
        {
            return TypedResults.Ok(result);
        }

        return result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase))
            ? TypedResults.NotFound(result)
            : TypedResults.BadRequest(result);
    }

    public async Task<Results<Created<TypedResult<string>>, BadRequest<TypedResult<string>>>> CreateStaff(
        ISender sender,
        CreateStaffCommand command)
    {
        var result = await sender.Send(command);
        if (!result.Succeeded || result.Data is null)
        {
            return TypedResults.BadRequest(
                TypedResult<string>.Failure(result.Errors.Length > 0 ? result.Errors : ["Could not create staff member."]));
        }

        var payload = TypedResult<string>.Success(result.Data);
        return TypedResults.Created($"/api/staff/{result.Data}", payload);
    }

    public async Task<Results<Ok<TypedResult<string>>, BadRequest<TypedResult<string>>, NotFound<TypedResult<string>>>> UpdateStaff(
        ISender sender,
        Guid id,
        UpdateStaffCommand command)
    {
        // Route id is the source of truth — body id is optional for the Angular client.
        var result = await sender.Send(command with { Id = id });
        if (!result.Succeeded || result.Data is null)
        {
            var failure = TypedResult<string>.Failure(
                result.Errors.Length > 0 ? result.Errors : ["Could not update staff member."]);

            if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
            {
                return TypedResults.NotFound(failure);
            }

            return TypedResults.BadRequest(failure);
        }

        return TypedResults.Ok(TypedResult<string>.Success(result.Data));
    }

    public async Task<Results<Ok<TypedResult<string>>, NotFound<TypedResult<string>>>> DeleteStaff(
        ISender sender,
        Guid id)
    {
        var result = await sender.Send(new DeleteStaffCommand { Id = id });
        if (!result.Succeeded || result.Data is null)
        {
            return TypedResults.NotFound(
                TypedResult<string>.Failure(result.Errors.Length > 0 ? result.Errors : ["Staff member not found."]));
        }

        return TypedResults.Ok(TypedResult<string>.Success(result.Data));
    }
}
