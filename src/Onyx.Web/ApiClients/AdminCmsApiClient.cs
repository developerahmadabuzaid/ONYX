using System.Globalization;
using Onyx.Contracts.Brand;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Insights;
using Onyx.Contracts.Intelligence;
using Onyx.Contracts.Knowledge;
using Onyx.Contracts.Media;
using Onyx.Contracts.Messaging;
using Onyx.Contracts.Proof;

namespace Onyx.Web.ApiClients;

public sealed class AdminCmsApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminCmsApiClient
{
    private const string Content = "api/v1/admin/content";
    private const string ProofItems = "api/v1/admin/proof-items";
    private const string Authors = "api/v1/admin/authors";

    private static readonly HashSet<string> Transitions = new(StringComparer.Ordinal)
    {
        "submit", "approve", "reject", "publish", "schedule", "schedule/cancel", "unpublish-schedule", "unpublish-schedule/cancel",
        "unpublish", "archive", "restore", "url-resolution",
    };

    public Task<ApiResult<PagedResult<ContentSummaryResponse>>> ListContentAsync(
        string? type, string? status, bool needsUrlDecision, string? search, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<ContentSummaryResponse>>(
            $"{Content}?page={Number(page)}&pageSize=25&needsUrlDecision={(needsUrlDecision ? "true" : "false")}{Query("type", type)}{Query("status", status)}{Query("search", search)}",
            cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> CreateContentAsync(CreateContentRequest request, CancellationToken cancellationToken) =>
        PostAsync<ContentDetailResponse>(Content, request, cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> GetContentAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<ContentDetailResponse>($"{Content}/{id:D}", cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> UpdateTranslationAsync(Guid id, string languageCode, UpdateContentTranslationRequest request, CancellationToken cancellationToken) =>
        PutAsync<ContentDetailResponse>($"{Content}/{id:D}/translations/{Uri.EscapeDataString(languageCode)}", request, cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> UpdateSeoAsync(Guid id, string languageCode, UpdateContentSeoRequest request, CancellationToken cancellationToken) =>
        PutAsync<ContentDetailResponse>($"{Content}/{id:D}/translations/{Uri.EscapeDataString(languageCode)}/seo", request, cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> UpdateSettingsAsync(Guid id, UpdateContentSettingsRequest request, CancellationToken cancellationToken) =>
        PutAsync<ContentDetailResponse>($"{Content}/{id:D}/settings", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteContentAsync(Guid id, DeleteContentRequest request, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"{Content}/{id:D}/delete", request, cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> TransitionAsync(Guid id, string action, object request, CancellationToken cancellationToken) =>
        Transitions.Contains(action)
            ? PostAsync<ContentDetailResponse>($"{Content}/{id:D}/{action}", request, cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown lifecycle action.");

    public Task<ApiResult<QualityReportResponse>> GetQualityAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<QualityReportResponse>($"{Content}/{id:D}/quality", cancellationToken);

    public Task<ApiResult<PublishingTimeRecommendationResponse>> GetPublishingTimeAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<PublishingTimeRecommendationResponse>($"{Content}/{id:D}/publishing-time", cancellationToken);

    public Task<ApiResult<IReadOnlyList<ContentRevisionSummaryResponse>>> ListRevisionsAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ContentRevisionSummaryResponse>>($"{Content}/{id:D}/revisions", cancellationToken);

    public Task<ApiResult<RevisionComparisonResponse>> CompareRevisionsAsync(Guid id, int from, int to, CancellationToken cancellationToken) =>
        GetAsync<RevisionComparisonResponse>($"{Content}/{id:D}/revisions/compare?from={Number(from)}&to={Number(to)}", cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> RestoreRevisionAsync(Guid id, int number, RestoreRevisionRequest request, CancellationToken cancellationToken) =>
        PostAsync<ContentDetailResponse>($"{Content}/{id:D}/revisions/{Number(number)}/restore", request, cancellationToken);

    public Task<ApiResult<PreviewLinkResponse>> CreatePreviewLinkAsync(Guid id, string languageCode, CancellationToken cancellationToken) =>
        PostAsync<PreviewLinkResponse>($"{Content}/{id:D}/preview-links", new CreatePreviewLinkRequest(languageCode), cancellationToken);

    public Task<ApiResult<IReadOnlyList<ContentRelationResponse>>> ListRelationsAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ContentRelationResponse>>($"{Content}/{id:D}/relations", cancellationToken);

    public Task<ApiResult<ContentRelationResponse>> AddRelationAsync(Guid id, AddContentRelationRequest request, CancellationToken cancellationToken) =>
        PostAsync<ContentRelationResponse>($"{Content}/{id:D}/relations", request, cancellationToken);

    public Task<ApiResult<NoContent>> RemoveRelationAsync(Guid id, Guid relationId, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Content}/{id:D}/relations/{relationId:D}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<RelatedSuggestionResponse>>> SuggestRelatedAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<RelatedSuggestionResponse>>($"{Content}/{id:D}/related-suggestions", cancellationToken);

    public Task<ApiResult<ContentIntelligenceResponse>> GetIntelligenceAsync(Guid id, string languageCode, CancellationToken cancellationToken) =>
        GetAsync<ContentIntelligenceResponse>($"{Content}/{id:D}/intelligence/{Uri.EscapeDataString(languageCode)}", cancellationToken);

    public Task<ApiResult<PagedResult<MediaAssetResponse>>> ListMediaAsync(string? search, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<MediaAssetResponse>>($"api/v1/admin/media?page={Number(page)}&pageSize=24{Query("search", search)}", cancellationToken);

    public Task<ApiResult<MediaAssetResponse>> UploadMediaAsync(Stream content, string fileName, string? contentType, CancellationToken cancellationToken) =>
        PostFileAsync<MediaAssetResponse>("api/v1/admin/media", content, fileName, contentType, cancellationToken);

    public Task<ApiResult<MediaAssetResponse>> GetMediaAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<MediaAssetResponse>($"api/v1/admin/media/{id:D}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<MediaUsageResponse>>> ListMediaUsagesAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<MediaUsageResponse>>($"api/v1/admin/media/{id:D}/usages", cancellationToken);

    public Task<ApiResult<MediaAssetResponse>> UpdateMediaTextsAsync(Guid id, UpdateMediaTextsRequest request, CancellationToken cancellationToken) =>
        PutAsync<MediaAssetResponse>($"api/v1/admin/media/{id:D}/texts", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteMediaAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"api/v1/admin/media/{id:D}", cancellationToken);

    public Task<ApiFileStream?> OpenMediaFileAsync(Guid id, CancellationToken cancellationToken) =>
        GetStreamAsync($"api/v1/admin/media/{id:D}/content", cancellationToken);

    public Task<ApiResult<PagedResult<RedirectResponse>>> ListRedirectsAsync(string? search, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<RedirectResponse>>($"api/v1/admin/redirects?page={Number(page)}&pageSize=50{Query("search", search)}", cancellationToken);

    public Task<ApiResult<RedirectResponse>> CreateRedirectAsync(SaveRedirectRequest request, CancellationToken cancellationToken) =>
        PostAsync<RedirectResponse>("api/v1/admin/redirects", request, cancellationToken);

    public Task<ApiResult<RedirectResponse>> UpdateRedirectAsync(Guid id, SaveRedirectRequest request, CancellationToken cancellationToken) =>
        PutAsync<RedirectResponse>($"api/v1/admin/redirects/{id:D}", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteRedirectAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"api/v1/admin/redirects/{id:D}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<CtaResponse>>> ListCtasAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<CtaResponse>>("api/v1/admin/ctas", cancellationToken);

    public Task<ApiResult<CtaResponse>> CreateCtaAsync(SaveCtaRequest request, CancellationToken cancellationToken) =>
        PostAsync<CtaResponse>("api/v1/admin/ctas", request, cancellationToken);

    public Task<ApiResult<CtaResponse>> UpdateCtaAsync(Guid id, SaveCtaRequest request, CancellationToken cancellationToken) =>
        PutAsync<CtaResponse>($"api/v1/admin/ctas/{id:D}", request, cancellationToken);

    public Task<ApiResult<CtaDefaultsResponse>> GetCtaDefaultsAsync(CancellationToken cancellationToken) =>
        GetAsync<CtaDefaultsResponse>("api/v1/admin/ctas/defaults", cancellationToken);

    public Task<ApiResult<CtaDefaultsResponse>> SetCtaDefaultAsync(SetCtaDefaultRequest request, CancellationToken cancellationToken) =>
        PutAsync<CtaDefaultsResponse>("api/v1/admin/ctas/defaults", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<ClusterResponse>>> ListClustersAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ClusterResponse>>("api/v1/admin/clusters", cancellationToken);

    public Task<ApiResult<ClusterResponse>> CreateClusterAsync(SaveClusterRequest request, CancellationToken cancellationToken) =>
        PostAsync<ClusterResponse>("api/v1/admin/clusters", request, cancellationToken);

    public Task<ApiResult<ClusterResponse>> SetClusterMemberAsync(Guid id, SetClusterMemberRequest request, CancellationToken cancellationToken) =>
        PutAsync<ClusterResponse>($"api/v1/admin/clusters/{id:D}/members", request, cancellationToken);

    public Task<ApiResult<ClusterResponse>> RemoveClusterMemberAsync(Guid id, Guid contentItemId, CancellationToken cancellationToken) =>
        DeleteAsync<ClusterResponse>($"api/v1/admin/clusters/{id:D}/members/{contentItemId:D}", cancellationToken);

    public Task<ApiResult<NoContent>> DeleteClusterAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"api/v1/admin/clusters/{id:D}", cancellationToken);

    public Task<ApiResult<BrandKitResponse>> GetBrandKitAsync(CancellationToken cancellationToken) =>
        GetAsync<BrandKitResponse>("api/v1/admin/brand-kit", cancellationToken);

    public Task<ApiResult<BrandKitResponse>> UpdateBrandKitAsync(UpdateBrandKitRequest request, CancellationToken cancellationToken) =>
        PutAsync<BrandKitResponse>("api/v1/admin/brand-kit", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<KnowledgeEntryResponse>>> ListKnowledgeAsync(string? status, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<KnowledgeEntryResponse>>(string.IsNullOrWhiteSpace(status) ? "api/v1/admin/knowledge" : $"api/v1/admin/knowledge?status={Uri.EscapeDataString(status)}", cancellationToken);

    public Task<ApiResult<KnowledgeEntryResponse>> CreateKnowledgeAsync(SaveKnowledgeEntryRequest request, CancellationToken cancellationToken) =>
        PostAsync<KnowledgeEntryResponse>("api/v1/admin/knowledge", request, cancellationToken);

    public Task<ApiResult<KnowledgeEntryResponse>> UpdateKnowledgeAsync(Guid id, SaveKnowledgeEntryRequest request, CancellationToken cancellationToken) =>
        PutAsync<KnowledgeEntryResponse>($"api/v1/admin/knowledge/{id:D}", request, cancellationToken);

    public Task<ApiResult<KnowledgeEntryResponse>> KnowledgeActionAsync(Guid id, string action, uint version, CancellationToken cancellationToken) =>
        action is "approve" or "retire"
            ? PostAsync<KnowledgeEntryResponse>($"api/v1/admin/knowledge/{id:D}/{action}", new KnowledgeVersionRequest(version), cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action));

    public Task<ApiResult<GovernanceRulesResponse>> GetGovernanceRulesAsync(CancellationToken cancellationToken) =>
        GetAsync<GovernanceRulesResponse>("api/v1/admin/knowledge/governance-rules", cancellationToken);

    public Task<ApiResult<IReadOnlyList<InformationRequestResponse>>> ListInformationRequestsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<InformationRequestResponse>>("api/v1/admin/knowledge/information-requests", cancellationToken);

    public Task<ApiResult<InformationRequestResponse>> RaiseInformationRequestAsync(RaiseInformationRequest request, CancellationToken cancellationToken) =>
        PostAsync<InformationRequestResponse>("api/v1/admin/knowledge/information-requests", request, cancellationToken);

    public Task<ApiResult<InformationRequestResponse>> ResolveInformationRequestAsync(Guid id, ResolveInformationRequest request, CancellationToken cancellationToken) =>
        PostAsync<InformationRequestResponse>($"api/v1/admin/knowledge/information-requests/{id:D}/resolve", request, cancellationToken);

    public Task<ApiResult<PagedResult<KeywordTargetResponse>>> ListKeywordsAsync(string? search, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<KeywordTargetResponse>>($"api/v1/admin/keywords?page={Number(page)}&pageSize=50{Query("search", search)}", cancellationToken);

    public Task<ApiResult<KeywordTargetResponse>> CreateKeywordAsync(SaveKeywordTargetRequest request, CancellationToken cancellationToken) =>
        PostAsync<KeywordTargetResponse>("api/v1/admin/keywords", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteKeywordAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"api/v1/admin/keywords/{id:D}", cancellationToken);

    public Task<ApiResult<KeywordConflictResponse>> CheckKeywordAsync(string languageCode, string keyword, CancellationToken cancellationToken) =>
        GetAsync<KeywordConflictResponse>($"api/v1/admin/keywords/conflicts?language={Uri.EscapeDataString(languageCode)}&keyword={Uri.EscapeDataString(keyword)}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<CompetitorResponse>>> ListCompetitorsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<CompetitorResponse>>("api/v1/admin/competitors", cancellationToken);

    public Task<ApiResult<CompetitorResponse>> CreateCompetitorAsync(SaveCompetitorRequest request, CancellationToken cancellationToken) =>
        PostAsync<CompetitorResponse>("api/v1/admin/competitors", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteCompetitorAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"api/v1/admin/competitors/{id:D}", cancellationToken);

    public Task<ApiResult<PagedResult<RecommendationResponse>>> ListRecommendationsAsync(string? status, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<RecommendationResponse>>($"api/v1/admin/seo-recommendations?page={Number(page)}&pageSize=25{Query("status", status)}", cancellationToken);

    public Task<ApiResult<RecommendationResponse>> ProposeRecommendationAsync(ProposeRecommendationRequest request, CancellationToken cancellationToken) =>
        PostAsync<RecommendationResponse>("api/v1/admin/seo-recommendations", request, cancellationToken);

    public Task<ApiResult<RecommendationResponse>> RecommendationActionAsync(Guid id, string action, DecideRequest request, CancellationToken cancellationToken) =>
        action is "approve" or "reject" or "apply" or "mark-applied"
            ? PostAsync<RecommendationResponse>($"api/v1/admin/seo-recommendations/{id:D}/{action}", request, cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action));

    public Task<ApiResult<PagedResult<CalendarEntryResponse>>> ListCalendarAsync(string? status, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<CalendarEntryResponse>>($"api/v1/admin/content-calendar?page={Number(page)}&pageSize=50{Query("status", status)}", cancellationToken);

    public Task<ApiResult<CalendarEntryResponse>> ProposeCalendarEntryAsync(ProposeCalendarEntryRequest request, CancellationToken cancellationToken) =>
        PostAsync<CalendarEntryResponse>("api/v1/admin/content-calendar", request, cancellationToken);

    public Task<ApiResult<CalendarEntryResponse>> CalendarActionAsync(Guid id, string action, DecideRequest request, CancellationToken cancellationToken) =>
        action is "accept" or "reject" or "complete"
            ? PostAsync<CalendarEntryResponse>($"api/v1/admin/content-calendar/{id:D}/{action}", request, cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action));

    public Task<ApiResult<CalendarEntryResponse>> LinkCalendarContentAsync(Guid id, LinkCalendarContentRequest request, CancellationToken cancellationToken) =>
        PostAsync<CalendarEntryResponse>($"api/v1/admin/content-calendar/{id:D}/link", request, cancellationToken);

    public Task<ApiResult<IntelligenceCapabilitiesResponse>> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
        GetAsync<IntelligenceCapabilitiesResponse>("api/v1/admin/intelligence/capabilities", cancellationToken);

    public Task<ApiResult<InternalLinkReportResponse>> GetLinkReportAsync(CancellationToken cancellationToken) =>
        GetAsync<InternalLinkReportResponse>("api/v1/admin/intelligence/link-report", cancellationToken);

    public Task<ApiResult<GeneratedRecommendationsResponse>> GenerateLinkRecommendationsAsync(CancellationToken cancellationToken) =>
        PostAsync<GeneratedRecommendationsResponse>("api/v1/admin/intelligence/link-report/recommendations", null, cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> VerifyProofAsync(Guid id, VerifyProofRequest request, CancellationToken cancellationToken) =>
        PostAsync<ContentDetailResponse>($"{Content}/{id:D}/proof-verification", request, cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> RevokeProofAsync(Guid id, uint version, CancellationToken cancellationToken) =>
        PostAsync<ContentDetailResponse>($"{Content}/{id:D}/proof-verification/revoke", new ContentVersionRequest(version), cancellationToken);

    public Task<ApiResult<ContentDetailResponse>> SetFeaturedAsync(Guid id, SetFeaturedRequest request, CancellationToken cancellationToken) =>
        PostAsync<ContentDetailResponse>($"{Content}/{id:D}/featured", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<ProofItemResponse>>> ListProofItemsAsync(Guid contentItemId, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ProofItemResponse>>($"{ProofItems}?contentItemId={contentItemId:D}", cancellationToken);

    public Task<ApiResult<ProofItemResponse>> CreateProofItemAsync(SaveProofItemRequest request, CancellationToken cancellationToken) =>
        PostAsync<ProofItemResponse>(ProofItems, request, cancellationToken);

    public Task<ApiResult<ProofItemResponse>> UpdateProofItemAsync(Guid id, SaveProofItemRequest request, CancellationToken cancellationToken) =>
        PutAsync<ProofItemResponse>($"{ProofItems}/{id:D}", request, cancellationToken);

    public Task<ApiResult<ProofItemResponse>> VerifyProofItemAsync(Guid id, VerifyProofRequest request, CancellationToken cancellationToken) =>
        PostAsync<ProofItemResponse>($"{ProofItems}/{id:D}/verify", request, cancellationToken);

    public Task<ApiResult<ProofItemResponse>> ProofItemActionAsync(Guid id, string action, uint version, CancellationToken cancellationToken) =>
        action is "publish" or "unpublish"
            ? PostAsync<ProofItemResponse>($"{ProofItems}/{id:D}/{action}", new ContentVersionRequest(version), cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action));

    public Task<ApiResult<NoContent>> DeleteProofItemAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{ProofItems}/{id:D}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<AuthorResponse>>> ListAuthorsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<AuthorResponse>>(Authors, cancellationToken);

    public Task<ApiResult<AuthorResponse>> CreateAuthorAsync(SaveAuthorRequest request, CancellationToken cancellationToken) =>
        PostAsync<AuthorResponse>(Authors, request, cancellationToken);

    public Task<ApiResult<AuthorResponse>> UpdateAuthorAsync(Guid id, SaveAuthorRequest request, CancellationToken cancellationToken) =>
        PutAsync<AuthorResponse>($"{Authors}/{id:D}", request, cancellationToken);

    public Task<ApiResult<NoContent>> DeleteAuthorAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync<NoContent>($"{Authors}/{id:D}", cancellationToken);

    public Task<ApiResult<IReadOnlyList<EmailTemplateResponse>>> ListEmailTemplatesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<EmailTemplateResponse>>("api/v1/admin/email-templates", cancellationToken);

    public Task<ApiResult<EmailTemplateResponse>> SaveEmailTemplateAsync(string key, string languageCode, SaveEmailTemplateCopyRequest request, CancellationToken cancellationToken) =>
        PutAsync<EmailTemplateResponse>($"api/v1/admin/email-templates/{Uri.EscapeDataString(key)}/{Uri.EscapeDataString(languageCode)}", request, cancellationToken);

    public Task<ApiResult<EmailTemplateResponse>> ResetEmailTemplateAsync(string key, string languageCode, CancellationToken cancellationToken) =>
        DeleteAsync<EmailTemplateResponse>($"api/v1/admin/email-templates/{Uri.EscapeDataString(key)}/{Uri.EscapeDataString(languageCode)}", cancellationToken);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Query(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"&{name}={Uri.EscapeDataString(value.Trim())}";
}
