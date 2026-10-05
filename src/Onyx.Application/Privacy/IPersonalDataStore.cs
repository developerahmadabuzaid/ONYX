using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

/// <summary>
/// Privacy reads and set-based removals across the lead aggregates and the email outbox. Personal-field changes themselves
/// happen in the domain (<c>Erase</c> methods); this port only loads, deletes and minimizes.
/// </summary>
public interface IPersonalDataStore
{
    /// <summary>Tracked records for the normalized email (inquiries and leads with that address, and everything under them).</summary>
    Task<SubjectRecords> LoadSubjectAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Tracked records for these leads (retention).</summary>
    Task<SubjectRecords> LoadLeadsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubjectEmailRecord>> ListEmailsToAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Deletes every email (pending, sent or dead-lettered) addressed to the subject, so nothing queued can still send it.</summary>
    Task<int> DeleteEmailsToAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<int> DeleteNotesAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken);

    /// <summary>
    /// Removes personal values from audit entries about these records: the visitor IP address, and values that older entries
    /// recorded before the fields were redacted (lead company, booking manage-token hash). The entries themselves remain.
    /// </summary>
    Task<int> MinimizeAuditAsync(IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken);

    /// <summary>Leads whose last inquiry is older than <paramref name="cutoff"/> and that have no upcoming confirmed booking.</summary>
    Task<IReadOnlyList<Guid>> ListLeadsDueAsync(DateTimeOffset cutoff, DateTimeOffset now, bool excludeErased, int take, CancellationToken cancellationToken);

    Task<long> CountLeadsDueAsync(DateTimeOffset cutoff, DateTimeOffset now, bool excludeErased, CancellationToken cancellationToken);

    /// <summary>Deletes the leads with their inquiries, consent records, notes, activities and bookings.</summary>
    Task<int> DeleteLeadsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken);

    /// <summary>Set-based retention for the categories without domain rules (emails, receipts, notifications, audit, requests).</summary>
    Task<int> ApplyRetentionBatchAsync(RetentionCategory category, RetentionAction action, DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);

    Task<long> CountRetentionDueAsync(RetentionCategory category, RetentionAction action, DateTimeOffset cutoff, CancellationToken cancellationToken);
}
