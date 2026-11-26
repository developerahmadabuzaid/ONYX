using System.ComponentModel.DataAnnotations;

namespace Onyx.Application.Customers;

/// <summary>Bound from <c>CustomerAccounts</c> and registered as a singleton by the composition root. No secrets.</summary>
public sealed class CustomerAccountOptions
{
    public const string SectionName = "CustomerAccounts";

    /// <summary>Version of the terms and privacy policy a customer accepts at sign-up; bump it when the documents change.</summary>
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string TermsVersion { get; set; } = "1";

    [Range(1, 168)]
    public int VerificationLifetimeHours { get; set; } = 48;

    [Range(10, 240)]
    public int PasswordResetLifetimeMinutes { get; set; } = 60;

    [Range(1, 30)]
    public int SessionIdleDays { get; set; } = 7;

    [Range(1, 90)]
    public int SessionAbsoluteDays { get; set; } = 30;

    [Range(3, 20)]
    public int MaxFailedSignIns { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;
}
