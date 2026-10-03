namespace Onyx.Infrastructure.Identity;

/// <summary>
/// One admin login (the refresh-token family). Its id is the <c>sid</c> claim; revoking it invalidates the outstanding access
/// token immediately (checked per request) and every refresh token in the family.
/// </summary>
public sealed class UserSession
{
    public const int RevocationReasonMaxLength = 50;
    public const int IpAddressMaxLength = 45;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset AbsoluteExpiresAtUtc { get; set; }

    public DateTimeOffset LastRefreshedAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public string? RevocationReason { get; set; }

    /// <summary>Personal data: covered by the retention policy.</summary>
    public string? IpAddress { get; set; }

    public List<RefreshToken> RefreshTokens { get; } = [];
}
