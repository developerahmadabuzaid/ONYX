namespace Onyx.Domain.Subscriptions;

/// <summary>Stored as text.</summary>
public enum SubscriptionStatus
{
    /// <summary>The customer asked to subscribe; staff activate or reject it.</summary>
    Requested = 0,
    Trial = 1,
    Active = 2,
    Rejected = 3,
    Cancelled = 4,

    /// <summary>A trial that reached its end date without being activated (set by the Worker).</summary>
    Expired = 5,
}
