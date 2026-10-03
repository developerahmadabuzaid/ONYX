using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Configuration;

/// <summary>
/// Bound from <c>DataProtection</c>. Onyx.Web has no database, so its key ring (which encrypts the admin cookie) lives in a
/// directory that must be shared by all Web instances and included in backups.
/// </summary>
public sealed class DataProtectionSettings
{
    public const string SectionName = "DataProtection";

    [Required]
    public string KeysDirectory { get; set; } = string.Empty;
}
