namespace Onyx.Contracts.Content;

/// <summary>
/// <see cref="Url"/> is set only for URL calls to action; conversion actions are routed by the site. <see cref="Id"/> lets a
/// conversion form record which call to action the visitor came from.
/// </summary>
public sealed record PublicCtaResponse(string ActionType, string Label, string? Secondary, string? Url, Guid? Id = null);
