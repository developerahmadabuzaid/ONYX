using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary>No volume, difficulty or ranking: <see cref="PerformanceDataStatus"/> says whether a connected provider supplies performance data.</summary>
public sealed record KeywordTargetResponse(
    Guid Id,
    string Keyword,
    string LanguageCode,
    string? TargetMarket,
    string SearchIntent,
    Guid? TargetContentItemId,
    Guid? ClusterId,
    string Status,
    string? Notes,
    string PerformanceDataStatus,
    uint Version);
