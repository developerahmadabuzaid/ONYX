using Onyx.Domain.Common;

namespace Onyx.Domain.Customers;

/// <summary>
/// A single-use emailed token (email verification or password reset). Only a SHA-256 hash of the high-entropy raw value is
/// stored; the raw value exists only in the email link.
/// </summary>
public sealed class CustomerToken : Entity
{
    public const int HashLength = 64;

    private CustomerToken()
    {
        TokenHash = string.Empty;
    }

    public Guid CustomerId { get; private set; }

    public CustomerTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? UsedAtUtc { get; private set; }

    public static CustomerToken Issue(Guid customerId, CustomerTokenPurpose purpose, string tokenHash, TimeSpan lifetime, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrEmpty(tokenHash) || tokenHash.Length != HashLength)
        {
            throw new ArgumentException("A SHA-256 hex hash is required.", nameof(tokenHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "The lifetime must be positive.");
        }

        return new CustomerToken
        {
            CustomerId = customerId,
            Purpose = purpose,
            TokenHash = tokenHash,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(lifetime),
        };
    }

    public bool IsUsable(DateTimeOffset nowUtc) => UsedAtUtc is null && nowUtc < ExpiresAtUtc;

    /// <summary>Consumes the token. Fails when it was already used or has expired.</summary>
    public Result Use(DateTimeOffset nowUtc)
    {
        if (!IsUsable(nowUtc))
        {
            return Error.Validation("Customer.InvalidToken", "This link is invalid or has expired.");
        }

        UsedAtUtc = nowUtc;
        return Result.Success();
    }
}
