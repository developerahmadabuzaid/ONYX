using System.Globalization;
using Onyx.Contracts.Common;
using Onyx.Contracts.News;

namespace Onyx.Web.ApiClients;

public sealed class AdminNewsApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminNewsApiClient
{
    private const string Items = "api/v1/admin/news";
    private const string Sources = "api/v1/admin/news-sources";
    private const string Categories = "api/v1/admin/news-categories";

    private static readonly HashSet<string> ItemActions = new(StringComparer.Ordinal) { "publish", "hide", "unpublish", "delete" };
    private static readonly HashSet<string> SourceActions = new(StringComparer.Ordinal) { "allow", "block" };

    public Task<ApiResult<PagedResult<NewsItemListItemResponse>>> ListItemsAsync(string? status, Guid? sourceId, int page, CancellationToken cancellationToken) =>
        GetAsync<PagedResult<NewsItemListItemResponse>>(
            $"{Items}?page={page.ToString(CultureInfo.InvariantCulture)}&pageSize=25"
            + (string.IsNullOrWhiteSpace(status) ? string.Empty : "&status=" + Uri.EscapeDataString(status))
            + (sourceId is { } source ? $"&sourceId={source:D}" : string.Empty),
            cancellationToken);

    public Task<ApiResult<NewsItemResponse>> GetItemAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<NewsItemResponse>($"{Items}/{id:D}", cancellationToken);

    public Task<ApiResult<NewsItemResponse>> EditTranslationAsync(Guid id, string language, UpdateNewsTranslationRequest request, CancellationToken cancellationToken) =>
        PutAsync<NewsItemResponse>($"{Items}/{id:D}/translations/{Uri.EscapeDataString(language)}", request, cancellationToken);

    public Task<ApiResult<NewsItemResponse>> ChangeCategoryAsync(Guid id, ChangeNewsCategoryRequest request, CancellationToken cancellationToken) =>
        PutAsync<NewsItemResponse>($"{Items}/{id:D}/category", request, cancellationToken);

    public Task<ApiResult<NewsItemResponse>> ChangeStateAsync(Guid id, string action, NewsVersionRequest request, CancellationToken cancellationToken) =>
        ItemActions.Contains(action)
            ? PostAsync<NewsItemResponse>($"{Items}/{id:D}/{action}", request, cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action));

    public Task<ApiResult<NewsItemResponse>> SetIndexingAsync(Guid id, SetNewsIndexingRequest request, CancellationToken cancellationToken) =>
        PutAsync<NewsItemResponse>($"{Items}/{id:D}/indexing", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<NewsSourceResponse>>> ListSourcesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<NewsSourceResponse>>(Sources, cancellationToken);

    public Task<ApiResult<NewsSourceResponse>> GetSourceAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<NewsSourceResponse>($"{Sources}/{id:D}", cancellationToken);

    public Task<ApiResult<NewsSourceResponse>> CreateSourceAsync(SaveNewsSourceRequest request, CancellationToken cancellationToken) =>
        PostAsync<NewsSourceResponse>(Sources, request, cancellationToken);

    public Task<ApiResult<NewsSourceResponse>> UpdateSourceAsync(Guid id, SaveNewsSourceRequest request, CancellationToken cancellationToken) =>
        PutAsync<NewsSourceResponse>($"{Sources}/{id:D}", request, cancellationToken);

    public Task<ApiResult<NewsSourceResponse>> ChangeSourceAsync(Guid id, string action, NewsVersionRequest request, CancellationToken cancellationToken) =>
        SourceActions.Contains(action)
            ? PostAsync<NewsSourceResponse>($"{Sources}/{id:D}/{action}", request, cancellationToken)
            : throw new ArgumentOutOfRangeException(nameof(action));

    public Task<ApiResult<IReadOnlyList<NewsCategoryResponse>>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<NewsCategoryResponse>>(Categories, cancellationToken);

    public Task<ApiResult<NewsCategoryResponse>> CreateCategoryAsync(SaveNewsCategoryRequest request, CancellationToken cancellationToken) =>
        PostAsync<NewsCategoryResponse>(Categories, request, cancellationToken);

    public Task<ApiResult<NewsCategoryResponse>> UpdateCategoryAsync(Guid id, SaveNewsCategoryRequest request, CancellationToken cancellationToken) =>
        PutAsync<NewsCategoryResponse>($"{Categories}/{id:D}", request, cancellationToken);
}
