namespace Jama.Application.Common.Interfaces;

/// <summary>Sets or clears a message's "seen" mark on the mail server itself, so every mail app agrees.</summary>
public interface IMailFlagWriter
{
    Task SetSeenAsync(string accountKey, string folder, long uidValidity, long uid, bool seen, CancellationToken cancellationToken);
}
