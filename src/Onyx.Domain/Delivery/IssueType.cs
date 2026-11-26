namespace Onyx.Domain.Delivery;

/// <summary>Issue types (Epic groups work; Subtask always has a parent). Stored as text.</summary>
public enum IssueType
{
    Epic = 0,
    Story = 1,
    Task = 2,
    Bug = 3,
    Subtask = 4,
    Feature = 5,
    Improvement = 6,
    TechnicalDebt = 7,
}
