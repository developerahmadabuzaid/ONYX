namespace Onyx.Contracts.Delivery;

/// <summary>A comment; Body is empty when deleted.</summary>
public sealed record IssueCommentResponse(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    Guid? ParentCommentId,
    string Body,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? EditedAtUtc,
    bool IsDeleted,
    bool CanEdit);
