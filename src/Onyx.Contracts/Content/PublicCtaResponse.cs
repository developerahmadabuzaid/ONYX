using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Url"/> is set only for URL calls to action; conversion actions are routed by the site.</summary>
public sealed record PublicCtaResponse(string ActionType, string Label, string? Secondary, string? Url);
