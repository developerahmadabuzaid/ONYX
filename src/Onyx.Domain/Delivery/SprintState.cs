namespace Onyx.Domain.Delivery;

/// <summary>Sprint lifecycle: Planned → Active → Completed, or Cancelled before completion. Stored as text.</summary>
public enum SprintState
{
    Planned = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3,
}
