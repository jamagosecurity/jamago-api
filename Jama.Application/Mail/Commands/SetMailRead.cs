using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jama.Application.Mail.Commands;

/// <summary>Mark one message read or unread: here at once, and on the mail server in the background so Outlook and
/// webmail agree.</summary>
public sealed record SetMailReadCommand(Guid Id, bool Read) : IRequest<ApiResult<bool>>;

public sealed class SetMailReadCommandHandler(
    IApplicationDbContext context,
    IMailFlagWriter flags,
    IBackgroundWork background,
    ILogger<SetMailReadCommandHandler> logger) : IRequestHandler<SetMailReadCommand, ApiResult<bool>>
{
    public async Task<ApiResult<bool>> Handle(SetMailReadCommand request, CancellationToken cancellationToken)
    {
        var message = await context.MailMessages.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
        if (message is null) return ApiResult<bool>.Failure("Message not found.");
        if (message.IsRead == request.Read) return ApiResult<bool>.Success(request.Read);

        message.IsRead = request.Read;
        await context.SaveChangesAsync(cancellationToken);

        // The read mark lives in the mailbox the message was read from (the copy mailbox, for forwarded mail).
        var key = string.IsNullOrEmpty(message.SourceKey) ? message.AccountKey : message.SourceKey;
        var (folder, validity, uid, read) = (message.Folder, message.UidValidity, message.Uid, request.Read);
        background.Enqueue(async token =>
        {
            try { await flags.SetSeenAsync(key, folder, validity, uid, read, token); }
            catch (Exception ex) { logger.LogWarning("Could not update the read mark on the mail server for {Key}: {Message}", key, ex.Message); }
        });
        return ApiResult<bool>.Success(request.Read);
    }
}
