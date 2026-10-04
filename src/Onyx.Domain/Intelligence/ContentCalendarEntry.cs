using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Intelligence;

/// <summary>
/// A planned piece of content: proposal (editor or future AI) → admin review → preparation (linked content item) → normal
/// review, approval and scheduling. A proposed publication time must state its basis; AI never schedules or publishes.
/// </summary>
public sealed class ContentCalendarEntry : AuditableEntity, IAuditable
{
    public const int TopicMaxLength = 300;
    public const int ReasonMaxLength = 2_000;
    public const int NoteMaxLength = 1_000;

    private ContentCalendarEntry()
    {
        Topic = string.Empty;
        LanguageCode = string.Empty;
        SupportingContentIds = [];
        Evidence = [];
    }

    public string Topic { get; private set; }

    public string LanguageCode { get; private set; }

    public string? TargetMarket { get; private set; }

    public Guid? TargetServiceId { get; private set; }

    public Guid? ClusterId { get; private set; }

    public string? Keyword { get; private set; }

    public SearchIntent SearchIntent { get; private set; }

    public string? Reason { get; private set; }

    public IReadOnlyList<Guid> SupportingContentIds { get; private set; }

    public DateTimeOffset? ProposedPublishAtUtc { get; private set; }

    public RecommendationBasis TimingBasis { get; private set; }

    public IReadOnlyList<EvidenceItem> Evidence { get; private set; }

    public RecommendationOrigin Origin { get; private set; }

    public CalendarEntryStatus Status { get; private set; }

    public Guid? LinkedContentItemId { get; private set; }

    public Guid? DecidedById { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public uint Version { get; private set; }

    public static Result<ContentCalendarEntry> Propose(
        string topic,
        string languageCode,
        string? targetMarket,
        Guid? targetServiceId,
        Guid? clusterId,
        string? keyword,
        SearchIntent intent,
        string? reason,
        IReadOnlyList<Guid> supportingContentIds,
        DateTimeOffset? proposedPublishAtUtc,
        RecommendationBasis timingBasis,
        IReadOnlyList<EvidenceItem> evidence,
        RecommendationOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(supportingContentIds);
        ArgumentNullException.ThrowIfNull(evidence);
        var trimmedTopic = topic?.Trim() ?? string.Empty;
        if (trimmedTopic.Length is 0 or > TopicMaxLength || reason is { Length: > ReasonMaxLength } || keyword is { Length: > KeywordTarget.KeywordMaxLength }
            || !Enum.IsDefined(intent) || !Enum.IsDefined(timingBasis) || !Enum.IsDefined(origin) || supportingContentIds.Count > 50)
        {
            return Invalid("A topic (max 300 characters) and valid planning fields are required.");
        }

        if (evidence.Select(item => item.Problem()).FirstOrDefault(problem => problem is not null) is { } problem)
        {
            return Invalid(problem);
        }

        // A date needs an honest basis: an editor's choice, recorded evidence, or a labelled estimate.
        if (proposedPublishAtUtc is not null && timingBasis == RecommendationBasis.InsufficientData)
        {
            return Invalid("Without data there is no recommended time; leave the date empty or record it as a manual choice.");
        }

        if (timingBasis == RecommendationBasis.Evidence && evidence.Count == 0)
        {
            return Invalid("A time based on evidence must include that evidence.");
        }

        return new ContentCalendarEntry
        {
            Topic = trimmedTopic,
            LanguageCode = languageCode,
            TargetMarket = string.IsNullOrWhiteSpace(targetMarket) ? null : targetMarket.Trim(),
            TargetServiceId = targetServiceId,
            ClusterId = clusterId,
            Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim(),
            SearchIntent = intent,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            SupportingContentIds = supportingContentIds.Distinct().ToList(),
            ProposedPublishAtUtc = proposedPublishAtUtc?.ToUniversalTime(),
            TimingBasis = timingBasis,
            Evidence = [.. evidence],
            Origin = origin,
            Status = CalendarEntryStatus.Proposed,
        };
    }

    public Result Accept(Guid userId, string? note, DateTimeOffset now) => Decide(CalendarEntryStatus.Accepted, userId, note, now);

    public Result Reject(Guid userId, string? note, DateTimeOffset now) => Decide(CalendarEntryStatus.Rejected, userId, note, now);

    /// <summary>Content preparation started: the entry now points at the draft that will go through the normal review workflow.</summary>
    public Result LinkContent(Guid contentItemId)
    {
        if (Status is not (CalendarEntryStatus.Accepted or CalendarEntryStatus.InPreparation))
        {
            return Error.Conflict("Calendar.NotAccepted", "Accept the proposal before preparing content for it.");
        }

        LinkedContentItemId = contentItemId;
        Status = CalendarEntryStatus.InPreparation;
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != CalendarEntryStatus.InPreparation)
        {
            return Error.Conflict("Calendar.NotInPreparation", "Only entries in preparation can be completed.");
        }

        Status = CalendarEntryStatus.Completed;
        return Result.Success();
    }

    private Result Decide(CalendarEntryStatus outcome, Guid userId, string? note, DateTimeOffset now)
    {
        if (Status != CalendarEntryStatus.Proposed)
        {
            return Error.Conflict("Calendar.AlreadyDecided", "This proposal has already been decided.");
        }

        if (note is { Length: > NoteMaxLength })
        {
            return Invalid($"Decision notes are limited to {NoteMaxLength} characters.");
        }

        Status = outcome;
        DecidedById = userId;
        DecidedAtUtc = now;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return Result.Success();
    }

    private static Error Invalid(string message) => Error.Validation("Calendar.Invalid", message);
}
