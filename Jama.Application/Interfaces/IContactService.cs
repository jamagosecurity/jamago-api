using Jama.Application.Common;
using Jama.Application.DTOs;
using Jama.Domain.Entities;

namespace Jama.Application.Interfaces;

public interface IContactRepository
{
    Task<(IReadOnlyList<ContactSubmission> Items, int TotalCount)> GetPagedAsync(PaginationQuery query, CancellationToken ct = default);
    Task<ContactSubmission> AddAsync(ContactSubmission entity, CancellationToken ct = default);
}

public interface IContactService
{
    Task<PagedResult<ContactSubmissionDto>> GetPagedAsync(PaginationQuery query, CancellationToken ct = default);
    Task<ContactSubmissionDto> CreateAsync(CreateContactSubmissionRequest request, CancellationToken ct = default);
}
