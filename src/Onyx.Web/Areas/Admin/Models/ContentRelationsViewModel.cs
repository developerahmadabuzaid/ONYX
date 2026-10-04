using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record ContentRelationsViewModel(
    ContentDetailResponse Content, IReadOnlyList<ContentRelationResponse> Relations, IReadOnlyList<RelatedSuggestionResponse> Suggestions);
