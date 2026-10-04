using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// A public URL path (without the language prefix) that a content item has used in one language. The current path serves the
/// page; former paths permanently redirect to the item's current path, so published URLs never break. A path belongs to one
/// item forever unless an editor explicitly releases it.
/// </summary>
public sealed class ContentPath : Entity
{
    public const int PathMaxLength = 500;

    private ContentPath()
    {
        LanguageCode = string.Empty;
        Path = string.Empty;
    }

    public Guid ContentItemId { get; private set; }

    public string LanguageCode { get; private set; }

    /// <summary>NFC Unicode segments joined by '/', e.g. <c>الخدمات/حلول-الذكاء-الاصطناعي</c>; never percent-encoded here.</summary>
    public string Path { get; private set; }

    public bool IsCurrent { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RetiredAtUtc { get; private set; }

    public static ContentPath Create(Guid contentItemId, string languageCode, string path, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length > PathMaxLength)
        {
            throw new ArgumentException("Path too long.", nameof(path));
        }

        return new ContentPath { ContentItemId = contentItemId, LanguageCode = languageCode, Path = path, IsCurrent = true, CreatedAtUtc = now };
    }

    public void Retire(DateTimeOffset now)
    {
        if (IsCurrent)
        {
            IsCurrent = false;
            RetiredAtUtc = now;
        }
    }

    /// <summary>The item moved back to a path it used before.</summary>
    public void Reactivate()
    {
        IsCurrent = true;
        RetiredAtUtc = null;
    }
}
