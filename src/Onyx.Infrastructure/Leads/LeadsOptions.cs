using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Onyx.Infrastructure.Leads;

/// <summary>
/// Bound from <c>Leads</c> in Onyx.API and Onyx.Worker. Recipient addresses are business settings (never code); with none
/// configured, inquiries are still stored and shown in Admin, only the internal email is skipped.
/// </summary>
public sealed class LeadsOptions
{
    public const string SectionName = "Leads";

    /// <summary>Identifies the form consent wording; change it whenever that wording changes.</summary>
    [Required]
    [RegularExpression("^[A-Za-z0-9._-]{1,20}$")]
    public string ConsentVersion { get; set; } = "1";

    public IReadOnlyList<string> DefaultRecipients { get; set; } = [];

    /// <summary>Per inquiry type (e.g. <c>Consultation</c>), overriding <see cref="DefaultRecipients"/>.</summary>
    public IDictionary<string, string[]> RecipientsByType { get; set; } = new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>How long after the consultation the visitor link still serves the calendar file.</summary>
    [Range(1, 24 * 30)]
    public int ManageLinkHoursAfterSlot { get; set; } = 24;

    public static bool IsValidAddress(string address) =>
        MailAddress.TryCreate(address, out var parsed) && string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase);
}
