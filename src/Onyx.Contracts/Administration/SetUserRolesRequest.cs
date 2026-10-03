namespace Onyx.Contracts.Administration;

public sealed record SetUserRolesRequest(IReadOnlyList<string> Roles);
