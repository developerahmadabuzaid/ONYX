using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Severity"/>: Error (blocks publishing), Warning or Recommendation. Findings are checks, not a ranking score.</summary>
public sealed record QualityFindingResponse(string Severity, string Code, string Message, string? LanguageCode, string? Field);
