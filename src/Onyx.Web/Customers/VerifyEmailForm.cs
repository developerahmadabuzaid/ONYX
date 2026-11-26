namespace Onyx.Web.Customers;

/// <summary>Email confirmation. Confirmed with a POST so link scanners that open the email link do not consume the token.</summary>
public sealed class VerifyEmailForm
{
    public Guid Id { get; set; }

    public string? Token { get; set; }

    public string? ReturnUrl { get; set; }
}
