namespace Onyx.Contracts.Identity;

/// <summary>Profile and effective permissions of the signed-in admin. Onyx.Web uses permissions only to hide UI; the API enforces them.</summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredLanguage,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Permissions);
