using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>
/// <see cref="Basis"/>: Evidence, Estimated or InsufficientData. Without real performance data there is no recommended time;
/// a recommendation never publishes anything.
/// </summary>
public sealed record PublishingTimeRecommendationResponse(string Basis, DateTimeOffset? RecommendedAtUtc, string Explanation);
