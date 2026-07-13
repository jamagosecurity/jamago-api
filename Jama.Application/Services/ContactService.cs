using Jama.Application.Common;
using Jama.Application.DTOs;
using Jama.Application.Interfaces;
using Jama.Domain.Entities;

namespace Jama.Application.Services;

public class ContactService(IContactRepository repository) : IContactService
{
    public async Task<PagedResult<ContactSubmissionDto>> GetPagedAsync(
        PaginationQuery query,
        CancellationToken ct = default)
    {
        var (items, total) = await repository.GetPagedAsync(query, ct);

        return new PagedResult<ContactSubmissionDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
        };
    }

    public async Task<ContactSubmissionDto> CreateAsync(
        CreateContactSubmissionRequest request,
        CancellationToken ct = default)
    {
        var entity = new ContactSubmission
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Service = request.Service.Trim(),
            Message = request.Message.Trim(),
        };

        var created = await repository.AddAsync(entity, ct);
        return MapToDto(created);
    }

    private static ContactSubmissionDto MapToDto(ContactSubmission entity) =>
        new(entity.Id, entity.FullName, entity.Email, entity.Phone, entity.Service, entity.Message, entity.Status, entity.CreatedAt);
}
