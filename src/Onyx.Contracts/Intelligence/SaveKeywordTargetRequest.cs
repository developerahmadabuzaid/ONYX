using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record SaveKeywordTargetRequest(
    string Keyword, string LanguageCode, string? TargetMarket, string SearchIntent, Guid? TargetContentItemId, Guid? ClusterId, string Status, string? Notes, uint? Version);
