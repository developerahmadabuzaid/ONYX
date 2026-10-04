using Onyx.Contracts.Brand;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;
using Onyx.Contracts.Knowledge;
using Onyx.Contracts.Media;
using Onyx.Contracts.Messaging;

namespace Onyx.Web.ApiClients;

/// <summary>Admin CMS calls, made on behalf of the signed-in admin (access token attached server-side). The API authorizes every call.</summary>
public interface IAdminCmsApiClient
{
    Task<ApiResult<PagedResult<ContentSummaryResponse>>> ListContentAsync(string? type, string? status, bool needsUrlDecision, string? search, int page, CancellationToken cancellationToken);

    Task<ApiResult<ContentDetailResponse>> CreateContentAsync(CreateContentRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ContentDetailResponse>> GetContentAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<ContentDetailResponse>> UpdateTranslationAsync(Guid id, string languageCode, UpdateContentTranslationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ContentDetailResponse>> UpdateSeoAsync(Guid id, string languageCode, UpdateContentSeoRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ContentDetailResponse>> UpdateSettingsAsync(Guid id, UpdateContentSettingsRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteContentAsync(Guid id, DeleteContentRequest request, CancellationToken cancellationToken);

    /// <summary>Lifecycle actions: submit, approve, reject, publish, schedule, schedule/cancel, unpublish-schedule, unpublish-schedule/cancel, unpublish, archive, restore, url-resolution.</summary>
    Task<ApiResult<ContentDetailResponse>> TransitionAsync(Guid id, string action, object request, CancellationToken cancellationToken);

    Task<ApiResult<QualityReportResponse>> GetQualityAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<PublishingTimeRecommendationResponse>> GetPublishingTimeAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ContentRevisionSummaryResponse>>> ListRevisionsAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<RevisionComparisonResponse>> CompareRevisionsAsync(Guid id, int from, int to, CancellationToken cancellationToken);

    Task<ApiResult<ContentDetailResponse>> RestoreRevisionAsync(Guid id, int number, RestoreRevisionRequest request, CancellationToken cancellationToken);

    Task<ApiResult<PreviewLinkResponse>> CreatePreviewLinkAsync(Guid id, string languageCode, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ContentRelationResponse>>> ListRelationsAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<ContentRelationResponse>> AddRelationAsync(Guid id, AddContentRelationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> RemoveRelationAsync(Guid id, Guid relationId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<RelatedSuggestionResponse>>> SuggestRelatedAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<ContentIntelligenceResponse>> GetIntelligenceAsync(Guid id, string languageCode, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<MediaAssetResponse>>> ListMediaAsync(string? search, int page, CancellationToken cancellationToken);

    Task<ApiResult<MediaAssetResponse>> UploadMediaAsync(Stream content, string fileName, string? contentType, CancellationToken cancellationToken);

    Task<ApiResult<MediaAssetResponse>> GetMediaAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<MediaUsageResponse>>> ListMediaUsagesAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<MediaAssetResponse>> UpdateMediaTextsAsync(Guid id, UpdateMediaTextsRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteMediaAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiFileStream?> OpenMediaFileAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<RedirectResponse>>> ListRedirectsAsync(string? search, int page, CancellationToken cancellationToken);

    Task<ApiResult<RedirectResponse>> CreateRedirectAsync(SaveRedirectRequest request, CancellationToken cancellationToken);

    Task<ApiResult<RedirectResponse>> UpdateRedirectAsync(Guid id, SaveRedirectRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteRedirectAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<CtaResponse>>> ListCtasAsync(CancellationToken cancellationToken);

    Task<ApiResult<CtaResponse>> CreateCtaAsync(SaveCtaRequest request, CancellationToken cancellationToken);

    Task<ApiResult<CtaResponse>> UpdateCtaAsync(Guid id, SaveCtaRequest request, CancellationToken cancellationToken);

    Task<ApiResult<CtaDefaultsResponse>> GetCtaDefaultsAsync(CancellationToken cancellationToken);

    Task<ApiResult<CtaDefaultsResponse>> SetCtaDefaultAsync(SetCtaDefaultRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ClusterResponse>>> ListClustersAsync(CancellationToken cancellationToken);

    Task<ApiResult<ClusterResponse>> CreateClusterAsync(SaveClusterRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ClusterResponse>> SetClusterMemberAsync(Guid id, SetClusterMemberRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ClusterResponse>> RemoveClusterMemberAsync(Guid id, Guid contentItemId, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteClusterAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<BrandKitResponse>> GetBrandKitAsync(CancellationToken cancellationToken);

    Task<ApiResult<BrandKitResponse>> UpdateBrandKitAsync(UpdateBrandKitRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<KnowledgeEntryResponse>>> ListKnowledgeAsync(string? status, CancellationToken cancellationToken);

    Task<ApiResult<KnowledgeEntryResponse>> CreateKnowledgeAsync(SaveKnowledgeEntryRequest request, CancellationToken cancellationToken);

    Task<ApiResult<KnowledgeEntryResponse>> UpdateKnowledgeAsync(Guid id, SaveKnowledgeEntryRequest request, CancellationToken cancellationToken);

    Task<ApiResult<KnowledgeEntryResponse>> KnowledgeActionAsync(Guid id, string action, uint version, CancellationToken cancellationToken);

    Task<ApiResult<GovernanceRulesResponse>> GetGovernanceRulesAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<InformationRequestResponse>>> ListInformationRequestsAsync(CancellationToken cancellationToken);

    Task<ApiResult<InformationRequestResponse>> RaiseInformationRequestAsync(RaiseInformationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<InformationRequestResponse>> ResolveInformationRequestAsync(Guid id, ResolveInformationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<KeywordTargetResponse>>> ListKeywordsAsync(string? search, int page, CancellationToken cancellationToken);

    Task<ApiResult<KeywordTargetResponse>> CreateKeywordAsync(SaveKeywordTargetRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteKeywordAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<KeywordConflictResponse>> CheckKeywordAsync(string languageCode, string keyword, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<CompetitorResponse>>> ListCompetitorsAsync(CancellationToken cancellationToken);

    Task<ApiResult<CompetitorResponse>> CreateCompetitorAsync(SaveCompetitorRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> DeleteCompetitorAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<RecommendationResponse>>> ListRecommendationsAsync(string? status, int page, CancellationToken cancellationToken);

    Task<ApiResult<RecommendationResponse>> ProposeRecommendationAsync(ProposeRecommendationRequest request, CancellationToken cancellationToken);

    /// <summary>approve, reject, apply or mark-applied.</summary>
    Task<ApiResult<RecommendationResponse>> RecommendationActionAsync(Guid id, string action, DecideRequest request, CancellationToken cancellationToken);

    Task<ApiResult<PagedResult<CalendarEntryResponse>>> ListCalendarAsync(string? status, int page, CancellationToken cancellationToken);

    Task<ApiResult<CalendarEntryResponse>> ProposeCalendarEntryAsync(ProposeCalendarEntryRequest request, CancellationToken cancellationToken);

    /// <summary>accept, reject or complete.</summary>
    Task<ApiResult<CalendarEntryResponse>> CalendarActionAsync(Guid id, string action, DecideRequest request, CancellationToken cancellationToken);

    Task<ApiResult<CalendarEntryResponse>> LinkCalendarContentAsync(Guid id, LinkCalendarContentRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IntelligenceCapabilitiesResponse>> GetCapabilitiesAsync(CancellationToken cancellationToken);

    Task<ApiResult<InternalLinkReportResponse>> GetLinkReportAsync(CancellationToken cancellationToken);

    Task<ApiResult<GeneratedRecommendationsResponse>> GenerateLinkRecommendationsAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<EmailTemplateResponse>>> ListEmailTemplatesAsync(CancellationToken cancellationToken);

    Task<ApiResult<EmailTemplateResponse>> SaveEmailTemplateAsync(string key, string languageCode, SaveEmailTemplateCopyRequest request, CancellationToken cancellationToken);

    Task<ApiResult<EmailTemplateResponse>> ResetEmailTemplateAsync(string key, string languageCode, CancellationToken cancellationToken);
}
