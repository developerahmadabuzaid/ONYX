using Onyx.Contracts.Common;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

public interface ISeoRecommendationRepository
{
    Task<SeoRecommendation?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<SeoRecommendation>> ListAsync(
        RecommendationStatus? status, Guid? contentItemId, RecommendationKind? kind, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>An open (proposed or approved) recommendation of this kind already exists for the same target.</summary>
    Task<bool> OpenExistsAsync(RecommendationKind kind, Guid? contentItemId, string? languageCode, string title, CancellationToken cancellationToken);

    void Add(SeoRecommendation recommendation);

    void SetExpectedVersion(SeoRecommendation recommendation, uint version);

    void AddObservation(SeoChangeObservation observation);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<SeoChangeObservation>>> ListObservationsAsync(
        IReadOnlyCollection<Guid> recommendationIds, CancellationToken cancellationToken);
}
