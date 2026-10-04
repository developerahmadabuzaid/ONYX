namespace Onyx.Application.Identity;

/// <summary>Outcome of an operator 2FA recovery. Contains no authentication data.</summary>
public sealed record TwoFactorRecoveryResult(Guid UserId, int SessionsRevoked, int ChallengesClosed);
