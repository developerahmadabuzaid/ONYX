using Onyx.Domain.Common;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// Stores a prepared inquiry with its consent record and attaches it to the open lead for the same email, or opens a new lead.
/// Joins the caller's unit of work: inquiry, consent, lead, activities and the <see cref="InquirySubmitted"/> outbox event
/// commit in one transaction. Two simultaneous first inquiries from one address may open two leads; staff can see both.
/// </summary>
public sealed class InquiryRecorder(IInquiryRepository inquiries, ILeadRepository leads, LeadSettings settings, TimeProvider timeProvider)
{
    internal async Task<Result<(Inquiry Inquiry, Lead Lead)>> RecordAsync(PreparedInquiry prepared, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (prepared.IsSpam || prepared.Contact is null || prepared.Attribution is null)
        {
            throw new InvalidOperationException("Spam submissions are never recorded.");
        }

        var now = timeProvider.GetUtcNow();
        var lead = await leads.FindOpenByEmailAsync(prepared.Contact.NormalizedEmail, cancellationToken).ConfigureAwait(false);
        var isNew = lead is null;
        lead ??= Lead.Open(prepared.Contact, prepared.Type, prepared.PillarId, prepared.LanguageCode, now);
        IReadOnlyList<Inquiry> earlier = isNew ? [] : await inquiries.ListForLeadAsync(lead.Id, cancellationToken).ConfigureAwait(false);

        var inquiry = Inquiry.Submit(
            prepared.Type, prepared.LanguageCode, prepared.Contact, prepared.Message, prepared.Answers, prepared.ServiceId, prepared.ProjectId,
            prepared.CallToActionId, prepared.Attribution, lead.Id, now);
        if (inquiry.IsFailure)
        {
            return inquiry.Error!;
        }

        var consent = ConsentRecord.Give(inquiry.Value.Id, lead.Id, contactConsent: true, prepared.MarketingConsent, settings.ConsentVersion, prepared.LanguageCode, now);
        if (consent.IsFailure)
        {
            return consent.Error!;
        }

        lead.AttachInquiry(inquiry.Value, earlier, prepared.PillarId);
        if (isNew)
        {
            leads.Add(lead);
        }
        else
        {
            leads.AddPendingActivities(lead);
        }

        inquiries.Add(inquiry.Value, consent.Value);
        return (inquiry.Value, lead);
    }

    /// <summary>A short, non-secret reference the visitor can quote (it grants no access).</summary>
    public static string Reference(Guid inquiryId) => "ONX-" + inquiryId.ToString("N")[^8..].ToUpperInvariant();
}
