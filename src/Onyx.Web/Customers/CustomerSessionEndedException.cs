namespace Onyx.Web.Customers;

/// <summary>The API no longer accepts the customer's session (signed out elsewhere, password changed, disabled or expired).</summary>
public sealed class CustomerSessionEndedException : Exception
{
    public CustomerSessionEndedException()
        : base("The customer session has ended.")
    {
    }

    public CustomerSessionEndedException(string message)
        : base(message)
    {
    }

    public CustomerSessionEndedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
