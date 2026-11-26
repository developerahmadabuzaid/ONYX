using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>Append-only activity of an issue: creation, field changes, status transitions, comments, links.</summary>
public sealed class IssueHistoryEntry : Entity
{
    public const int FieldMaxLength = 40;
    public const int ValueMaxLength = 500;

    private IssueHistoryEntry()
    {
        Field = string.Empty;
    }

    public IssueHistoryEntry(Guid projectId, Guid issueId, Guid? actorId, string field, string? oldValue, string? newValue, DateTimeOffset atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ProjectId = projectId;
        IssueId = issueId;
        ActorId = actorId;
        Field = field.Length <= FieldMaxLength ? field : field[..FieldMaxLength];
        OldValue = Truncate(oldValue);
        NewValue = Truncate(newValue);
        AtUtc = atUtc;
    }

    public Guid ProjectId { get; private set; }

    public Guid IssueId { get; private set; }

    public Guid? ActorId { get; private set; }

    public string Field { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTimeOffset AtUtc { get; private set; }

    private static string? Truncate(string? value) =>
        value is null || value.Length <= ValueMaxLength ? value : string.Concat(value.AsSpan(0, ValueMaxLength - 1), "…");
}
