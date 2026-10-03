namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Short-lived, single-use proof that the password step succeeded, exchanged for tokens after the second factor (or after
/// mandatory authenticator enrolment). Stored hashed; invalidated after too many wrong codes.
/// </summary>
public sealed class LoginChallenge
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public LoginChallengePurpose Purpose { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }

    public int FailedAttempts { get; set; }
}
