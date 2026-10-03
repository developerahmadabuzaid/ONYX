namespace Onyx.Application.Identity;

public sealed record ActorPermissions(Guid UserId, bool IsSuperAdmin, IReadOnlySet<string> Permissions);
