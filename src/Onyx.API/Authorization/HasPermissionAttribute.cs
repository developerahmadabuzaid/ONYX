using Microsoft.AspNetCore.Authorization;

namespace Onyx.API.Authorization;

/// <summary>Requires an authenticated admin holding <paramref name="permission"/> (resolved server-side, never from the token).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicyProvider.PolicyPrefix + permission)
{
    public string Permission { get; } = permission;
}
