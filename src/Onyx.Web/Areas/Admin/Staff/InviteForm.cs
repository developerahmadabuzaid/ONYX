namespace Onyx.Web.Areas.Admin.Staff;

/// <summary>Invite form; roles arrive as repeated <c>Roles</c> checkbox values.</summary>
public sealed class InviteForm
{
    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string? Language { get; set; }

    public List<string> Roles { get; set; } = [];
}
