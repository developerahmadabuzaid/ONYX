using Onyx.Contracts.Common;
using Onyx.Contracts.News;

namespace Onyx.Web.ApiClients;

public interface IAdminNewsApiClient
{
    Task<ApiResult<PagedResult<NewsItemListItemResponse>>> ListItemsAsync(string? status, Guid? sourceId, int page, CancellationToken cancellationToken);

    Task<ApiResult<NewsItemResponse>> GetItemAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<NewsItemResponse>> EditTranslationAsync(Guid id, string language, UpdateNewsTranslationRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NewsItemResponse>> ChangeCategoryAsync(Guid id, ChangeNewsCategoryRequest request, CancellationToken cancellationToken);

    /// <summary><paramref name="action"/> is one of publish, hide, unpublish, delete.</summary>
    Task<ApiResult<NewsItemResponse>> ChangeStateAsync(Guid id, string action, NewsVersionRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NewsItemResponse>> SetIndexingAsync(Guid id, SetNewsIndexingRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<NewsSourceResponse>>> ListSourcesAsync(CancellationToken cancellationToken);

    Task<ApiResult<NewsSourceResponse>> GetSourceAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiResult<NewsSourceResponse>> CreateSourceAsync(SaveNewsSourceRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NewsSourceResponse>> UpdateSourceAsync(Guid id, SaveNewsSourceRequest request, CancellationToken cancellationToken);

    /// <summary><paramref name="action"/> is allow or block.</summary>
    Task<ApiResult<NewsSourceResponse>> ChangeSourceAsync(Guid id, string action, NewsVersionRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<NewsCategoryResponse>>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<ApiResult<NewsCategoryResponse>> CreateCategoryAsync(SaveNewsCategoryRequest request, CancellationToken cancellationToken);

    Task<ApiResult<NewsCategoryResponse>> UpdateCategoryAsync(Guid id, SaveNewsCategoryRequest request, CancellationToken cancellationToken);
}
