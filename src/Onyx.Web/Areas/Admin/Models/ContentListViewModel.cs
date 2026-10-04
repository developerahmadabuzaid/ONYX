using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record ContentListViewModel(PagedResult<ContentSummaryResponse> Items, string? Type, string? Status, bool NeedsUrlDecision, string? Search);
