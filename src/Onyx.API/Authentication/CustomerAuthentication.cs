namespace Onyx.API.Authentication;

/// <summary>
/// The customer authentication scheme and policy (docs/architecture.md §24.8). Customer endpoints opt in with
/// <c>[Authorize(Policy = CustomerAuthentication.Policy)]</c>; every other endpoint authenticates only the staff JWT scheme, so a
/// customer credential is never even read there.
/// </summary>
public static class CustomerAuthentication
{
    public const string Scheme = "OnyxCustomer";
    public const string Policy = "Customer";
    public const string CustomerIdClaim = "onyx:customer";
    public const string SessionIdClaim = "onyx:customer_session";
}
