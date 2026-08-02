namespace Jama.Application.Common.Interfaces;

/// <summary>
/// Stores and retrieves uploaded file content, keyed by an opaque storage key
/// the caller owns. Deliberately knows nothing about VIP clients or folders, so
/// any future feature that needs attachments can reuse it.
///
/// Implementations must treat the key as untrusted and refuse anything that
/// escapes their root.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes content and returns the key needed to read it back.</summary>
    Task<string> SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken);

    /// <summary>Opens stored content, or null when the key has no file behind it.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Removes stored content. Succeeds silently when already gone.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
