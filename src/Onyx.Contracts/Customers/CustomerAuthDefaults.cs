namespace Onyx.Contracts.Customers;

/// <summary>
/// How Onyx.Web presents a customer session to Onyx.API: <c>Authorization: OnyxCustomer {token}</c>. A different scheme name
/// from staff <c>Bearer</c> tokens, so neither credential is ever read by the other side's authentication handler.
/// </summary>
public static class CustomerAuthDefaults
{
    public const string AuthorizationScheme = "OnyxCustomer";
}
