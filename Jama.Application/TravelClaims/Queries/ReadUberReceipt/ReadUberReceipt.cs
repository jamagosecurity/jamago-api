using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.TravelClaims.Queries.ReadUberReceipt;

/// <summary>Reads an Uber receipt before the claim is submitted, so the form can be filled in for the person to check.
/// Nothing is saved. A photo has no text to read; the person types the figures.</summary>
public sealed record ReadUberReceiptQuery(string? FileName, long Size, Stream? Content) : IRequest<ApiResult<UberReceiptReading>>;

public sealed class ReadUberReceiptQueryHandler(IPdfTextReader reader, ICurrentUser actor) : IRequestHandler<ReadUberReceiptQuery, ApiResult<UberReceiptReading>>
{
    public async Task<ApiResult<UberReceiptReading>> Handle(ReadUberReceiptQuery r, CancellationToken ct)
    {
        if (!TravelClaimRules.IsStaff(actor)) return ApiResult<UberReceiptReading>.Failure("Only staff can claim Uber bills.");
        var (bytes, error) = await TravelClaimRules.ReadFileAsync(r.Content, r.FileName, r.Size, "Uber receipt", ct);
        if (bytes is null) return ApiResult<UberReceiptReading>.Failure(error!);
        if (!Path.GetExtension(r.FileName ?? "").Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return ApiResult<UberReceiptReading>.Success(UberReceiptReading.Nothing);
        return ApiResult<UberReceiptReading>.Success(UberReceiptParser.Parse(reader.ReadText(bytes)));
    }
}
