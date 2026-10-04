using Onyx.Contracts.Common;
using Onyx.Contracts.Intelligence;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record KeywordListViewModel(PagedResult<KeywordTargetResponse> Items, string? Search, string? CheckedKeyword, KeywordConflictResponse? Conflicts);
