using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>A directed link between two issues of the same project ("AI-3 blocks AI-7"). The reverse wording is derived.</summary>
public sealed class IssueLink : Entity
{
    private IssueLink()
    {
    }

    public Guid ProjectId { get; private set; }

    public Guid SourceIssueId { get; private set; }

    public Guid TargetIssueId { get; private set; }

    public IssueLinkType Type { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<IssueLink> Create(Issue source, Issue target, IssueLinkType type, Guid createdById, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(type))
        {
            return Error.Validation("Delivery.InvalidLinkType", "Choose a link type.");
        }

        if (source.Id == target.Id || source.ProjectId != target.ProjectId)
        {
            return Error.Validation("Delivery.InvalidLink", "Link two different issues of the same project.");
        }

        return new IssueLink
        {
            ProjectId = source.ProjectId,
            SourceIssueId = source.Id,
            TargetIssueId = target.Id,
            Type = type,
            CreatedById = createdById,
            CreatedAtUtc = now,
        };
    }
}
