using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Onyx.Domain.Leads;
using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

/// <summary>
/// The machine-readable export (format <c>onyx.data-subject-export</c>, version 1): UTF-8 JSON with Arabic and English text as
/// characters, sections in a fixed order and items sorted by time then id, so the same data always produces the same file.
/// One-time link tokens in email bodies are replaced. It holds what the subject provided and what Onyx recorded about them; it leaves out credentials and tokens, internal
/// security metadata (IP addresses, change diffs) and other people (staff names behind notes, owners or actions).
/// </summary>
public static partial class DataSubjectExportBuilder
{
    /// <summary>Replaces one-time link tokens (booking manage links, invitation and reset links) in email bodies.</summary>
    public const string RedactedToken = "token=[removed]";

    public const string Format = "onyx.data-subject-export";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static string Build(
        DataSubjectRequest request, string subjectEmail, SubjectRecords records, IReadOnlyList<SubjectEmailRecord> emails, AccountExport? account, DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(emails);
        var consents = records.Consents.ToDictionary(consent => consent.InquiryId);
        var document = new
        {
            format = Format,
            version = FormatVersion,
            request = new { id = request.Id, type = request.Type.ToString(), receivedAtUtc = request.ReceivedAtUtc, generatedAtUtc },
            subject = new { email = subjectEmail },
            leads = records.Leads.OrderBy(lead => lead.FirstInquiryAtUtc).ThenBy(lead => lead.Id).Select(lead => new
            {
                id = lead.Id,
                displayName = lead.DisplayName,
                company = lead.Company,
                stage = lead.Stage.ToString(),
                source = lead.Source.ToString(),
                priority = lead.Priority.ToString(),
                languageCode = lead.LanguageCode,
                firstInquiryAtUtc = lead.FirstInquiryAtUtc,
                lastInquiryAtUtc = lead.LastInquiryAtUtc,
                closedAtUtc = lead.ClosedAtUtc,
                notes = records.Notes.Where(note => note.LeadId == lead.Id).OrderBy(note => note.CreatedAtUtc).ThenBy(note => note.Id)
                    .Select(note => new { createdAtUtc = note.CreatedAtUtc, body = note.Body }),
                activities = records.Activities.Where(activity => activity.LeadId == lead.Id).OrderBy(activity => activity.OccurredAtUtc).ThenBy(activity => activity.Id)
                    .Select(activity => new { occurredAtUtc = activity.OccurredAtUtc, kind = activity.Kind.ToString(), detail = activity.Detail }),
            }),
            inquiries = records.Inquiries.OrderBy(inquiry => inquiry.SubmittedAtUtc).ThenBy(inquiry => inquiry.Id).Select(inquiry => new
            {
                id = inquiry.Id,
                leadId = inquiry.LeadId,
                type = inquiry.Type.ToString(),
                languageCode = inquiry.LanguageCode,
                submittedAtUtc = inquiry.SubmittedAtUtc,
                name = inquiry.Name,
                email = inquiry.Email,
                phone = inquiry.Phone,
                company = inquiry.Company,
                country = inquiry.Country,
                message = inquiry.Message,
                answers = new { version = inquiry.Answers.Version, values = inquiry.Answers.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value) },
                serviceId = inquiry.ServiceId,
                projectId = inquiry.ProjectId,
                callToActionId = inquiry.CallToActionId,
                attribution = Attribution(inquiry.Attribution),
                consent = consents.TryGetValue(inquiry.Id, out var consent)
                    ? new
                    {
                        contactConsent = consent.ContactConsent,
                        marketingConsent = consent.MarketingConsent,
                        marketingWithdrawnAtUtc = consent.MarketingWithdrawnAtUtc,
                        consentVersion = consent.ConsentVersion,
                        givenAtUtc = consent.GivenAtUtc,
                    }
                    : null,
            }),
            bookings = records.Bookings.OrderBy(booking => booking.StartUtc).ThenBy(booking => booking.Id).Select(booking => new
            {
                id = booking.Id,
                leadId = booking.LeadId,
                startUtc = booking.StartUtc,
                endUtc = booking.EndUtc,
                timeZoneId = booking.TimeZoneId,
                status = booking.Status.ToString(),
                languageCode = booking.LanguageCode,
                cancelledAtUtc = booking.CancelledAtUtc,
            }),
            emails = emails.OrderBy(email => email.CreatedAtUtc).ThenBy(email => email.TemplateKey, StringComparer.Ordinal).Select(email => new
            {
                createdAtUtc = email.CreatedAtUtc,
                sentAtUtc = email.SentAtUtc,
                status = email.Status,
                template = email.TemplateKey,
                languageCode = email.LanguageCode,
                subject = email.Subject,
                textBody = RedactTokens(email.TextBody),
            }),
            account,
        };

        return JsonSerializer.Serialize(document, Options);
    }

    public static string RedactTokens(string text) => TokenParameter().Replace(text ?? string.Empty, RedactedToken);

    public static string FileName(DataSubjectRequest request) => $"onyx-data-export-{request.Id:N}.json";

    [System.Text.RegularExpressions.GeneratedRegex(@"token=[^&\s""<>]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex TokenParameter();

    private static object Attribution(Attribution attribution) => new
    {
        analyticsConsent = attribution.AnalyticsConsent,
        firstTouch = attribution.FirstTouch,
        lastTouch = attribution.LastTouch,
        submissionPath = attribution.SubmissionPath,
        languageCode = attribution.LanguageCode,
        market = attribution.Market,
        deviceClass = attribution.DeviceClass,
        analyticsClientId = attribution.AnalyticsClientId,
    };
}
