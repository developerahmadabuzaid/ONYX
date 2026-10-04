using Onyx.Contracts.Content;
using Onyx.Domain.Content;

namespace Onyx.Application.Intelligence;

/// <summary>Recommends when to publish. Never publishes, and never presents an estimate as analytics.</summary>
public interface IPublishingTimeAdvisor
{
    Task<PublishingTimeRecommendationResponse> RecommendAsync(ContentItem item, CancellationToken cancellationToken);
}
