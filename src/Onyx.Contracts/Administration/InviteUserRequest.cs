namespace Onyx.Contracts.Administration;

public sealed record InviteUserRequest(string Email, string DisplayName, string PreferredLanguage, IReadOnlyList<string> Roles);
