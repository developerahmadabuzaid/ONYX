namespace Onyx.Contracts.Administration;

public sealed record UserSummaryResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredLanguage,
    bool IsActive,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool IsLockedOut,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);
