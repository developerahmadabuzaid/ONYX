using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record ContentRevisionsViewModel(ContentDetailResponse Content, IReadOnlyList<ContentRevisionSummaryResponse> Revisions, RevisionComparisonResponse? Comparison);
