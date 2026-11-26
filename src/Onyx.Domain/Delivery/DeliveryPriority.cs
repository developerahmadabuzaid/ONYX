namespace Onyx.Domain.Delivery;

/// <summary>Priority shared by projects and issues; higher values are more urgent. Stored as text.</summary>
public enum DeliveryPriority
{
    Lowest = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Highest = 4,
}
