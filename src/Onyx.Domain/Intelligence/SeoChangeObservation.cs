using Onyx.Domain.Common;

namespace Onyx.Domain.Intelligence;

/// <summary>
/// A measurement around an applied SEO change: the baseline before it and later follow-ups. Assessments describe association,
/// never causation ("associated with improvement", "no clear change", "insufficient data").
/// </summary>
public sealed class SeoChangeObservation : Entity
{
    public const int NoteMaxLength = 1_000;

    private SeoChangeObservation()
    {
        Evidence = new EvidenceItem(EvidenceSource.RuleEngine, "-");
    }

    public Guid RecommendationId { get; private set; }

    public ObservationKind Kind { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    /// <summary>The measured data with its source and period. Metrics only from connected providers.</summary>
    public EvidenceItem Evidence { get; private set; }

    public ChangeAssessment Assessment { get; private set; }

    public string? Note { get; private set; }

    public static Result<SeoChangeObservation> Record(
        Guid recommendationId, ObservationKind kind, EvidenceItem evidence, ChangeAssessment assessment, string? note, bool hasBaseline, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Problem() is { } problem)
        {
            return Error.Validation("Observation.Invalid", problem);
        }

        if (!Enum.IsDefined(kind) || !Enum.IsDefined(assessment) || note is { Length: > NoteMaxLength })
        {
            return Error.Validation("Observation.Invalid", "Unknown observation kind or assessment.");
        }

        // A baseline describes the starting point, and a follow-up without a baseline cannot support any comparison.
        if ((kind == ObservationKind.Baseline || !hasBaseline || evidence.Metrics is not { Count: > 0 }) && assessment != ChangeAssessment.InsufficientData)
        {
            return Error.Validation("Observation.InsufficientData", "Without a baseline and measured follow-up data the only honest assessment is 'insufficient data'.");
        }

        return new SeoChangeObservation
        {
            RecommendationId = recommendationId,
            Kind = kind,
            Evidence = evidence,
            Assessment = assessment,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RecordedAtUtc = now,
        };
    }
}
