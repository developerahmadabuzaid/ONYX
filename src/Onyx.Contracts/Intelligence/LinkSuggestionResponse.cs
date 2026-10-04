using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary>Rule-based: the target's primary keyword appears in the source text but the source does not link to it yet.</summary>
public sealed record LinkSuggestionResponse(Guid FromContentItemId, Guid ToContentItemId, string LanguageCode, string AnchorText, string Reason);
