using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.TravelClaims.Commands.ApproveTravelClaims;
using Jama.Application.TravelClaims.Commands.ReimburseTravelClaims;
using Jama.Application.TravelClaims.Commands.RejectTravelClaim;
using Jama.Application.TravelClaims.Commands.SetLunchRate;
using Jama.Application.TravelClaims.Commands.SubmitTravelClaim;
using Jama.Application.TravelClaims.Commands.WithdrawTravelClaim;
using Jama.Application.TravelClaims.Queries.GetLunchRates;
using Jama.Application.TravelClaims.Queries.GetMyTravelClaims;
using Jama.Application.TravelClaims.Queries.GetTravelClaimFile;
using Jama.Application.TravelClaims.Queries.GetTravelClaims;
using Jama.Application.TravelClaims.Queries.GetTravelPeople;
using Jama.Application.TravelClaims.Queries.ReadUberReceipt;
using Jama.Domain.Enums;
using Jama.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jama.Web.Endpoints;

public sealed record RejectTravelClaimBody(string? Reason);
public sealed record ApproveBody(IReadOnlyList<TravelClaimApproval>? Items);
public sealed record ReimburseBody(IReadOnlyList<Guid>? ClaimIds, string? Method, string? Reference);
public sealed record LunchRateBody(decimal AmountPerPerson, string? EffectiveFrom);

/// <summary>
/// Uber bills. The day, time, amount and places of a ride are read from Uber's PDF on the server — the form never sends them.
/// Every staff member and technician claims their own (signed in; clients are refused in the handlers).
/// The accountant (accounts.manage) sees everyone's, approves or rejects, pays them back and sets the lunch rate.
/// </summary>
public sealed class TravelClaims : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetMine, "mine", requireAuthorization: true)
            .MapGet(GetPeople, "people", requireAuthorization: true)
            .MapPost(ReadReceipt, "read", requireAuthorization: true, allowFileUpload: true)
            .MapPost(Submit, requireAuthorization: true, allowFileUpload: true)
            .MapPost(Withdraw, "{id:guid}/withdraw", requireAuthorization: true)
            .MapGet(GetFile, "{id:guid}/lines/{lineId:guid}/file", requireAuthorization: true)
            .MapGet(GetAll, permission: Permissions.AccountsManage)
            .MapPost(Approve, "approve", permission: Permissions.AccountsManage)
            .MapPost(Reject, "{id:guid}/reject", permission: Permissions.AccountsManage)
            .MapPost(Reimburse, "reimburse", permission: Permissions.AccountsManage)
            .MapGet(GetLunchRates, "lunch-rate", permission: Permissions.AccountsManage)
            .MapPost(SetLunchRate, "lunch-rate", permission: Permissions.AccountsManage);
    }

    private static IResult Reply<T>(ApiResult<T> result) => result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);

    private static DateOnly? Day(string? v) => DateOnly.TryParseExact(v ?? "", "yyyy-MM-dd", out var d) ? d : null;

    private static TEnum? Parse<TEnum>(string? v) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(v, ignoreCase: true, out var e) && Enum.IsDefined(e) ? e : null;

    private static IReadOnlyList<Guid> Ids(string? csv) => (csv ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();

    public async Task<IResult> GetMine(ISender sender, CancellationToken ct) => Reply(await sender.Send(new GetMyTravelClaimsQuery(), ct));

    public async Task<IResult> GetPeople(ISender sender, CancellationToken ct) => Reply(await sender.Send(new GetTravelPeopleQuery(), ct));

    public async Task<IResult> ReadReceipt(ISender sender, IFormFile? receipt, CancellationToken ct)
    {
        await using var stream = receipt?.OpenReadStream();
        return Reply(await sender.Send(new ReadUberReceiptQuery(receipt?.FileName, receipt?.Length ?? 0, stream), ct));
    }

    public async Task<IResult> Submit(
        ISender sender,
        [FromForm] string? tripType, [FromForm] string? siteName, [FromForm] string? companions, [FromForm] string? ateLunch, [FromForm] decimal? lunchBillAmount, [FromForm] string? note,
        IFormFile? receipt, IFormFile? lunchBill, CancellationToken ct)
    {
        await using var receiptStream = receipt?.OpenReadStream();
        await using var lunchStream = lunchBill?.OpenReadStream();
        return Reply(await sender.Send(new SubmitTravelClaimCommand
        {
            TripType = Parse<TravelTripType>(tripType),
            SiteName = siteName,
            Companions = Ids(companions), AteLunch = Ids(ateLunch), LunchBillAmount = lunchBillAmount ?? 0, Note = note,
            ReceiptFileName = receipt?.FileName, ReceiptContentType = receipt?.ContentType, ReceiptSize = receipt?.Length ?? 0, Receipt = receiptStream,
            LunchBillFileName = lunchBill?.FileName, LunchBillContentType = lunchBill?.ContentType, LunchBillSize = lunchBill?.Length ?? 0, LunchBill = lunchStream,
        }, ct));
    }

    public async Task<IResult> Withdraw(ISender sender, Guid id, CancellationToken ct) => Reply(await sender.Send(new WithdrawTravelClaimCommand(id), ct));

    public async Task<IResult> GetFile(ISender sender, Guid id, Guid lineId, CancellationToken ct)
    {
        var result = await sender.Send(new GetTravelClaimFileQuery(id, lineId), ct);
        return result.Succeeded ? Results.File(result.Data!.Content, result.Data.ContentType, result.Data.FileName) : Results.NotFound(result);
    }

    public async Task<IResult> GetAll(ISender sender, CancellationToken ct, Guid? personId = null, string? from = null, string? to = null) =>
        Reply(await sender.Send(new GetTravelClaimsQuery(personId, Day(from), Day(to)), ct));

    public async Task<IResult> Approve(ISender sender, ApproveBody body, CancellationToken ct) =>
        Reply(await sender.Send(new ApproveTravelClaimsCommand(body.Items ?? []), ct));

    public async Task<IResult> Reject(ISender sender, Guid id, RejectTravelClaimBody body, CancellationToken ct) =>
        Reply(await sender.Send(new RejectTravelClaimCommand(id, body.Reason), ct));

    public async Task<IResult> Reimburse(ISender sender, ReimburseBody body, CancellationToken ct) =>
        Reply(await sender.Send(new ReimburseTravelClaimsCommand(body.ClaimIds ?? [], Parse<ReimbursementMethod>(body.Method), body.Reference), ct));

    public async Task<IResult> GetLunchRates(ISender sender, CancellationToken ct) => Reply(await sender.Send(new GetLunchRatesQuery(), ct));

    public async Task<IResult> SetLunchRate(ISender sender, LunchRateBody body, CancellationToken ct) =>
        Reply(await sender.Send(new SetLunchRateCommand(body.AmountPerPerson, Day(body.EffectiveFrom)), ct));
}
