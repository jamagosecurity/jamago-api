namespace Jama.Application.Common.Interfaces;

/// <summary>Sets or clears a message's "seen" mark on the mail server itself, so every mail app agrees.</summary>
public interface IMailFlagWriter
{
    Task SetSeenAsync(string accountKey, string folder, long uidValidity, long uid, bool seen, CancellationToken cancellationToken);

    /// <summary>Moves one message to the mailbox's Trash (it can still be restored from webmail). When the UID is not
    /// known yet — a message just sent — the copy is found by its Message-ID instead.</summary>
    Task MoveToTrashAsync(string accountKey, string folder, long uidValidity, long uid, string? messageId, CancellationToken cancellationToken);
}
