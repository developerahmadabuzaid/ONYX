using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Identity;

/// <summary>Bound from the <c>IdentitySecurity</c> section.</summary>
public sealed class IdentitySecurityOptions
{
    public const string SectionName = "IdentitySecurity";

    /// <summary>When true, every admin must use an authenticator, not only privileged roles.</summary>
    public bool RequireTwoFactorForAllUsers { get; set; }

    /// <summary>Base URL of the Onyx.Web admin area, used to build invitation and password-reset links (https; http only for localhost).</summary>
    [Required]
    public string AdminPortalBaseUrl { get; set; } = string.Empty;

    [Range(1, 168)]
    public int InvitationLifetimeHours { get; set; } = 72;

    [Range(10, 240)]
    public int PasswordResetLifetimeMinutes { get; set; } = 60;
}
