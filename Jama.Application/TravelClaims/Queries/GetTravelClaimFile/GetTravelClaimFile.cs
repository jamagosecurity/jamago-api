using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Queries.GetTravelClaimFile;

/// <summary>The receipt of one line of a claim — for the claimant, or the accountant. Anyone else gets "not found".</summary>
public sealed record GetTravelClaimFileQuery(Guid ClaimId, Guid LineId) : IRequest<ApiResult<TravelClaimFile>>;

public sealed class GetTravelClaimFileQueryHandler(IApplicationDbContext context, IFileStorage storage, ICurrentUser actor)
    : IRequestHandler<GetTravelClaimFileQuery, ApiResult<TravelClaimFile>>
{
    public async Task<ApiResult<TravelClaimFile>> Handle(GetTravelClaimFileQuery r, CancellationToken ct)
    {
        var line = await context.TravelClaimLines.AsNoTracking()
            .Where(l => l.Id == r.LineId && l.TravelClaimId == r.ClaimId)
            .Select(l => new { l.StorageKey, l.FileName, l.ContentType, l.TravelClaim.ClaimantId }).FirstOrDefaultAsync(ct);
        if (line is null || (line.ClaimantId != actor.UserId && !actor.Has(Permissions.AccountsManage)))
            return ApiResult<TravelClaimFile>.Failure("File not found.");
        var stream = await storage.OpenReadAsync(line.StorageKey, ct);
        return stream is null
            ? ApiResult<TravelClaimFile>.Failure("The file is missing.")
            : ApiResult<TravelClaimFile>.Success(new(stream, line.FileName, line.ContentType));
    }
}
