namespace Onyx.Web.Customers;

/// <summary>
/// The customer sign-in of Onyx.Web: its own cookie scheme, separate from the staff BFF scheme (<c>__Host-onyx.admin</c>). The
/// opaque API session token lives only inside this encrypted, HttpOnly cookie; it never reaches browser JavaScript.
/// </summary>
public static class CustomerWebAuth
{
    public const string Scheme = "OnyxCustomer";
    public const string CookieName = "__Host-onyx.customer";
    public const string SessionTokenName = "customer_session";
    public const string CustomerIdClaim = "onyx:customer";
    public const string NameClaim = "onyx:customer_name";
}
