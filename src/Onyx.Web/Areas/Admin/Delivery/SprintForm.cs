using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Sprint create/plan form fields.</summary>
public sealed class SprintForm
{
    public string? Name { get; set; }

    public string? Goal { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public SaveSprintRequest ToRequest(uint? version) => new(Name, Goal, StartDate, EndDate, version);
}
