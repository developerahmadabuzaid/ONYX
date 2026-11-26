namespace Onyx.Web.Customers;

/// <summary>Customer sign-up form. Marketing consent is separate from the required terms acceptance and off by default.</summary>
public sealed class RegisterForm
{
    public string? DisplayName { get; set; }

    public string? Company { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }

    public bool AcceptTerms { get; set; }

    public bool MarketingConsent { get; set; }

    public string? ReturnUrl { get; set; }
}
