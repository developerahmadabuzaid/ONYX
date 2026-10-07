namespace Onyx.Domain.Privacy;

/// <summary>
/// Data categories with a retention policy (architecture.md §14). Only categories whose data exists today are listed; tool runs
/// and advisor conversations get theirs with the stages that create them. Stored as text; append only.
/// </summary>
public enum RetentionCategory
{
    /// <summary>Leads with their inquiries, consent records, notes, activities and bookings, measured from the last inquiry.</summary>
    LeadsAndInquiries = 0,

    /// <summary>Delivered or dead-lettered outbound emails (pending emails are never touched).</summary>
    EmailMessages = 1,

    /// <summary>Server-side cookie consent receipts.</summary>
    CookieConsentReceipts = 2,

    /// <summary>Admin notifications and their read states.</summary>
    Notifications = 3,

    /// <summary>The audit log.</summary>
    AuditLogs = 4,

    /// <summary>Closed (completed or rejected) data-subject requests.</summary>
    DataSubjectRequests = 5,

    /// <summary>Daily web-analytics and search-performance aggregates (Stage 12), measured by their day. Contains no personal data.</summary>
    AnalyticsAggregates = 6,
}
