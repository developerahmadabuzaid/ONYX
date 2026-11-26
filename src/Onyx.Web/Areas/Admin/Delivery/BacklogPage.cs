using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Backlog, open sprints and the history of closed sprints (sprint report data from recorded snapshots).</summary>
public sealed record BacklogPage(BacklogResponse Backlog, IReadOnlyList<SprintResponse> ClosedSprints);
