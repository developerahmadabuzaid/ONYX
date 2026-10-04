namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Binds a pending authenticator key to the enrolment challenge that generated it (stored as a user token). Enrolment can
/// only be completed with that same challenge, so a key left over from an abandoned, stale or other setup attempt is unusable.
/// The value is a challenge id, not a secret; the binding is removed when enrolment completes or 2FA is reset.
/// </summary>
internal static class AuthenticatorEnrollmentBinding
{
    public const string LoginProvider = "[Onyx]";
    public const string TokenName = "AuthenticatorKeyEnrollment";
}
