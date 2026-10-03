namespace Onyx.Contracts.Administration;

/// <summary>Used to create and to update a role.</summary>
public sealed record SaveRoleRequest(string Name, IReadOnlyList<string> Permissions);
