using System.Globalization;
using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.Proformas;
using Jama.Application.Proformas.Commands.SaveProforma;
using Jama.Application.Proformas.Queries.GetAdminQuotations;
using Jama.Application.Proformas.Queries.GetProformaFile;
using Jama.Application.Proformas.Queries.GetProformaSheet;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

public sealed record SaveProformaBody(
    string? PoNumber, string? PoDate, string? CustomerName, string? ProjectName, string? ContactNumber, string? Location);

/// <summary>
/// The office administrator's section (proforma.manage): every quotation of every staff member, in any status, and
/// the short proforma invoice of each. Read and print only — quotations are built and approved elsewhere.
/// </summary>
public sealed class Proformas : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetQuotations, "quotations", permission: Permissions.ProformaManage)
            .MapGet(GetSheet, "{boqId:guid}", permission: Permissions.ProformaManage)
            .MapPost(Save, "{boqId:guid}", permission: Permissions.ProformaManage)
            // ?format=pdf (default), excel or word — the same document in each.
            .MapGet(GetFile, "{boqId:guid}/file", permission: Permissions.ProformaManage)
            // Shows the document before download: saves nothing, uses up no number.
            .MapGet(GetPreview, "{boqId:guid}/preview", permission: Permissions.ProformaManage);
    }

    private static IResult Reply<T>(ApiResult<T> result) => result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);

    public async Task<IResult> GetQuotations(ISender sender, [AsParameters] GetAdminQuotationsQuery query, CancellationToken ct) =>
        Reply(await sender.Send(query, ct));

    public async Task<IResult> GetSheet(ISender sender, Guid boqId, CancellationToken ct) =>
        Reply(await sender.Send(new GetProformaSheetQuery(boqId), ct));

    public async Task<IResult> Save(ISender sender, Guid boqId, SaveProformaBody body, CancellationToken ct)
    {
        DateOnly? poDate = null;
        if (!string.IsNullOrWhiteSpace(body.PoDate))
        {
            if (!DateOnly.TryParseExact(body.PoDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return Results.BadRequest(ApiResult<string>.Failure("Enter the P.O. date as a date."));
            poDate = parsed;
        }

        var customer = new ProformaCustomer(body.CustomerName, body.ProjectName, body.ContactNumber, body.Location);
        return Reply(await sender.Send(new SaveProformaCommand(boqId, body.PoNumber, poDate, customer), ct));
    }

    public async Task<IResult> GetFile(ISender sender, Guid boqId, string? format, CancellationToken ct)
    {
        if (!Enum.TryParse<ProformaFormat>(format ?? nameof(ProformaFormat.Pdf), ignoreCase: true, out var chosen) || !Enum.IsDefined(chosen))
            return Results.BadRequest(ApiResult<string>.Failure("Choose PDF, Excel or Word."));
        return File(await sender.Send(new GetProformaFileQuery(boqId, chosen), ct));
    }

    public async Task<IResult> GetPreview(
        ISender sender, Guid boqId, string? poNumber, string? poDate,
        string? customerName, string? projectName, string? contactNumber, string? location, CancellationToken ct)
    {
        var customer = new ProformaCustomer(customerName, projectName, contactNumber, location);
        DateOnly? date = DateOnly.TryParseExact(poDate?.Trim() ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
        return File(await sender.Send(new GetProformaFileQuery(boqId, ProformaFormat.Pdf, new ProformaInput(poNumber, date, customer)), ct));
    }

    private static IResult File(ApiResult<ProformaFileDto> result) =>
        result.Succeeded && result.Data is not null
            ? Results.File(result.Data.Content, result.Data.ContentType, result.Data.FileName)
            : Results.BadRequest(ApiResult<string>.Failure(result.Errors));
}
