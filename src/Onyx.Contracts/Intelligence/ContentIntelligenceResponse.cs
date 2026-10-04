using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary>Health is computed from real snapshots only; without them it is InsufficientData.</summary>
public sealed record ContentIntelligenceResponse(
    Guid ContentItemId,
    string LanguageCode,
    string Health,
    IReadOnlyList<string> HealthReasons,
    IReadOnlyList<PerformanceSnapshotResponse> Snapshots,
    IReadOnlyList<DataSourceStatusResponse> Sources);
