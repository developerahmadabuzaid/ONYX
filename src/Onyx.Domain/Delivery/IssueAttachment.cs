using System.Text;
using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>
/// A file attached to an issue. The type is decided by the server from the file's bytes, never from its name; the file is stored
/// under a random key and only served through the issue's access check, always as a download.
/// </summary>
public sealed class IssueAttachment : Entity
{
    public const int FileNameMaxLength = 200;
    public const int ContentTypeMaxLength = 100;
    public const int ExtensionMaxLength = 10;
    public const int StorageKeyMaxLength = 200;
    public const int Sha256Length = 64;

    private IssueAttachment()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
        Extension = string.Empty;
        StorageKey = string.Empty;
        Sha256 = string.Empty;
    }

    public Guid ProjectId { get; private set; }

    public Guid IssueId { get; private set; }

    public Guid UploadedById { get; private set; }

    /// <summary>Display name: the uploaded name without any path, control characters or a misleading extension.</summary>
    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public string Extension { get; private set; }

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; }

    public string Sha256 { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);

    public static IssueAttachment Create(
        Issue issue, Guid uploadedById, string? originalName, string contentType, string extension, long sizeBytes, string storageKey, string sha256, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        return new IssueAttachment
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UploadedById = uploadedById,
            FileName = SafeFileName(originalName, extension),
            ContentType = contentType,
            Extension = extension,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            Sha256 = sha256,
            UploadedAtUtc = now,
        };
    }

    /// <summary>
    /// The name to show and to offer on download: the last path segment, control and reserved characters removed, and always
    /// ending in the extension the server detected (so "report.pdf.html" can never be offered as HTML).
    /// </summary>
    public static string SafeFileName(string? originalName, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        var name = (originalName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (!char.IsControl(character) && character is not ('"' or '<' or '>' or '|' or ':' or '*' or '?'))
            {
                builder.Append(character);
            }
        }

        var clean = builder.ToString().Trim().Trim('.');
        var suffix = "." + extension;
        if (clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^suffix.Length];
        }

        var stemLimit = FileNameMaxLength - suffix.Length;
        if (clean.Length > stemLimit)
        {
            clean = clean[..stemLimit];
        }

        return (clean.Length == 0 ? "file" : clean) + suffix;
    }
}
