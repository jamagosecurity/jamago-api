using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.Staffs;
using Jama.Application.Staffs.Commands.CreateStaff;
using Jama.Application.Staffs.Commands.DeleteStaff;
using Jama.Application.Staffs.Commands.UpdateStaff;
using Jama.Application.Staffs.Queries.GetActiveStaff;
using Jama.Application.Staffs.Queries.GetAllStaff;
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
            .MapGet(GetActiveStaff)
            .MapGet(GetAllStaff, "all", Roles.Admin)
            .MapGet(GetStaff, "{id}", Roles.Admin)
            .MapPost(CreateStaff, roles: Roles.Admin)
            .MapPut(UpdateStaff, "{id}", Roles.Admin)
            .MapDelete(DeleteStaff, "{id}", Roles.Admin);
    }

    public async Task<Ok<TypedResult<IReadOnlyList<StaffDto>>>> GetActiveStaff(ISender sender)
    {
        var result = await sender.Send(new GetActiveStaffQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<TypedResult<IReadOnlyList<StaffDto>>>> GetAllStaff(ISender sender)
    {
        var result = await sender.Send(new GetAllStaffQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<TypedResult<StaffDto>>, NotFound<TypedResult<StaffDto>>>> GetStaff(
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
        if (id != command.Id)
        {
            return TypedResults.BadRequest(TypedResult<string>.BadRequest());
        }

        var result = await sender.Send(command);
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
