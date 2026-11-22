using Jama.Application.CashHandovers;
using Jama.Application.Common;
using Jama.Application.MoiJobs;
using Jama.Application.NonMoiJobs;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

public sealed record ConfirmHandoverBody(string? Kind, Guid? CollectorId, decimal ExpectedAmount, decimal ReceivedAmount, string? Note);

/// <summary>The accountant's panel: cash handed over at the end of the day, and the online transfers. All of it needs accounts.manage.</summary>
public sealed class CashHandovers : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetPending, permission: Permissions.AccountsManage)
            .MapPost(Confirm, permission: Permissions.AccountsManage)
            .MapGet(GetHistory, "history", permission: Permissions.AccountsManage)
            .MapGet(GetOnline, "online", permission: Permissions.AccountsManage)
            .MapGet(GetOnlineReceipt, "online/{kind}/{paymentId:guid}/receipt", permission: Permissions.AccountsManage);
    }

    private static DateOnly? Day(string? v) => DateOnly.TryParseExact(v ?? "", "yyyy-MM-dd", out var d) ? d : null;

    public async Task<IResult> GetPending(ISender sender, CancellationToken cancellationToken, string? kind = null)
    {
        var result = await sender.Send(new GetPendingHandoversQuery(kind ?? ""), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Confirm(ISender sender, ConfirmHandoverBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfirmCashHandoverCommand(body.Kind ?? "", body.CollectorId, body.ExpectedAmount, body.ReceivedAmount, body.Note), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetHistory(ISender sender, CancellationToken cancellationToken, string? kind = null, string? from = null, string? to = null)
    {
        var result = await sender.Send(new GetCashHandoverHistoryQuery(kind ?? "", Day(from), Day(to)), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetOnline(ISender sender, CancellationToken cancellationToken, string? kind = null, string? from = null, string? to = null)
    {
        var result = await sender.Send(new GetOnlinePaymentsQuery(kind ?? "", Day(from), Day(to)), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    /// <summary>The transfer receipt, so the accountant can check it against the bank.</summary>
    public async Task<IResult> GetOnlineReceipt(ISender sender, string kind, Guid paymentId, CancellationToken cancellationToken)
    {
        switch (CashKinds.Parse(kind))
        {
            case CashKinds.Moi:
            {
                var r = await sender.Send(new GetMoiJobPaymentReceiptQuery(paymentId), cancellationToken);
                return r.Succeeded ? Results.File(r.Data!.Content, r.Data.ContentType, r.Data.FileName) : Results.NotFound(r);
            }
            case CashKinds.NonMoi:
            {
                var r = await sender.Send(new GetNonMoiJobPaymentReceiptQuery(paymentId), cancellationToken);
                return r.Succeeded ? Results.File(r.Data!.Content, r.Data.ContentType, r.Data.FileName) : Results.NotFound(r);
            }
            default:
                return Results.BadRequest();
        }
    }
}
