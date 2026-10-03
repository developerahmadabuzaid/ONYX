namespace Onyx.Contracts.Identity;

public static class LoginStatus
{
    /// <summary>Tokens issued.</summary>
    public const string Succeeded = "Succeeded";

    /// <summary>Password accepted; submit an authenticator or recovery code with the challenge token.</summary>
    public const string TwoFactorRequired = "TwoFactorRequired";

    /// <summary>Password accepted; this account must enrol an authenticator before any token is issued.</summary>
    public const string TwoFactorEnrollmentRequired = "TwoFactorEnrollmentRequired";
}
