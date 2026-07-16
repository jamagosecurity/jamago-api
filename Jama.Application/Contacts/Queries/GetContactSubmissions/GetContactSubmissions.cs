using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Contacts.Queries.GetContactSubmissions;

public record GetContactSubmissionsQuery : IRequest<TypedResult<IReadOnlyList<ContactSubmissionDto>>>;

public class GetContactSubmissionsQueryHandler
    : IRequestHandler<GetContactSubmissionsQuery, TypedResult<IReadOnlyList<ContactSubmissionDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetContactSubmissionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<IReadOnlyList<ContactSubmissionDto>>> Handle(
        GetContactSubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _context.ContactSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ContactSubmissionDto> result = items.Select(ContactMappings.ToDto).ToList();
        return TypedResult<IReadOnlyList<ContactSubmissionDto>>.Success(result);
    }
}
