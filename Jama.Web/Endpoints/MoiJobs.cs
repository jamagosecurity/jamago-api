using Jama.Application.Common;
using Jama.Application.MoiJobs;
using Jama.Application.MoiJobs.Commands;
using Jama.Application.MoiJobs.Queries;
using Jama.Application.Common.Models;
using Jama.Application.MoiJobs.Services;
using Jama.Web.Infrastructure;
using MediatR;
using AppRoles = Jama.Application.Common.Roles;

namespace Jama.Web.Endpoints;

/// <summary>
/// MOI jobs admin issues to a technician. Managing them takes
/// moijob.manage; a technician reads
/// only the jobs assigned to them.
/// </summary>
public sealed record MoiPublicRespondBody(string? Decision, string? Message, string? Name);

public sealed record MoiCloseJobBody(decimal Discount, string? Email, bool SendEmail, bool SendWhatsApp, string? MailboxKey);

public sealed record MoiCompleteJobBody(IReadOnlyList<CompleteJobLine>? Lines, string? Notes);

public sealed class MoiJobs : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetAll, permission: Permissions.MoiJobManage)
            .MapGet(GetMine, "mine", AppRoles.Technician, permission: Permissions.MoiJobWork)
            .MapPost(Complete, "{id:guid}/complete", AppRoles.Technician, permission: Permissions.MoiJobWork)
            .MapPost(Close, "{id:guid}/close", permission: Permissions.MoiJobManage)
            .MapGet(GetBill, "{id:guid}/bill", permission: Permissions.MoiJobManage)
            .MapGet(GetMailboxes, "send-options", permission: Permissions.MoiJobManage)
            .MapGet(GetAwaitingClose, "awaiting-close", permission: Permissions.MoiJobManage)
            // The client's own link: no sign-in, but the unguessable token in it is the key.
            .MapGet(GetBillByToken, "bill/{token}")
            // The client's page and answer. Anonymous: the secret token in the link is the key.
            .MapGet(GetPublicBill, "public/{token}")
            .MapPost(RespondPublic, "public/{token}/respond", rateLimitPolicy: RateLimitPolicies.JobBillResponse)
            .MapGet(GetTechnicians, "technicians", permission: Permissions.MoiJobManage)
            // Money received: cash collected or an online transfer with its receipt.
            .MapPost(AddPayment, "{id:guid}/payments", permission: Permissions.MoiJobManage, allowFileUpload: true)
            .MapGet(GetPaymentReceipt, "payments/{paymentId:guid}/receipt", permission: Permissions.MoiJobManage)
            .MapDelete(DeletePayment, "payments/{paymentId:guid}", permission: AuthorizationPolicies.SuperAdmin)
            .MapGet(GetCollections, "collections", permission: Permissions.MoiJobManage)
            // The MOI price list — services the technician ticks, with prices and pictures.
            .MapGet(GetServices, "services", requireAuthorization: true)
            .MapGet(GetServiceImage, "services/images/{imageId:guid}")
            .MapPost(CreateService, "services", permission: Permissions.MoiJobManage)
            .MapPut(UpdateService, "services/{id:guid}", permission: Permissions.MoiJobManage)
            .MapDelete(DeleteService, "services/{id:guid}", permission: Permissions.MoiJobManage)
            .MapPost(UploadServiceImage, "services/{id:guid}/images", permission: Permissions.MoiJobManage, allowFileUpload: true)
            .MapDelete(DeleteServiceImage, "services/images/{imageId:guid}", permission: Permissions.MoiJobManage)
            .MapPost(RenameServiceCategory, "services/categories/rename", permission: Permissions.MoiJobManage)
            .MapDelete(DeleteServiceCategory, "services/categories", permission: Permissions.MoiJobManage)
            .MapPost(Create, permission: Permissions.MoiJobManage)
            .MapPut(Update, "{id:guid}", permission: Permissions.MoiJobManage)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.MoiJobManage);
    }

    public async Task<IResult> GetAll(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetMoiJobsQuery(), cancellationToken));

    public async Task<IResult> GetMine(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetMyMoiJobsQuery(), cancellationToken));

    public async Task<IResult> GetTechnicians(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetJobTechniciansQuery(), cancellationToken));

    public async Task<IResult> Complete(
        ISender sender, Guid id, MoiCompleteJobBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CompleteMoiJobCommand(id, body.Lines, body.Notes), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Close(
        ISender sender, Guid id, MoiCloseJobBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CloseMoiJobCommand(id, body.Discount, body.Email, body.SendEmail, body.SendWhatsApp, body.MailboxKey), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetBill(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMoiJobBillQuery(id), cancellationToken);
        return result.Succeeded ? Results.File(result.Data!.Content, "application/pdf", result.Data.FileName) : Results.NotFound(result);
    }

    public async Task<IResult> GetBillByToken(ISender sender, string token, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMoiJobBillByTokenQuery(token), cancellationToken);
        return result.Succeeded ? Results.File(result.Data!.Content, "application/pdf", result.Data.FileName) : Results.NotFound();
    }

    public async Task<IResult> GetAwaitingClose(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetJobsAwaitingCloseQuery(), cancellationToken));

    public async Task<IResult> GetPublicBill(ISender sender, string token, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublicJobBillQuery(token), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> RespondPublic(ISender sender, string token, MoiPublicRespondBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RespondToMoiJobCommand(token, body.Decision, body.Message, body.Name), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetMailboxes(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetBillMailboxesQuery(), cancellationToken));

    public async Task<IResult> Create(ISender sender, CreateMoiJobCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender, Guid id, UpdateMoiJobCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMoiJobCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> AddPayment(
        ISender sender, Guid id, [Microsoft.AspNetCore.Mvc.FromForm] string method, [Microsoft.AspNetCore.Mvc.FromForm] decimal amount,
        [Microsoft.AspNetCore.Mvc.FromForm] string? receivedOn, [Microsoft.AspNetCore.Mvc.FromForm] Guid? collectedById,
        [Microsoft.AspNetCore.Mvc.FromForm] string? note, IFormFile? receipt, CancellationToken cancellationToken)
    {
        DateOnly? day = DateOnly.TryParseExact(receivedOn ?? "", "yyyy-MM-dd", out var d) ? d : null;
        await using var stream = receipt?.OpenReadStream();
        var result = await sender.Send(new AddMoiJobPaymentCommand
        {
            JobId = id, Method = method, Amount = amount, ReceivedOn = day, CollectedById = collectedById, Note = note,
            ReceiptFileName = receipt?.FileName, ReceiptContentType = receipt?.ContentType, ReceiptSize = receipt?.Length ?? 0,
            Receipt = stream,
        }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetPaymentReceipt(ISender sender, Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMoiJobPaymentReceiptQuery(paymentId), cancellationToken);
        return result.Succeeded ? Results.File(result.Data!.Content, result.Data.ContentType, result.Data.FileName) : Results.NotFound(result);
    }

    public async Task<IResult> DeletePayment(ISender sender, Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMoiJobPaymentCommand(paymentId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetCollections(ISender sender, CancellationToken cancellationToken, string? from = null, string? to = null)
    {
        static DateOnly? Day(string? v) => DateOnly.TryParseExact(v ?? "", "yyyy-MM-dd", out var d) ? d : null;
        var result = await sender.Send(new GetMoiCollectionsQuery(Day(from), Day(to)), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    // ===== MOI price list =====

    public async Task<IResult> GetServices(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetMoiServiceCatalogueQuery(), cancellationToken));

    public async Task<IResult> CreateService(ISender sender, CreateMoiServiceItemCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> UpdateService(ISender sender, Guid id, UpdateMoiServiceItemCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> DeleteService(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMoiServiceItemCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> UploadServiceImage(ISender sender, Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(ApiResult<string>.Failure("An image is required."));
        await using var stream = file.OpenReadStream();
        var result = await sender.Send(new UploadMoiServiceImageCommand
        {
            ItemId = id, FileName = file.FileName, ContentType = file.ContentType, SizeBytes = file.Length, Content = stream,
        }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetServiceImage(ISender sender, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMoiServiceImageQuery(imageId), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(ApiResult<string>.Failure(result.Errors));
        return Results.File(result.Data.Content, result.Data.ContentType);
    }

    public async Task<IResult> DeleteServiceImage(ISender sender, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMoiServiceImageCommand(imageId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> RenameServiceCategory(ISender sender, RenameMoiServiceCategoryCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> DeleteServiceCategory(ISender sender, string name, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMoiServiceCategoryCommand(name), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }
}
