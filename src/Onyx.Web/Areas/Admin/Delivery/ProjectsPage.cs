using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>The projects list; <see cref="CanCreate"/> only hides the form (the API decides).</summary>
public sealed record ProjectsPage(IReadOnlyList<DeliveryProjectListItemResponse> Projects, bool CanCreate);
