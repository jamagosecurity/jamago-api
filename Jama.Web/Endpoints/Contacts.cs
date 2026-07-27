using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.Contacts;
using Jama.Application.Contacts.Commands.CreateContactSubmission;
using Jama.Application.Contacts.Queries.GetContactSubmissions;
using Jama.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jama.Web.Endpoints;

public class Contacts : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            // Admins hold every permission implicitly, so this stays open to them
            // and additionally to staff an admin has granted contact.view.
            .MapGet(GetContacts, permission: Permissions.ContactView)
            .MapPost(CreateContact);
    }

    public async Task<Ok<TypedResult<IReadOnlyList<ContactSubmissionDto>>>> GetContacts(ISender sender)
    {
        var result = await sender.Send(new GetContactSubmissionsQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Created<TypedResult<ContactSubmissionDto>>> CreateContact(
        ISender sender,
        CreateContactSubmissionCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Created($"/{nameof(Contacts)}/{result.Data?.Id}", result);
    }
}
