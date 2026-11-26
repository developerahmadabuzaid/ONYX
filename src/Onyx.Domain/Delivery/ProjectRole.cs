namespace Onyx.Domain.Delivery;

/// <summary>A member's role in one project; higher values include the capabilities of lower ones. Stored as text.</summary>
public enum ProjectRole
{
    Viewer = 0,
    Contributor = 1,
    Developer = 2,
    Manager = 3,
    Admin = 4,
}
