namespace Onyx.Contracts.Identity;

/// <summary>Kept server-side by Onyx.Web inside its encrypted HttpOnly cookie (docs/architecture.md §25.1).</summary>
public sealed record TokenPairResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    DateTimeOffset SessionExpiresAtUtc);
