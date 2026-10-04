using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record SaveCtaRequest(string Key, string ActionType, string? TargetUrl, IReadOnlyList<LocalizedTextDto> Texts, bool IsActive, uint? Version);
