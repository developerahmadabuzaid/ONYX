using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>A timeline item in display order, with its indentation (issues under their epic).</summary>
public sealed record TimelineItemView(TimelineItemResponse Item, int Depth);
