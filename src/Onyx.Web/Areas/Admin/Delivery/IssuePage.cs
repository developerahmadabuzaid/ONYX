using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>The issue details page.</summary>
public sealed record IssuePage(IssueDetailResponse Issue, IssueOptions? Options);
