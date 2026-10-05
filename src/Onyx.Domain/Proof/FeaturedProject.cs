using Onyx.Domain.Common;

namespace Onyx.Domain.Proof;

/// <summary>
/// One of the homepage's featured-work slots. Slots are unique (1..<see cref="MaxFeatured"/>) and a project holds at most one,
/// so the database itself enforces the limit even under concurrent requests.
/// </summary>
public sealed class FeaturedProject : Entity
{
    public const int MaxFeatured = 3;

    private FeaturedProject()
    {
    }

    public Guid ContentItemId { get; private set; }

    public int Slot { get; private set; }

    public Guid FeaturedById { get; private set; }

    public DateTimeOffset FeaturedAtUtc { get; private set; }

    public static Result<FeaturedProject> Create(Guid contentItemId, int slot, Guid featuredById, DateTimeOffset now) =>
        slot is < 1 or > MaxFeatured
            ? Error.Validation("Featured.InvalidSlot", $"Featured slots are 1 to {MaxFeatured}.")
            : new FeaturedProject { ContentItemId = contentItemId, Slot = slot, FeaturedById = featuredById, FeaturedAtUtc = now };
}
