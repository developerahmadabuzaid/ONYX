namespace Onyx.Contracts.Delivery;

/// <summary>
/// One change applied to several issues. <see cref="Action"/> is Assign (to <see cref="AssigneeId"/>, or unassign when it is
/// null), Priority, AddLabel or Delete. Each issue is checked and saved on its own, so one refusal does not undo the others.
/// </summary>
public sealed record BulkIssueRequest(
    IReadOnlyList<Guid>? IssueIds,
    string? Action,
    Guid? AssigneeId,
    string? Priority,
    string? Label);
