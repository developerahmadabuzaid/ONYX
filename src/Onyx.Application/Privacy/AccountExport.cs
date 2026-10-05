namespace Onyx.Application.Privacy;

/// <summary>
/// An admin account as included in a data-subject export. Credentials, password hashes, security stamps, authenticator keys,
/// recovery codes and session tokens are never part of it.
/// </summary>
public sealed record AccountExport(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredLanguage,
    bool IsActive,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AccountSessionExport> Sessions,
    IReadOnlyList<AccountActivityExport> Activity);
