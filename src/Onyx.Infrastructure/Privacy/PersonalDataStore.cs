using Microsoft.EntityFrameworkCore;
using Onyx.Application.Privacy;
using Onyx.Domain.Booking;
using Onyx.Domain.Privacy;
using Onyx.Infrastructure.Email;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Privacy;

/// <summary>
/// Loads and removes personal data for data-subject requests and retention. Every query is bounded by the subject's address,
/// explicit ids or a batch size, and uses bound parameters only. Set-based statements join the caller's transaction.
/// </summary>
internal sealed class PersonalDataStore(ApplicationDbContext dbContext) : IPersonalDataStore
{
    public async Task<SubjectRecords> LoadSubjectAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var email = normalizedEmail.Trim().ToLowerInvariant();
        var inquiryLeadIds = dbContext.Inquiries.Where(inquiry => inquiry.NormalizedEmail == email).Select(inquiry => inquiry.LeadId);
        var leadIds = await dbContext.Leads
            .Where(lead => lead.NormalizedEmail == email || inquiryLeadIds.Contains(lead.Id))
            .Select(lead => lead.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Inquiries are attached to a lead only when their address matches it, so a lead's inquiries all belong to the subject.
        return await LoadAsync(leadIds, email, cancellationToken).ConfigureAwait(false);
    }

    public Task<SubjectRecords> LoadLeadsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken) =>
        LoadAsync(leadIds.ToList(), null, cancellationToken);

    public async Task<IReadOnlyList<SubjectEmailRecord>> ListEmailsToAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var email = normalizedEmail.Trim().ToLowerInvariant();
        var messages = await dbContext.EmailMessages.AsNoTracking()
            .Where(message => message.ToAddress.ToLower() == email)
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .Select(message => new { message.TemplateKey, message.LanguageCode, message.Subject, message.TextBody, message.Status, message.CreatedAtUtc, message.SentAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return messages
            .Select(message => new SubjectEmailRecord(
                message.TemplateKey, message.LanguageCode, message.Subject, message.TextBody, message.Status.ToString(), message.CreatedAtUtc, message.SentAtUtc))
            .ToList();
    }

    public Task<int> DeleteEmailsToAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var email = normalizedEmail.Trim().ToLowerInvariant();
        return dbContext.EmailMessages.Where(message => message.ToAddress.ToLower() == email).ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteNotesAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken) =>
        leadIds.Count == 0 ? Task.FromResult(0) : dbContext.LeadNotes.Where(note => leadIds.Contains(note.LeadId)).ExecuteDeleteAsync(cancellationToken);

    public async Task<int> MinimizeAuditAsync(IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken)
    {
        if (entityIds.Count == 0)
        {
            return 0;
        }

        var ids = entityIds.Select(id => id.ToString()).ToArray();

        // Visitor-originated entries carry the visitor's IP. Entries written before Lead.Company was redacted and before the
        // manage-token hash was excluded may still hold those values: remove the keys from the change set (jsonb).
        return await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE audit_logs
            SET ip_address = NULL,
                changes = CASE WHEN changes IS NULL THEN NULL ELSE changes - 'Company' - 'ManageTokenHash' END
            WHERE entity_id = ANY({ids})
              AND entity_type IN ('Lead', 'Inquiry', 'ConsentRecord', 'ConsultationBooking')
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListLeadsDueAsync(DateTimeOffset cutoff, DateTimeOffset now, bool excludeErased, int take, CancellationToken cancellationToken) =>
        await DueLeads(cutoff, now, excludeErased).OrderBy(lead => lead.LastInquiryAtUtc).ThenBy(lead => lead.Id).Select(lead => lead.Id).Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<long> CountLeadsDueAsync(DateTimeOffset cutoff, DateTimeOffset now, bool excludeErased, CancellationToken cancellationToken) =>
        DueLeads(cutoff, now, excludeErased).LongCountAsync(cancellationToken);

    public async Task<int> DeleteLeadsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken)
    {
        if (leadIds.Count == 0)
        {
            return 0;
        }

        var inquiryIds = dbContext.Inquiries.Where(inquiry => leadIds.Contains(inquiry.LeadId)).Select(inquiry => inquiry.Id);
        var auditIds = await dbContext.Inquiries.Where(inquiry => leadIds.Contains(inquiry.LeadId)).Select(inquiry => inquiry.Id)
            .Concat(dbContext.ConsentRecords.Where(consent => leadIds.Contains(consent.LeadId)).Select(consent => consent.Id))
            .Concat(dbContext.ConsultationBookings.Where(booking => leadIds.Contains(booking.LeadId)).Select(booking => booking.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.ConsultationBookings.Where(booking => leadIds.Contains(booking.LeadId) || inquiryIds.Contains(booking.InquiryId))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.ConsentRecords.Where(consent => leadIds.Contains(consent.LeadId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.LeadNotes.Where(note => leadIds.Contains(note.LeadId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.LeadActivities.Where(activity => leadIds.Contains(activity.LeadId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Inquiries.Where(inquiry => leadIds.Contains(inquiry.LeadId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await dbContext.Leads.Where(lead => leadIds.Contains(lead.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // The audit entries about the deleted records remain (append-only evidence) without visitor IPs or personal values.
        await MinimizeAuditAsync(auditIds.Concat(leadIds).ToList(), cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    public async Task<int> ApplyRetentionBatchAsync(RetentionCategory category, RetentionAction action, DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        switch (category, action)
        {
            case (RetentionCategory.EmailMessages, RetentionAction.Delete):
                var emails = DueEmails(cutoff).OrderBy(message => message.CreatedAtUtc).Select(message => message.Id).Take(batchSize);
                return await dbContext.EmailMessages.Where(message => emails.Contains(message.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            case (RetentionCategory.CookieConsentReceipts, RetentionAction.Delete):
                var receipts = dbContext.CookieConsentReceipts.Where(receipt => receipt.RecordedAtUtc < cutoff).OrderBy(receipt => receipt.RecordedAtUtc).Select(receipt => receipt.Id).Take(batchSize);
                return await dbContext.CookieConsentReceipts.Where(receipt => receipts.Contains(receipt.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            case (RetentionCategory.Notifications, RetentionAction.Delete):
                var notifications = dbContext.AdminNotifications.Where(item => item.CreatedAtUtc < cutoff).OrderBy(item => item.CreatedAtUtc).Select(item => item.Id).Take(batchSize);
                return await dbContext.AdminNotifications.Where(item => notifications.Contains(item.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            case (RetentionCategory.AuditLogs, RetentionAction.Delete):
                var oldEntries = dbContext.AuditLogs.Where(log => log.OccurredAtUtc < cutoff).OrderBy(log => log.OccurredAtUtc).Select(log => log.Id).Take(batchSize);
                return await dbContext.AuditLogs.Where(log => oldEntries.Contains(log.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            case (RetentionCategory.AuditLogs, RetentionAction.Anonymize):
                var identified = DueIdentifiedAudit(cutoff).OrderBy(log => log.OccurredAtUtc).Select(log => log.Id).Take(batchSize);
                return await dbContext.AuditLogs.Where(log => identified.Contains(log.Id))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(log => log.IpAddress, (string?)null).SetProperty(log => log.UserName, (string?)null), cancellationToken)
                    .ConfigureAwait(false);

            case (RetentionCategory.DataSubjectRequests, RetentionAction.Delete):
                var closed = DueRequests(cutoff).OrderBy(request => request.CompletedAtUtc).Select(request => request.Id).Take(batchSize);
                return await dbContext.DataSubjectRequests.Where(request => closed.Contains(request.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            default:
                throw new InvalidOperationException($"Retention {action} is not supported for {category}.");
        }
    }

    public Task<long> CountRetentionDueAsync(RetentionCategory category, RetentionAction action, DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        (category, action) switch
        {
            (RetentionCategory.EmailMessages, RetentionAction.Delete) => DueEmails(cutoff).LongCountAsync(cancellationToken),
            (RetentionCategory.CookieConsentReceipts, RetentionAction.Delete) => dbContext.CookieConsentReceipts.LongCountAsync(receipt => receipt.RecordedAtUtc < cutoff, cancellationToken),
            (RetentionCategory.Notifications, RetentionAction.Delete) => dbContext.AdminNotifications.LongCountAsync(item => item.CreatedAtUtc < cutoff, cancellationToken),
            (RetentionCategory.AuditLogs, RetentionAction.Delete) => dbContext.AuditLogs.LongCountAsync(log => log.OccurredAtUtc < cutoff, cancellationToken),
            (RetentionCategory.AuditLogs, RetentionAction.Anonymize) => DueIdentifiedAudit(cutoff).LongCountAsync(cancellationToken),
            (RetentionCategory.DataSubjectRequests, RetentionAction.Delete) => DueRequests(cutoff).LongCountAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Retention {action} is not supported for {category}."),
        };

    private async Task<SubjectRecords> LoadAsync(List<Guid> leadIds, string? email, CancellationToken cancellationToken)
    {
        var leads = await dbContext.Leads.Where(lead => leadIds.Contains(lead.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var inquiries = await dbContext.Inquiries
            .Where(inquiry => leadIds.Contains(inquiry.LeadId) || (email != null && inquiry.NormalizedEmail == email))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var inquiryIds = inquiries.Select(inquiry => inquiry.Id).ToList();
        var consents = await dbContext.ConsentRecords.Where(consent => inquiryIds.Contains(consent.InquiryId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var bookings = await dbContext.ConsultationBookings
            .Where(booking => leadIds.Contains(booking.LeadId) || inquiryIds.Contains(booking.InquiryId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var notes = await dbContext.LeadNotes.Where(note => leadIds.Contains(note.LeadId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var activities = await dbContext.LeadActivities.AsNoTracking().Where(activity => leadIds.Contains(activity.LeadId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new SubjectRecords(leads, inquiries, consents, bookings, notes, activities);
    }

    private IQueryable<Domain.Leads.Lead> DueLeads(DateTimeOffset cutoff, DateTimeOffset now, bool excludeErased) =>
        dbContext.Leads.Where(lead =>
            lead.LastInquiryAtUtc < cutoff
            && (!excludeErased || lead.ErasedAtUtc == null)
            && !dbContext.ConsultationBookings.Any(booking => booking.LeadId == lead.Id && booking.Status == BookingStatus.Confirmed && booking.EndUtc > now));

    private IQueryable<EmailMessage> DueEmails(DateTimeOffset cutoff) =>
        dbContext.EmailMessages.Where(message => message.Status != EmailMessageStatus.Pending && message.CreatedAtUtc < cutoff);

    private IQueryable<Domain.Auditing.AuditLog> DueIdentifiedAudit(DateTimeOffset cutoff) =>
        dbContext.AuditLogs.Where(log => log.OccurredAtUtc < cutoff && (log.IpAddress != null || log.UserName != null));

    private IQueryable<DataSubjectRequest> DueRequests(DateTimeOffset cutoff) =>
        dbContext.DataSubjectRequests.Where(request =>
            (request.Status == DataSubjectRequestStatus.Completed || request.Status == DataSubjectRequestStatus.Rejected)
            && request.CompletedAtUtc != null && request.CompletedAtUtc < cutoff);
}
