using Onyx.Contracts.Content;
using Onyx.Domain.Common;

namespace Onyx.Application.Content;

/// <summary>Errors, warnings and recommendations. Deliberately not a score: no number here implies anything about rankings.</summary>
public sealed record QualityReport(IReadOnlyList<QualityFinding> Findings)
{
    public bool CanPublish => Findings.All(finding => finding.Severity != QualitySeverity.Error);

    public QualityReportResponse ToResponse() => new(
        CanPublish,
        Findings.Select(finding => new QualityFindingResponse(finding.Severity.ToString(), finding.Code, finding.Message, finding.LanguageCode, finding.Field)).ToList());

    public Error ToError() => Error.Validation(
        "Content.QualityGate",
        "The content has blocking issues. Fix the errors in the quality report.",
        Findings.Where(finding => finding.Severity == QualitySeverity.Error)
            .GroupBy(finding => finding.LanguageCode is null ? finding.Code : $"{finding.Code}:{finding.LanguageCode}")
            .ToDictionary(group => group.Key, group => group.Select(finding => finding.Message).ToArray()));
}
