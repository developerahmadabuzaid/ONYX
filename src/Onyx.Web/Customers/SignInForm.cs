namespace Onyx.Web.Customers;

/// <summary>Customer sign-in form.</summary>
public sealed class SignInForm
{
    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}
