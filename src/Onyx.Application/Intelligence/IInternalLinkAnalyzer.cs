using Onyx.Contracts.Intelligence;

namespace Onyx.Application.Intelligence;

public interface IInternalLinkAnalyzer
{
    Task<InternalLinkReportResponse> AnalyzeAsync(CancellationToken cancellationToken);

    /// <summary>Turns report findings into proposed recommendations (rule engine, with evidence) for editor review.</summary>
    Task<GeneratedRecommendationsResponse> GenerateRecommendationsAsync(CancellationToken cancellationToken);
}
