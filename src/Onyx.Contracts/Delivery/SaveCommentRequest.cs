namespace Onyx.Contracts.Delivery;

/// <summary>Adds a comment (optionally replying to a top-level comment) or edits one.</summary>
public sealed record SaveCommentRequest(
    string? Body,
    Guid? ParentCommentId);
