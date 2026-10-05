using Onyx.Domain.Common;

namespace Onyx.Domain.Leads;

/// <summary>One entry in a lead's timeline. Append-only; <see cref="Detail"/> holds codes and ids, never contact details.</summary>
public sealed class LeadActivity : Entity
{
    public const int DetailMaxLength = 200;

    private LeadActivity()
    {
    }

    public Guid LeadId { get; private set; }

    public LeadActivityKind Kind { get; private set; }

    public string? Detail { get; private set; }

    public Guid? RelatedId { get; private set; }

    public Guid? ActorId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    internal static LeadActivity Record(Guid leadId, LeadActivityKind kind, string? detail, Guid? relatedId, Guid? actorId, DateTimeOffset now) =>
        new()
        {
            LeadId = leadId,
            Kind = kind,
            Detail = detail is { Length: > DetailMaxLength } ? detail[..DetailMaxLength] : detail,
            RelatedId = relatedId,
            ActorId = actorId,
            OccurredAtUtc = now,
        };
}
