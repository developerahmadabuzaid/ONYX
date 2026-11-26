using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Choices for issue forms in one project: its members (assignees), epics and open sprints.</summary>
public sealed record IssueOptions(DeliveryProjectResponse Project, IReadOnlyList<IssueListItemResponse> Epics, IReadOnlyList<SprintResponse> Sprints);
