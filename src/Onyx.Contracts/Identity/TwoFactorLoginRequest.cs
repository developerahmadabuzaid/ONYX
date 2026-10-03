namespace Onyx.Contracts.Identity;

/// <summary>Exactly one of <see cref="Code"/> (authenticator) or <see cref="RecoveryCode"/> must be supplied.</summary>
public sealed record TwoFactorLoginRequest(string ChallengeToken, string? Code, string? RecoveryCode);
