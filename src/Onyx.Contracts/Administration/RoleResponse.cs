namespace Onyx.Contracts.Administration;

/// <summary><see cref="IsSystem"/> marks the built-in SuperAdmin role: it always holds every permission and cannot be edited or deleted.</summary>
public sealed record RoleResponse(Guid Id, string Name, bool IsSystem, IReadOnlyList<string> Permissions, int UserCount);
