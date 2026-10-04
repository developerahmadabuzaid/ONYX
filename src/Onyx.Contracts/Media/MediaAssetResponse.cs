using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Media;

public sealed record MediaAssetResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    string? Title,
    IReadOnlyList<LocalizedTextDto> Texts,
    DateTimeOffset CreatedAtUtc,
    int UsageCount,
    uint Version);
