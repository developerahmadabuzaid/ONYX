using Microsoft.EntityFrameworkCore;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Application.Knowledge;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class SeoRecommendationRepository(ApplicationDbContext dbContext) : ISeoRecommendationRepository
{
    public Task<SeoRecommendation?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SeoRecommendations.SingleOrDefaultAsync(recommendation => recommendation.Id == id, cancellationToken);

    public async Task<PagedResult<SeoRecommendation>> ListAsync(
        RecommendationStatus? status, Guid? contentItemId, RecommendationKind? kind, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.SeoRecommendations.AsNoTracking();
        if (status is { } statusFilter)
        {
            query = query.Where(recommendation => recommendation.Status == statusFilter);
        }

        if (contentItemId is { } contentFilter)
        {
            query = query.Where(recommendation => recommendation.ContentItemId == contentFilter);
        }

        if (kind is { } kindFilter)
        {
            query = query.Where(recommendation => recommendation.Kind == kindFilter);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(recommendation => recommendation.CreatedAtUtc).ThenBy(recommendation => recommendation.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<SeoRecommendation>(items, page, pageSize, total);
    }

    public Task<bool> OpenExistsAsync(RecommendationKind kind, Guid? contentItemId, string? languageCode, string title, CancellationToken cancellationToken) =>
        dbContext.SeoRecommendations.AnyAsync(recommendation => recommendation.Kind == kind && recommendation.ContentItemId == contentItemId
            && recommendation.LanguageCode == languageCode && recommendation.Title == title
            && (recommendation.Status == RecommendationStatus.Proposed || recommendation.Status == RecommendationStatus.Approved), cancellationToken);

    public void Add(SeoRecommendation recommendation) => dbContext.SeoRecommendations.Add(recommendation);

    public void SetExpectedVersion(SeoRecommendation recommendation, uint version) =>
        dbContext.Entry(recommendation).Property(entry => entry.Version).OriginalValue = version;

    public void AddObservation(SeoChangeObservation observation) => dbContext.SeoChangeObservations.Add(observation);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<SeoChangeObservation>>> ListObservationsAsync(
        IReadOnlyCollection<Guid> recommendationIds, CancellationToken cancellationToken)
    {
        if (recommendationIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<SeoChangeObservation>>();
        }

        var observations = await dbContext.SeoChangeObservations.AsNoTracking()
            .Where(observation => recommendationIds.Contains(observation.RecommendationId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return observations.GroupBy(observation => observation.RecommendationId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SeoChangeObservation>)group.ToList());
    }
}
