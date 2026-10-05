namespace Onyx.Application.Identity;

/// <summary>Read-only contact details of admin users, for features that need to email or name admins.</summary>
public interface IUserDirectory
{
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Active users who hold <paramref name="permission"/> through a role (SuperAdmins hold every permission).</summary>
    Task<IReadOnlyList<UserContact>> ListWithPermissionAsync(string permission, CancellationToken cancellationToken);

    /// <summary>Display names by user id (inactive users included, so history keeps its names).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
