using Jama.Domain.Entities;

namespace Jama.Application.Contacts;

internal static class ContactMappings
{
    internal static ContactSubmissionDto ToDto(ContactSubmission entity) =>
        new(
            entity.Id,
            entity.FullName,
            entity.Email,
            entity.Phone,
            entity.Service,
            entity.Message,
            entity.Status,
            entity.CreatedAt);
}
