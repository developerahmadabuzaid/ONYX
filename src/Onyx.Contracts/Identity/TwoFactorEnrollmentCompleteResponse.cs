namespace Onyx.Contracts.Identity;

/// <summary>Recovery codes are shown once and are stored only in protected form.</summary>
public sealed record TwoFactorEnrollmentCompleteResponse(IReadOnlyList<string> RecoveryCodes, TokenPairResponse Tokens);
