using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jama.Application.Mail.Commands;

/// <summary>
/// Deletes messages: gone from the list at once, and moved to the mailbox's Trash on the mail server in the background,
/// so they can still be restored from webmail. Administrators only (checked at the endpoint).
/// </summary>
public sealed record DeleteMailCommand(IReadOnlyList<Guid> Ids) : IRequest<ApiResult<int>>;

public sealed class DeleteMailCommandHandler(
    IApplicationDbContext context,
    IMailFlagWriter server,
    IBackgroundWork background,
    ILogger<DeleteMailCommandHandler> logger) : IRequestHandler<DeleteMailCommand, ApiResult<int>>
{
    public async Task<ApiResult<int>> Handle(DeleteMailCommand request, CancellationToken cancellationToken)
    {
        var ids = (request.Ids ?? []).Distinct().Take(200).ToList();
        if (ids.Count == 0) return ApiResult<int>.Failure("Choose a message to delete.");

        var messages = await context.MailMessages.Where(m => ids.Contains(m.Id)).ToListAsync(cancellationToken);
        if (messages.Count == 0) return ApiResult<int>.Failure("Message not found.");

        var moves = messages.Select(m => (
            Key: string.IsNullOrEmpty(m.SourceKey) ? m.AccountKey : m.SourceKey,
            m.Folder, m.UidValidity, m.Uid, m.MessageId)).ToList();
        context.MailMessages.RemoveRange(messages);
        await context.SaveChangesAsync(cancellationToken);

        background.Enqueue(async token =>
        {
            foreach (var m in moves)
            {
                try { await server.MoveToTrashAsync(m.Key, m.Folder, m.UidValidity, m.Uid, m.MessageId, token); }
                catch (Exception ex) { logger.LogWarning("Could not move a deleted message to Trash for {Key}: {Message}", m.Key, ex.Message); }
            }
        });
        return ApiResult<int>.Success(messages.Count);
    }
}
