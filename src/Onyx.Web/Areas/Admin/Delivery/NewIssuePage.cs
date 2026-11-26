using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>The create-issue page; <see cref="Options"/> is null until a project is chosen.</summary>
public sealed record NewIssuePage(IReadOnlyList<DeliveryProjectListItemResponse> Projects, IssueOptions? Options, IssueForm Form);
