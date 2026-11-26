namespace Onyx.Domain.Customers;

/// <summary>Stored as text.</summary>
public enum CustomerAccountStatus
{
    /// <summary>Registered; cannot sign in until the email address is verified.</summary>
    PendingVerification = 0,
    Active = 1,

    /// <summary>Disabled by staff; every session is revoked.</summary>
    Disabled = 2,

    /// <summary>Deleted at the customer's request: personal data is removed and the row is kept only for referential integrity.</summary>
    Erased = 3,
}
