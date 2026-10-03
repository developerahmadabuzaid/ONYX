namespace Onyx.Infrastructure.Identity;

/// <summary>One permission (from the Contracts catalogue) granted to a role.</summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public string Permission { get; set; } = string.Empty;
}
