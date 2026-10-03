namespace Onyx.Contracts.Identity;

/// <summary>Returned to Onyx.Web (server-to-server only). Tokens and challenge tokens must never reach browser JavaScript.</summary>
public sealed record LoginResponse(
    string Status,
    TokenPairResponse? Tokens,
    string? ChallengeToken,
    DateTimeOffset? ChallengeExpiresAtUtc);
