using Onyx.Contracts.Content;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

/// <summary>
/// Audience-timing advice, not a ranking factor: no authoritative source says the hour of publication affects search ranking.
/// A suggestion may only ever come from Onyx's own visitor-activity data from a connected analytics provider; until such data
/// and a validated method exist, the honest answer is "insufficient data", with no time suggested.
/// </summary>
public sealed class PublishingTimeAdvisor(IContentPerformanceRepository performance) : IPublishingTimeAdvisor
{
    public async Task<PublishingTimeRecommendationResponse> RecommendAsync(ContentItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var hasData = await performance.AnyAsync(cancellationToken).ConfigureAwait(false);

        // The data model exists, but no analysis is claimed until real provider data and a validated method are in place.
        return new PublishingTimeRecommendationResponse(
            RecommendationBasis.InsufficientData.ToString(),
            null,
            hasData
                ? "Audience timing only (publication time is not a search ranking factor). Visitor data exists but is not yet sufficient for a reliable suggestion. Choose the time manually."
                : "Audience timing only (publication time is not a search ranking factor). No analytics provider is connected, so there is no visitor-activity data to base a suggestion on. Choose the time manually.");
    }
}
