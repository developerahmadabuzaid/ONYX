using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Identity;

/// <summary>Bound from the <c>Sessions</c> section (docs/architecture.md §25.1).</summary>
public sealed class SessionOptions
{
    public const string SectionName = "Sessions";

    [Range(5, 1_440)]
    public int IdleTimeoutMinutes { get; set; } = 60;

    [Range(1, 72)]
    public int AbsoluteLifetimeHours { get; set; } = 12;

    /// <summary>Window in which a just-rotated refresh token returns the same successor instead of triggering reuse detection.</summary>
    [Range(0, 120)]
    public int RefreshGraceSeconds { get; set; } = 30;

    [Range(0, 10)]
    public int MaxGraceRedemptions { get; set; } = 3;

    [Range(1, 30)]
    public int TwoFactorChallengeMinutes { get; set; } = 5;

    [Range(1, 60)]
    public int EnrollmentChallengeMinutes { get; set; } = 15;

    [Range(1, 10)]
    public int MaxChallengeAttempts { get; set; } = 5;

    /// <summary>How long a session check is cached per instance; revocations elsewhere take effect within this window.</summary>
    [Range(0, 60)]
    public int ValidationCacheSeconds { get; set; } = 15;

    [Range(0, 600)]
    public int PermissionCacheSeconds { get; set; } = 60;
}
