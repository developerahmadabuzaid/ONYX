using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

public interface IContentAdministrationService
{
    Task<Result<ContentDetailResponse>> CreateAsync(CreateContentRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<PagedResult<ContentSummaryResponse>>> ListAsync(
        string? type, string? status, bool? isLive, bool needsUrlDecision, Guid? parentId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> UpdateTranslationAsync(Guid id, string languageCode, UpdateContentTranslationRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> UpdateSeoAsync(Guid id, string languageCode, UpdateContentSeoRequest request, CancellationToken cancellationToken);

    Task<Result<ContentDetailResponse>> UpdateSettingsAsync(Guid id, UpdateContentSettingsRequest request, CancellationToken cancellationToken);

    Task<Result> DeletePermanentAsync(Guid id, DeleteContentRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ContentRelationResponse>>> ListRelationsAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<ContentRelationResponse>> AddRelationAsync(Guid id, AddContentRelationRequest request, CancellationToken cancellationToken);

    Task<Result> RemoveRelationAsync(Guid id, Guid relationId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<RelatedSuggestionResponse>>> SuggestRelatedAsync(Guid id, CancellationToken cancellationToken);
}
