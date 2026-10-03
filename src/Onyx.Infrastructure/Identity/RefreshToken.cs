namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Single-use rotating refresh token. Only a SHA-256 hash is stored. <see cref="SealedSuccessor"/> holds the successor token
/// encrypted with a key derived from <em>this</em> token's raw value, which only the client knows, so a concurrent refresh in
/// the grace window can receive the same successor while a database leak alone reveals no usable token.
/// </summary>
public sealed class RefreshToken
{
    public const int HashLength = 64;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SessionId { get; set; }

    public UserSession? Session { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Idle expiry, never beyond the session's absolute expiry.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public string? SealedSuccessor { get; set; }

    public int GraceRedemptions { get; set; }
}
