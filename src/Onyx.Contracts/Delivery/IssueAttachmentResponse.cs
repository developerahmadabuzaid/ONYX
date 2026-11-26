namespace Onyx.Contracts.Delivery;

/// <summary>A file attached to an issue.</summary>
public sealed record IssueAttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    bool IsImage,
    string UploadedByName,
    DateTimeOffset UploadedAtUtc,
    bool CanDelete);
