using Microsoft.AspNetCore.Identity;

namespace Onyx.Infrastructure.Identity;

/// <summary>Admin account (ASP.NET Core Identity). There is no public registration: accounts are invited or bootstrapped.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 100;

    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public string DisplayName { get; set; } = string.Empty;

    public string PreferredLanguage { get; set; } = "ar";

    /// <summary>Disabled accounts cannot sign in and all their sessions are revoked.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    /// <summary>Last accepted TOTP time step; codes for this step or earlier are rejected (replay protection).</summary>
    public long? LastTotpTimeStep { get; set; }
}
