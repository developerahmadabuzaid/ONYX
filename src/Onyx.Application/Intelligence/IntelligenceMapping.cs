using Onyx.Contracts.Intelligence;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

public static class IntelligenceMapping
{
    public static EvidenceDto ToDto(EvidenceItem evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new EvidenceDto(evidence.Source.ToString(), evidence.Description, evidence.ObservedAtUtc, evidence.PeriodStartUtc, evidence.PeriodEndUtc,
            evidence.Metrics is { Count: > 0 } metrics ? new Dictionary<string, double>(metrics) : null);
    }

    public static RecommendationResponse ToResponse(SeoRecommendation recommendation, IReadOnlyList<SeoChangeObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        return new RecommendationResponse(
            recommendation.Id,
            recommendation.ContentItemId,
            recommendation.LanguageCode,
            recommendation.ClusterId,
            recommendation.Kind.ToString(),
            recommendation.Origin.ToString(),
            recommendation.Status.ToString(),
            recommendation.Basis.ToString(),
            recommendation.Title,
            recommendation.DetectedIssue,
            recommendation.Reasoning,
            recommendation.ProposedChanges.Select(change => new ProposedChangeDto(change.Field, change.CurrentValue, change.ProposedValue)).ToList(),
            recommendation.Evidence.Select(ToDto).ToList(),
            recommendation.CreatedAtUtc,
            recommendation.DecidedAtUtc,
            recommendation.DecisionNote,
            recommendation.AppliedAtUtc,
            observations.OrderBy(observation => observation.RecordedAtUtc)
                .Select(observation => new ObservationResponse(observation.Id, observation.Kind.ToString(), observation.RecordedAtUtc, ToDto(observation.Evidence),
                    observation.Assessment.ToString(), observation.Note))
                .ToList(),
            recommendation.Version);
    }
}
