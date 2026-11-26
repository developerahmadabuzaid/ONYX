namespace Onyx.Domain.Delivery;

/// <summary>Lifecycle of a delivery project. Stored as text.</summary>
public enum DeliveryProjectStatus
{
    Planned = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Archived = 4,
}
