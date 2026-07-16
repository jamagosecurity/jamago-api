using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;

namespace Jama.Application.Contacts.Commands.CreateContactSubmission;

public record CreateContactSubmissionCommand : IRequest<TypedResult<ContactSubmissionDto>>
{
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Service { get; init; }
    public string? Message { get; init; }
}

public class CreateContactSubmissionCommandHandler : IRequestHandler<CreateContactSubmissionCommand, TypedResult<ContactSubmissionDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateContactSubmissionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<ContactSubmissionDto>> Handle(
        CreateContactSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new ContactSubmission
        {
            Id = Guid.CreateVersion7(),
            FullName = request.FullName!,
            Email = request.Email!.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Service = request.Service!,
            Message = request.Message!,
        };

        _context.ContactSubmissions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<ContactSubmissionDto>.Success(ContactMappings.ToDto(entity));
    }
}
