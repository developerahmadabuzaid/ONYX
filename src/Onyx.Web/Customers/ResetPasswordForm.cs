namespace Onyx.Web.Customers;

/// <summary>New password from an emailed reset link; the token travels in the form body, never in a link the page sends anywhere.</summary>
public sealed class ResetPasswordForm
{
    public Guid Id { get; set; }

    public string? Token { get; set; }

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }
}
