namespace Onyx.Contracts.Identity;

/// <summary>The shared key is shown once to the user (as text and as a QR code of <see cref="AuthenticatorUri"/>).</summary>
public sealed record TwoFactorEnrollmentStartResponse(string SharedKey, string AuthenticatorUri);
