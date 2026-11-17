using Jama.Application.Common;
using Jama.Application.NonMoiJobs;
using Jama.Application.NonMoiJobs.Commands;
using Jama.Application.NonMoiJobs.Queries;
using Jama.Web.Infrastructure;
using MediatR;
using AppRoles = Jama.Application.Common.Roles;

namespace Jama.Web.Endpoints;

/// <summary>
/// Non-MOI jobs admin issues to a technician. Managing them takes
/// nonmoijob.manage (which maintenance.manage implies); a technician reads
/// only the jobs assigned to them.
/// </summary>
public sealed record PublicRespondBody(string? Decision, string? Message, string? Name);

public sealed record CloseJobBody(decimal Discount, string? Email, bool SendEmail, bool SendWhatsApp, string? MailboxKey);

public sealed record CompleteJobBody(IReadOnlyList<CompleteJobLine>? Lines, string? Notes);

public sealed class NonMoiJobs : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetAll, permission: Permissions.NonMoiJobManage)
            .MapGet(GetMine, "mine", AppRoles.Technician, permission: Permissions.NonMoiJobWork)
            .MapPost(Complete, "{id:guid}/complete", AppRoles.Technician, permission: Permissions.NonMoiJobWork)
            .MapPost(Close, "{id:guid}/close", permission: Permissions.NonMoiJobManage)
            .MapGet(GetBill, "{id:guid}/bill", permission: Permissions.NonMoiJobManage)
            .MapGet(GetMailboxes, "send-options", permission: Permissions.NonMoiJobManage)
            .MapGet(GetAwaitingClose, "awaiting-close", permission: Permissions.NonMoiJobManage)
            // The client's own link: no sign-in, but the unguessable token in it is the key.
            .MapGet(GetBillByToken, "bill/{token}")
            // The client's page and answer. Anonymous: the secret token in the link is the key.
            .MapGet(GetPublicBill, "public/{token}")
            .MapPost(RespondPublic, "public/{token}/respond", rateLimitPolicy: RateLimitPolicies.JobBillResponse)
            .MapGet(GetTechnicians, "technicians", permission: Permissions.NonMoiJobManage)
            // Money received: cash collected or an online transfer with its receipt.
            .MapPost(AddPayment, "{id:guid}/payments", permission: Permissions.NonMoiJobManage, allowFileUpload: true)
            .MapGet(GetPaymentReceipt, "payments/{paymentId:guid}/receipt", permission: Permissions.NonMoiJobManage)
            .MapDelete(DeletePayment, "payments/{paymentId:guid}", permission: AuthorizationPolicies.SuperAdmin)
            .MapGet(GetCollections, "collections", permission: Permissions.NonMoiJobManage)
            .MapPost(Create, permission: Permissions.NonMoiJobManage)
            .MapPut(Update, "{id:guid}", permission: Permissions.NonMoiJobManage)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.NonMoiJobManage);
    }

    public async Task<IResult> GetAll(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetNonMoiJobsQuery(), cancellationToken));

    public async Task<IResult> GetMine(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetMyNonMoiJobsQuery(), cancellationToken));

    public async Task<IResult> GetTechnicians(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetJobTechniciansQuery(), cancellationToken));

    public async Task<IResult> Complete(
        ISender sender, Guid id, CompleteJobBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CompleteNonMoiJobCommand(id, body.Lines, body.Notes), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Close(
        ISender sender, Guid id, CloseJobBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CloseNonMoiJobCommand(id, body.Discount, body.Email, body.SendEmail, body.SendWhatsApp, body.MailboxKey), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetBill(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNonMoiJobBillQuery(id), cancellationToken);
        return result.Succeeded ? Results.File(result.Data!.Content, "application/pdf", result.Data.FileName) : Results.NotFound(result);
    }

    public async Task<IResult> GetBillByToken(ISender sender, string token, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNonMoiJobBillByTokenQuery(token), cancellationToken);
        return result.Succeeded ? Results.File(result.Data!.Content, "application/pdf", result.Data.FileName) : Results.NotFound();
    }

    public async Task<IResult> GetAwaitingClose(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetJobsAwaitingCloseQuery(), cancellationToken));

    public async Task<IResult> GetPublicBill(ISender sender, string token, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublicJobBillQuery(token), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> RespondPublic(ISender sender, string token, PublicRespondBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RespondToNonMoiJobCommand(token, body.Decision, body.Message, body.Name), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetMailboxes(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetBillMailboxesQuery(), cancellationToken));

    public async Task<IResult> Create(ISender sender, CreateNonMoiJobCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender, Guid id, UpdateNonMoiJobCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteNonMoiJobCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> AddPayment(
        ISender sender, Guid id, [Microsoft.AspNetCore.Mvc.FromForm] string method, [Microsoft.AspNetCore.Mvc.FromForm] decimal amount,
        [Microsoft.AspNetCore.Mvc.FromForm] string? receivedOn, [Microsoft.AspNetCore.Mvc.FromForm] Guid? collectedById,
        [Microsoft.AspNetCore.Mvc.FromForm] string? note, IFormFile? receipt, CancellationToken cancellationToken)
    {
        DateOnly? day = DateOnly.TryParseExact(receivedOn ?? "", "yyyy-MM-dd", out var d) ? d : null;
        await using var stream = receipt?.OpenReadStream();
        var result = await sender.Send(new AddNonMoiJobPaymentCommand
        {
            JobId = id, Method = method, Amount = amount, ReceivedOn = day, CollectedById = collectedById, Note = note,
            ReceiptFileName = receipt?.FileName, ReceiptContentType = receipt?.ContentType, ReceiptSize = receipt?.Length ?? 0,
            Receipt = stream,
        }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetPaymentReceipt(ISender sender, Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNonMoiJobPaymentReceiptQuery(paymentId), cancellationToken);
        return result.Succeeded ? Results.File(result.Data!.Content, result.Data.ContentType, result.Data.FileName) : Results.NotFound(result);
    }

    public async Task<IResult> DeletePayment(ISender sender, Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteNonMoiJobPaymentCommand(paymentId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetCollections(ISender sender, CancellationToken cancellationToken, string? from = null, string? to = null)
    {
        static DateOnly? Day(string? v) => DateOnly.TryParseExact(v ?? "", "yyyy-MM-dd", out var d) ? d : null;
        var result = await sender.Send(new GetCollectionsQuery(Day(from), Day(to)), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }
}
