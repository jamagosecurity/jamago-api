using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.ClientRegistrations.Queries.GetClientRegistrations;

/// <summary>Registrations in one status (Pending when not given), newest
/// first, optionally narrowed by a search over name, email, mobile and site.</summary>
public sealed record GetClientRegistrationsQuery(ClientRegistrationStatus? Status, string? Search)
    : IRequest<ApiResult<ClientRegistrationListDto>>;

public sealed class GetClientRegistrationsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClientRegistrationsQuery, ApiResult<ClientRegistrationListDto>>
{
    public async Task<ApiResult<ClientRegistrationListDto>> Handle(
        GetClientRegistrationsQuery request,
        CancellationToken cancellationToken)
    {
        var status = request.Status ?? ClientRegistrationStatus.Pending;

        var query = context.ClientRegistrations
            .AsNoTracking()
            .Include(x => x.DiaInspection)
            .Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(search)
                || x.Email.Contains(search)
                || x.Mobile.Contains(search)
                || x.SiteLocation.ToLower().Contains(search));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // Counted unfiltered by search: the tabs say how many are in each
        // status, not how many match whatever is typed in the box.
        var counts = await context.ClientRegistrations
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(ClientRegistrationStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;

        return ApiResult<ClientRegistrationListDto>.Success(new ClientRegistrationListDto(
            items.Select(ClientRegistrationMappings.ToDto).ToList(),
            new ClientRegistrationCountsDto(
                CountOf(ClientRegistrationStatus.Pending),
                CountOf(ClientRegistrationStatus.Approved),
                CountOf(ClientRegistrationStatus.Rejected)),
            ClientTeams.All));
    }
}
