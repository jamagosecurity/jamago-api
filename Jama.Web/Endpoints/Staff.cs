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

    public async Task<Ok<TypedResult<StaffDto>>> GetStaff(ISender sender, Guid id)
    {
        var result = await sender.Send(new GetStaffByIdQuery { Id = id });
        return TypedResults.Ok(result);
    }

    public async Task<Created<TypedResult<Guid>>> CreateStaff(ISender sender, CreateStaffCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Created($"/{nameof(Staff)}/{result.Data}", result);
    }

    public async Task<TypedResult<string>> UpdateStaff(ISender sender, Guid id, UpdateStaffCommand command)
    {
        if (id != command.Id)
        {
            return TypedResult<string>.BadRequest();
        }

        await sender.Send(command);
        return TypedResult<string>.Success(string.Empty);
    }

    public async Task<TypedResult<string>> DeleteStaff(ISender sender, Guid id)
    {
        await sender.Send(new DeleteStaffCommand { Id = id });
        return TypedResult<string>.Success(string.Empty);
    }
}
