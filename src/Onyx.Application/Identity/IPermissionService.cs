namespace Onyx.Application.Identity;

/// <summary>Resolves effective permissions server-side (never from the token). Results are cached briefly and invalidated on change.</summary>
public interface IPermissionService
{
    Task<ActorPermissions> GetAsync(Guid userId, CancellationToken cancellationToken);

    void Invalidate(Guid userId);

    Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken);
}
