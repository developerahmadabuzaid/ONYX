namespace Onyx.Web.Customers;

/// <summary>Forgot password and resend verification.</summary>
public sealed class EmailForm
{
    public string? Email { get; set; }

    public string? ReturnUrl { get; set; }
}
