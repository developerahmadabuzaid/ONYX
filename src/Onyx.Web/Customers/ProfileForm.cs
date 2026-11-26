namespace Onyx.Web.Customers;

/// <summary>The profile page: profile details, password change and account deletion (one form each, told apart by Command).</summary>
public sealed class ProfileForm
{
    public string? DisplayName { get; set; }

    public string? Company { get; set; }

    public string? Language { get; set; }

    public bool MarketingConsent { get; set; }

    public uint Version { get; set; }

    public string? Email { get; set; }

    public string? CurrentPassword { get; set; }

    public string? NewPassword { get; set; }

    public string? ConfirmPassword { get; set; }

    public string? DeletePassword { get; set; }

    public string? Command { get; set; }
}
