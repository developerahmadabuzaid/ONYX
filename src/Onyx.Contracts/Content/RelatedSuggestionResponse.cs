using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>A rule-based suggestion (same cluster, sibling service, parent…). Not saved until an editor adds it.</summary>
public sealed record RelatedSuggestionResponse(Guid ContentItemId, string Reason, IReadOnlyDictionary<string, string> Titles);
