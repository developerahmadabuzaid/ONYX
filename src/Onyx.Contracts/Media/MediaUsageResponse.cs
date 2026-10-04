using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Media;

public sealed record MediaUsageResponse(Guid ContentItemId, string LanguageCode, string Context, bool IsLive, IReadOnlyDictionary<string, string> Titles);
