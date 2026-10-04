using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// A short-lived, read-only grant to view one language of a content item's working copy. Only the SHA-256 hash of the random
/// token is stored. A preview carries no admin identity or session.
/// </summary>
public sealed class ContentPreviewToken : Entity
{
    public const int HashLength = 64;

    private ContentPreviewToken()
    {
        LanguageCode = string.Empty;
        TokenHash = string.Empty;
    }

    public Guid ContentItemId { get; private set; }

    public string LanguageCode { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public Guid? CreatedById { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public static ContentPreviewToken Issue(Guid contentItemId, string languageCode, string tokenHash, DateTimeOffset now, TimeSpan lifetime, Guid? createdById)
    {
        if (tokenHash is not { Length: HashLength } || lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("A preview token needs a SHA-256 hash and a positive lifetime.");
        }

        return new ContentPreviewToken
        {
            ContentItemId = contentItemId,
            LanguageCode = languageCode,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
            CreatedById = createdById,
        };
    }

    public bool IsValidAt(DateTimeOffset now) => RevokedAtUtc is null && now < ExpiresAtUtc;

    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;
}
