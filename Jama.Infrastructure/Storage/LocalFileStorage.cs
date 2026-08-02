using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Microsoft.Extensions.Options;

namespace Jama.Infrastructure.Storage;

/// <summary>
/// Stores uploads on the server's own disk, under a configured root.
///
/// Nothing here is reachable by the web server directly — nginx serves the
/// frontend from a different directory, and content only leaves through an
/// authenticated API endpoint. That is the point: a client's documents must
/// never be guessable by URL.
/// </summary>
public sealed class LocalFileStorage(IOptions<FileStorageSettings> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.Root);

    public async Task<string> SaveAsync(
        Stream content,
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);

        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps a key to an absolute path and refuses anything that resolves outside
    /// the root. Keys are generated server-side today, but a traversal check is
    /// the difference between a bug and an arbitrary-file-read vulnerability the
    /// day one is ever built from user input.
    ///
    /// Keys are stored '/'-separated so they mean the same thing on Windows and
    /// Linux; both separators are accepted on the way in, because rows written
    /// before that rule existed may still contain backslashes.
    /// </summary>
    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        var normalised = storageKey
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        var combined = Path.GetFullPath(Path.Combine(_root, normalised));

        if (!combined.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(combined, _root, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Storage key resolves outside the storage root.");
        }

        return combined;
    }
}
