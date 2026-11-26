using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>
/// One column of a project's workflow. Default statuses carry a stable <see cref="SystemKey"/> so the UI can show them in the
/// visitor's language; statuses added later show their own <see cref="Name"/>.
/// </summary>
public sealed class WorkflowStatus : Entity
{
    public const int NameMaxLength = 40;
    public const int SystemKeyMaxLength = 20;

    private WorkflowStatus()
    {
        Name = string.Empty;
    }

    internal WorkflowStatus(Guid projectId, string name, string? systemKey, StatusCategory category, int order)
    {
        ProjectId = projectId;
        Name = name;
        SystemKey = systemKey;
        Category = category;
        Order = order;
    }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; }

    public string? SystemKey { get; private set; }

    public StatusCategory Category { get; private set; }

    public int Order { get; private set; }

    /// <summary>Optional work-in-progress limit shown on the board (advisory: the board warns, the server does not block).</summary>
    public int? WipLimit { get; private set; }

    internal Result SetWipLimit(int? limit)
    {
        if (limit is < 1 or > 500)
        {
            return Error.Validation("Delivery.InvalidWipLimit", "The work-in-progress limit is between 1 and 500, or empty.");
        }

        WipLimit = limit;
        return Result.Success();
    }
}
