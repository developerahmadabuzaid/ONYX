using Onyx.Application.Leads;

namespace Onyx.Application.Privacy;

/// <summary>
/// Erases the personal data in a set of lead records, the same way for data-subject erasure and retention anonymization:
/// inquiries and leads keep only non-personal fields, marketing consent is withdrawn (the consent evidence itself has no
/// personal fields and remains), upcoming bookings are cancelled and manage links stop working, staff notes are deleted, and
/// audit entries about these records lose the visitor IP. Joins the caller's unit of work; idempotent.
/// </summary>
public sealed class SubjectEraser(IPersonalDataStore store, ILeadRepository leads)
{
    public async Task<ErasureCounts> EraseAsync(SubjectRecords records, Guid? actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var inquiry in records.Inquiries)
        {
            inquiry.Erase(now);
        }

        foreach (var consent in records.Consents.Where(consent => consent.HasMarketingConsent))
        {
            consent.WithdrawMarketing(actorId, now);
        }

        foreach (var booking in records.Bookings)
        {
            booking.EraseVisitorAccess(now);
        }

        foreach (var lead in records.Leads)
        {
            lead.Erase(actorId, now);
            leads.AddPendingActivities(lead);
        }

        var leadIds = records.Leads.Select(lead => lead.Id).ToList();
        var notes = await store.DeleteNotesAsync(leadIds, cancellationToken).ConfigureAwait(false);
        var entityIds = leadIds
            .Concat(records.Inquiries.Select(inquiry => inquiry.Id))
            .Concat(records.Consents.Select(consent => consent.Id))
            .Concat(records.Bookings.Select(booking => booking.Id))
            .Distinct()
            .ToList();
        var audit = await store.MinimizeAuditAsync(entityIds, cancellationToken).ConfigureAwait(false);

        return new ErasureCounts(records.Leads.Count, records.Inquiries.Count, records.Consents.Count, records.Bookings.Count, notes, audit);
    }
}
