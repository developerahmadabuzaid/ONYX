namespace Onyx.Domain.Notifications;

/// <summary>Stored as text and localized by the Admin UI. Append new values only.</summary>
public enum NotificationType
{
    NewLead = 0,
    NewBooking = 1,
    ArticleReadyForReview = 2,
    JobFailed = 3,
    NewsIngestionFailed = 4,
    IntegrationFailed = 5,
    ToolAttention = 6,
    EmailDeliveryFailed = 7,
    OutboxMessageFailed = 8,
}
