namespace Onyx.Infrastructure.Identity;

/// <summary>ASP.NET Core Data Protection key XML, shared by all API instances and included in database backups.</summary>
public sealed class DataProtectionKeyRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string? FriendlyName { get; set; }

    public string Xml { get; set; } = string.Empty;
}
