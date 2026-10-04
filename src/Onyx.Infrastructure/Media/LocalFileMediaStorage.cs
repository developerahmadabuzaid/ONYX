using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Onyx.Application.Media;

namespace Onyx.Infrastructure.Media;

/// <summary>
/// Local file-system media storage (development and simple single-server hosting). Keys are validated against the server's
/// own key format and resolved strictly inside the storage root, so no path traversal is possible. Replaceable by an object
/// storage adapter without changing CMS or domain code.
/// </summary>
internal sealed partial class LocalFileMediaStorage(IOptions<MediaOptions> options, IHostEnvironment environment) : IMediaStorage
{
    private readonly Lazy<string> _root = new(() =>
    {
        var configured = options.Value.StorageRoot;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Media:StorageRoot is not configured.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured, environment.ContentRootPath));
    });

    public async Task SaveAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true))
        {
            await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, path, overwrite: false);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        Stream? stream = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        if (string.IsNullOrEmpty(storageKey) || !KeyPattern().IsMatch(storageKey))
        {
            throw new ArgumentException("Invalid media storage key.", nameof(storageKey));
        }

        var root = _root.Value;
        var full = Path.GetFullPath(Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Media storage key escapes the storage root.", nameof(storageKey));
        }

        return full;
    }

    [GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\\.(jpg|png|webp|gif)$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
