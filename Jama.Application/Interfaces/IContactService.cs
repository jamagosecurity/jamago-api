using Jama.Application.DTOs;
using Jama.Domain.Entities;

namespace Jama.Application.Interfaces;

public interface IContactRepository
{
    Task<IReadOnlyList<ContactSubmission>> GetAllAsync(CancellationToken ct = default);
    Task<ContactSubmission> AddAsync(ContactSubmission entity, CancellationToken ct = default);
}

public interface IContactService
{
    Task<IReadOnlyList<ContactSubmissionDto>> GetAllAsync(CancellationToken ct = default);
    Task<ContactSubmissionDto> CreateAsync(CreateContactSubmissionRequest request, CancellationToken ct = default);
}
