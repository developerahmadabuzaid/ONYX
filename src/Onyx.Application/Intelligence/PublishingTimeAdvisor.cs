using Onyx.Contracts.Content;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

/// <summary>
/// Evidence-first publishing-time advice. A recommendation needs historical performance and visitor-activity data from a
/// connected analytics provider; until such data exists the honest answer is "insufficient data", with no time suggested.
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
                ? "Performance data exists but is not yet sufficient for a reliable timing recommendation. Choose the time manually."
                : "No analytics provider is connected, so there is no visitor-activity data to base a recommendation on. Choose the time manually.");
    }
}
