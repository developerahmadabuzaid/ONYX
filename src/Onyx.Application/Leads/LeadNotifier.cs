using Onyx.Application.Email;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// Internal notification emails. They name the kind of request and link to the lead in Admin instead of carrying the visitor
/// contact details (architecture.md §13.5); recipients come from settings.
/// </summary>
public sealed class LeadNotifier(IEmailOutbox emailOutbox, LeadSettings settings)
{
    public const string InquiryReceivedTemplate = "leads.inquiry-received";
    public const string InquiryInternalTemplate = "leads.inquiry-internal";

    /// <summary>Internal emails are written in the default admin language; the visitor language is stated as a value.</summary>
    public const string InternalLanguage = "ar";

    public Uri? AdminLeadLink(Guid leadId) =>
        settings.AdminBaseUrl is { } admin ? new Uri(admin, $"/admin/leads/{leadId:D}") : null;

    public async Task QueueInternalAsync(InquiryType type, string templateKey, Guid leadId, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var recipient in settings.RecipientsFor(type))
        {
            var model = new Dictionary<string, string>(values, StringComparer.Ordinal);
            if (AdminLeadLink(leadId) is { } link)
            {
                model["link"] = link.AbsoluteUri;
            }

            var queued = await emailOutbox.QueueAsync(new EmailRequest(templateKey, InternalLanguage, recipient, null, model), cancellationToken).ConfigureAwait(false);
            if (queued.IsFailure)
            {
                throw new InvalidOperationException($"Internal email '{templateKey}' could not be queued: {queued.Error!.Code}.");
            }
        }
    }
}
