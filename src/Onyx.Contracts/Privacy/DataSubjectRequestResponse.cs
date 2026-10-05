namespace Onyx.Contracts.Privacy;

/// <summary>
/// A data-subject request. <see cref="SubjectEmail"/> is null once the subject has been erased; <see cref="SubjectReference"/>
/// (a prefix of the address hash) still links requests for the same subject.
/// </summary>
public sealed record DataSubjectRequestResponse(
    Guid Id,
    string Type,
    string Status,
    string? SubjectEmail,
    string SubjectReference,
    string? RequestNote,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? VerifiedAtUtc,
    string? VerificationNote,
    DateTimeOffset? CompletedAtUtc,
    string? ResultSummary,
    string? RejectionReason,
    uint Version);
