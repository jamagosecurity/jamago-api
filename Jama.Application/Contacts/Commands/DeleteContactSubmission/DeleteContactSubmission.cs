using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Contacts.Commands.DeleteContactSubmission;

/// <summary>Removes an enquiry from the inbox. Administrators only — see the
/// endpoint — since staff granted contact.view can read the inbox, not clear it.</summary>
public sealed record DeleteContactSubmissionCommand(Guid Id) : IRequest<TypedResult<string>>;

public sealed class DeleteContactSubmissionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteContactSubmissionCommand, TypedResult<string>>
{
    public async Task<TypedResult<string>> Handle(
        DeleteContactSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await context.ContactSubmissions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (submission is null)
            return TypedResult<string>.Failure("Enquiry not found.");

        context.ContactSubmissions.Remove(submission);
        await context.SaveChangesAsync(cancellationToken);
        return TypedResult<string>.Success("Enquiry deleted.");
    }
}
