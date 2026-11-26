namespace Onyx.Domain.Delivery;

/// <summary>What a workflow status means for progress, independent of its name. Stored as text.</summary>
public enum StatusCategory
{
    ToDo = 0,
    InProgress = 1,
    Done = 2,
}
