using Onyx.Domain.Common;

namespace Onyx.Domain.Knowledge;

/// <summary>
/// "Needs information": a fact required to write something truthfully is missing from approved knowledge. Raised by editors
/// (and later by AI drafting) instead of guessing; resolved by supplying approved knowledge or dismissing the claim.
/// </summary>
public sealed class InformationRequest : AuditableEntity, IAuditable
{
    public const int QuestionMaxLength = 1_000;
    public const int ContextMaxLength = 1_000;
    public const int ResolutionMaxLength = 1_000;

    private InformationRequest()
    {
        Question = string.Empty;
    }

    public string Question { get; private set; }

    /// <summary>What needs the information (e.g. a content item or proposal), free text.</summary>
    public string? Context { get; private set; }

    public Guid? ContentItemId { get; private set; }

    public InformationRequestStatus Status { get; private set; }

    public Guid? KnowledgeEntryId { get; private set; }

    public string? ResolutionNote { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public static Result<InformationRequest> Raise(string question, string? context, Guid? contentItemId)
    {
        var trimmed = question?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > QuestionMaxLength || context is { Length: > ContextMaxLength })
        {
            return Error.Validation("InformationRequest.Invalid", $"A question of 1-{QuestionMaxLength} characters is required.");
        }

        return new InformationRequest
        {
            Question = trimmed,
            Context = string.IsNullOrWhiteSpace(context) ? null : context.Trim(),
            ContentItemId = contentItemId,
            Status = InformationRequestStatus.Open,
        };
    }

    public Result Resolve(InformationRequestStatus outcome, Guid? knowledgeEntryId, string? note, DateTimeOffset now)
    {
        if (Status != InformationRequestStatus.Open)
        {
            return Error.Conflict("InformationRequest.Closed", "This request is already closed.");
        }

        if (outcome is not (InformationRequestStatus.Answered or InformationRequestStatus.Dismissed)
            || (outcome == InformationRequestStatus.Answered && knowledgeEntryId is null) || note is { Length: > ResolutionMaxLength })
        {
            return Error.Validation("InformationRequest.InvalidResolution", "An answer links the approved knowledge entry that supplies the fact.");
        }

        Status = outcome;
        KnowledgeEntryId = knowledgeEntryId;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ResolvedAtUtc = now;
        return Result.Success();
    }
}
