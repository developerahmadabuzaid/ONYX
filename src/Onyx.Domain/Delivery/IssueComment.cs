using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>A comment on an issue (plain text, rendered encoded). Replies reference a top-level comment; deletion keeps a placeholder.</summary>
public sealed class IssueComment : AuditableEntity, IAuditable
{
    public const int BodyMaxLength = 10000;

    private IssueComment()
    {
        Body = string.Empty;
    }

    public Guid IssueId { get; private set; }

    public Guid AuthorId { get; private set; }

    public Guid? ParentCommentId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset? EditedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Delivery;

    public static Result<IssueComment> Create(Guid issueId, Guid authorId, string? body, IssueComment? parent)
    {
        var text = DeliveryText.Required(body, BodyMaxLength, "Delivery.InvalidComment", $"Write a comment (at most {BodyMaxLength} characters).");
        if (text.IsFailure)
        {
            return text.Error!;
        }

        if (parent is not null && (parent.IssueId != issueId || parent.ParentCommentId is not null))
        {
            return Error.Validation("Delivery.InvalidReply", "Replies answer a top-level comment of the same issue.");
        }

        return new IssueComment { IssueId = issueId, AuthorId = authorId, Body = text.Value, ParentCommentId = parent?.Id };
    }

    public Result Edit(string? body, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Error.Conflict("Delivery.CommentDeleted", "This comment was deleted.");
        }

        var text = DeliveryText.Required(body, BodyMaxLength, "Delivery.InvalidComment", $"Write a comment (at most {BodyMaxLength} characters).");
        if (text.IsFailure)
        {
            return text.Error!;
        }

        Body = text.Value;
        EditedAtUtc = now;
        return Result.Success();
    }

    /// <summary>Removes the text but keeps the row so replies stay in context.</summary>
    public void Delete()
    {
        IsDeleted = true;
        Body = string.Empty;
    }
}
