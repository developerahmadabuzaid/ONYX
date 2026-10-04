using Onyx.Domain.Common;

namespace Onyx.Domain.Intelligence;

/// <summary>
/// A proposed SEO/content change with its evidence and reasoning. Recommendations never modify content by themselves: an
/// editor approves or rejects, and applying an approved change only edits the working copy, which is reviewed again before
/// publishing. Also the record of an SEO change for later observation (<see cref="SeoChangeObservation"/>).
/// </summary>
public sealed class SeoRecommendation : AuditableEntity, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int IssueMaxLength = 2_000;
    public const int ReasoningMaxLength = 4_000;
    public const int DecisionNoteMaxLength = 1_000;
    public const int MaxEvidence = 20;
    public const int MaxChanges = 20;

    private SeoRecommendation()
    {
        Title = string.Empty;
        DetectedIssue = string.Empty;
        ProposedChanges = [];
        Evidence = [];
    }

    public Guid? ContentItemId { get; private set; }

    public string? LanguageCode { get; private set; }

    public Guid? ClusterId { get; private set; }

    public RecommendationKind Kind { get; private set; }

    public RecommendationOrigin Origin { get; private set; }

    public RecommendationStatus Status { get; private set; }

    public RecommendationBasis Basis { get; private set; }

    public string Title { get; private set; }

    public string DetectedIssue { get; private set; }

    public IReadOnlyList<ProposedFieldChange> ProposedChanges { get; private set; }

    public string? Reasoning { get; private set; }

    public IReadOnlyList<EvidenceItem> Evidence { get; private set; }

    public Guid? DecidedById { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public DateTimeOffset? AppliedAtUtc { get; private set; }

    public int? AppliedRevisionNumber { get; private set; }

    public uint Version { get; private set; }

    public static Result<SeoRecommendation> Propose(
        RecommendationKind kind,
        RecommendationOrigin origin,
        RecommendationBasis basis,
        string title,
        string detectedIssue,
        string? reasoning,
        IReadOnlyList<ProposedFieldChange> changes,
        IReadOnlyList<EvidenceItem> evidence,
        Guid? contentItemId,
        string? languageCode,
        Guid? clusterId)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(evidence);
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var trimmedIssue = detectedIssue?.Trim() ?? string.Empty;

        if (!Enum.IsDefined(kind) || !Enum.IsDefined(origin) || !Enum.IsDefined(basis)
            || trimmedTitle.Length is 0 or > TitleMaxLength || trimmedIssue.Length is 0 or > IssueMaxLength || reasoning is { Length: > ReasoningMaxLength })
        {
            return Invalid("A kind, a title and the detected issue are required.");
        }

        if (evidence.Count > MaxEvidence)
        {
            return Invalid("Too many evidence items.");
        }

        if (evidence.Select(item => item.Problem()).FirstOrDefault(problem => problem is not null) is { } evidenceProblem)
        {
            return Invalid(evidenceProblem);
        }

        // "Evidence" must be backed by recorded evidence; anything else must say it is an estimate or lacks data.
        if (basis == RecommendationBasis.Evidence && evidence.Count == 0)
        {
            return Invalid("A recommendation based on evidence must include that evidence.");
        }

        if (changes.Count > MaxChanges || changes.Any(change => string.IsNullOrWhiteSpace(change.Field) || change.Field.Length > 64
            || change.ProposedValue is { Length: > 4_000 } || change.CurrentValue is { Length: > 4_000 }))
        {
            return Invalid("Invalid proposed changes.");
        }

        return new SeoRecommendation
        {
            Kind = kind,
            Origin = origin,
            Basis = basis,
            Status = RecommendationStatus.Proposed,
            Title = trimmedTitle,
            DetectedIssue = trimmedIssue,
            Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning.Trim(),
            ProposedChanges = [.. changes],
            Evidence = [.. evidence],
            ContentItemId = contentItemId,
            LanguageCode = languageCode,
            ClusterId = clusterId,
        };
    }

    public Result Approve(Guid userId, string? note, DateTimeOffset now) => Decide(RecommendationStatus.Approved, userId, note, now);

    public Result Reject(Guid userId, string? note, DateTimeOffset now) => Decide(RecommendationStatus.Rejected, userId, note, now);

    public Result MarkApplied(int? revisionNumber, DateTimeOffset now)
    {
        if (Status != RecommendationStatus.Approved)
        {
            return Error.Conflict("Recommendation.NotApproved", "Only an approved recommendation can be applied.");
        }

        Status = RecommendationStatus.Applied;
        AppliedAtUtc = now;
        AppliedRevisionNumber = revisionNumber;
        return Result.Success();
    }

    private Result Decide(RecommendationStatus outcome, Guid userId, string? note, DateTimeOffset now)
    {
        if (Status != RecommendationStatus.Proposed)
        {
            return Error.Conflict("Recommendation.AlreadyDecided", "This recommendation has already been decided.");
        }

        if (note is { Length: > DecisionNoteMaxLength })
        {
            return Invalid($"Decision notes are limited to {DecisionNoteMaxLength} characters.");
        }

        Status = outcome;
        DecidedById = userId;
        DecidedAtUtc = now;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return Result.Success();
    }

    private static Error Invalid(string message) => Error.Validation("Recommendation.Invalid", message);
}
