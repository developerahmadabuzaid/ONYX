using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Content;

/// <summary>Insight byline and provenance. <see cref="Author"/> is null when the organization is credited.</summary>
public sealed record PublicInsightResponse(string Kind, string Origin, PublicAuthorResponse? Author);
