using System.Globalization;
using System.Text;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// CSV export of leads (RFC 4180, UTF-8 with BOM so spreadsheet apps read Arabic correctly). Every field is quoted, and values
/// that a spreadsheet would treat as a formula are prefixed with an apostrophe (CSV injection protection).
/// </summary>
public static class LeadCsv
{
    private static readonly string[] Header =
    [
        "lead_id", "name", "email", "company", "stage", "source", "priority", "score", "owner", "inquiries", "language",
        "first_inquiry_utc", "last_inquiry_utc", "marketing_consent",
    ];

    public static string Write(IEnumerable<Lead> leads, IReadOnlyDictionary<Guid, string> ownerNames, IReadOnlySet<Guid> marketingConsent)
    {
        ArgumentNullException.ThrowIfNull(leads);
        ArgumentNullException.ThrowIfNull(ownerNames);
        ArgumentNullException.ThrowIfNull(marketingConsent);
        var csv = new StringBuilder().Append((char)0xFEFF);
        AppendRow(csv, Header);
        foreach (var lead in leads)
        {
            AppendRow(csv,
            [
                lead.Id.ToString(),
                lead.DisplayName,
                lead.NormalizedEmail,
                lead.Company ?? string.Empty,
                lead.Stage.ToString(),
                lead.Source.ToString(),
                lead.Priority.ToString(),
                lead.Score.ToString(CultureInfo.InvariantCulture),
                lead.OwnerId is { } owner && ownerNames.TryGetValue(owner, out var ownerName) ? ownerName : string.Empty,
                lead.InquiryCount.ToString(CultureInfo.InvariantCulture),
                lead.LanguageCode,
                lead.FirstInquiryAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                lead.LastInquiryAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                marketingConsent.Contains(lead.Id) ? "yes" : "no",
            ]);
        }

        return csv.ToString();
    }

    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> fields) =>
        csv.Append(string.Join(',', fields.Select(Escape))).Append("\r\n");
}
