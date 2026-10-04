using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record PerformanceSnapshotResponse(string Source, DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, IReadOnlyDictionary<string, double> Metrics, DateTimeOffset CollectedAtUtc);
