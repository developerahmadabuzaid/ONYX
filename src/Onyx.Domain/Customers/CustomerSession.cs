using Onyx.Domain.Common;

namespace Onyx.Domain.Customers;

/// <summary>
/// One customer sign-in. The opaque session token is stored only as a SHA-256 hash and checked on every API request, so
/// revoking the session (sign-out, password change, disable, erasure) takes effect immediately. The session ends after
/// <see cref="IdleExpiresAtUtc"/> without use and at <see cref="AbsoluteExpiresAtUtc"/> regardless of use.
/// </summary>
public sealed class CustomerSession : Entity
{
    public const int HashLength = 64;
    public const int ReasonMaxLength = 40;
    public const int IpAddressMaxLength = 45;

    private CustomerSession()
    {
        TokenHash = string.Empty;
        SecurityStamp = string.Empty;
    }

    public Guid CustomerId { get; private set; }

    public string TokenHash { get; private set; }

    /// <summary>The account's security stamp when the session started; a later credential change invalidates the session.</summary>
    [SensitiveData]
    public string SecurityStamp { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public DateTimeOffset IdleExpiresAtUtc { get; private set; }

    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? RevocationReason { get; private set; }

    /// <summary>Personal data: covered by the retention policy.</summary>
    public string? IpAddress { get; private set; }

    public static CustomerSession Start(
        Guid customerId, string tokenHash, string securityStamp, TimeSpan idle, TimeSpan absolute, string? ipAddress, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrEmpty(tokenHash) || tokenHash.Length != HashLength)
        {
            throw new ArgumentException("A SHA-256 hex hash is required.", nameof(tokenHash));
        }

        var absoluteExpiry = nowUtc.Add(absolute);
        var idleExpiry = nowUtc.Add(idle);
        return new CustomerSession
        {
            CustomerId = customerId,
            TokenHash = tokenHash,
            SecurityStamp = securityStamp,
            CreatedAtUtc = nowUtc,
            LastSeenAtUtc = nowUtc,
            IdleExpiresAtUtc = idleExpiry < absoluteExpiry ? idleExpiry : absoluteExpiry,
            AbsoluteExpiresAtUtc = absoluteExpiry,
            IpAddress = ipAddress is { Length: > IpAddressMaxLength } ? ipAddress[..IpAddressMaxLength] : ipAddress,
        };
    }

    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc is null && nowUtc < IdleExpiresAtUtc && nowUtc < AbsoluteExpiresAtUtc;

    /// <summary>Extends the idle window (never beyond the absolute expiry).</summary>
    public void Touch(TimeSpan idle, DateTimeOffset nowUtc)
    {
        LastSeenAtUtc = nowUtc;
        var idleExpiry = nowUtc.Add(idle);
        IdleExpiresAtUtc = idleExpiry < AbsoluteExpiresAtUtc ? idleExpiry : AbsoluteExpiresAtUtc;
    }

    public void Revoke(string reason, DateTimeOffset nowUtc)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = nowUtc;
        RevocationReason = reason.Length > ReasonMaxLength ? reason[..ReasonMaxLength] : reason;
    }
}
