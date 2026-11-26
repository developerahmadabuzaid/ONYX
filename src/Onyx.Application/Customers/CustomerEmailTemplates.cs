namespace Onyx.Application.Customers;

/// <summary>Keys of the customer email templates (implemented in Onyx.Infrastructure; copy is admin-editable).</summary>
public static class CustomerEmailTemplates
{
    public const string VerifyEmail = "customer.verify-email";
    public const string AlreadyRegistered = "customer.already-registered";
    public const string PasswordReset = "customer.password-reset";
    public const string SubscriptionUpdated = "customer.subscription-updated";
}
