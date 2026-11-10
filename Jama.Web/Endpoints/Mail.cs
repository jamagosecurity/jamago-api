using Jama.Application.Common;
using Jama.Application.Mail.Commands;
using Jama.Application.Mail.Queries;
using Jama.Web.Infrastructure;
using MediatR;
using AppRoles = Jama.Application.Common.Roles;

namespace Jama.Web.Endpoints;

/// <summary>Company mail. Reading takes mail.read (admins hold it); sending stays administrators only.</summary>
public sealed class Mail : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetMessages, "messages", permission: Permissions.MailRead)
            .MapGet(GetMessage, "messages/{id:guid}", permission: Permissions.MailRead)
            .MapGet(GetAccounts, "accounts", permission: Permissions.MailRead)
            .MapPost(Send, "send", AppRoles.Admin);
    }

    public async Task<IResult> GetMessages(ISender sender, CancellationToken ct,
        string? account = null, string? folder = null, string? search = null, int page = 1, int pageSize = 50) =>
        Results.Ok(await sender.Send(new GetMailMessagesQuery(account, folder, search, page, pageSize), ct));

    public async Task<IResult> GetMessage(ISender sender, Guid id, CancellationToken ct)
    {
        var r = await sender.Send(new GetMailMessageQuery(id), ct);
        return r.Succeeded ? Results.Ok(r) : Results.NotFound(r);
    }

    public async Task<IResult> Send(ISender sender, SendMailCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetAccounts(ISender sender, CancellationToken ct) =>
        Results.Ok(await sender.Send(new GetMailAccountsQuery(), ct));
}
