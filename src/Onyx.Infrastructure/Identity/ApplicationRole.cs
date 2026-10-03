using Microsoft.AspNetCore.Identity;

namespace Onyx.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
        Id = Guid.CreateVersion7();
    }

    public ApplicationRole(string roleName)
        : this()
    {
        Name = roleName;
    }

    /// <summary>True only for SuperAdmin: holds every permission implicitly, cannot be edited or deleted.</summary>
    public bool IsSystem { get; set; }
}
