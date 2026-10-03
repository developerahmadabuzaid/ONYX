namespace Onyx.Application.Identity;

/// <summary>Read-only contact details of admin users, for features that need to email the current admin.</summary>
public interface IUserDirectory
{
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken cancellationToken);
}
