namespace Onyx.Contracts.SiteContent;

/// <summary>Every published site content document (key → JSON object), served to Onyx.Web and cached there.</summary>
public sealed record PublicSiteContentResponse(IReadOnlyDictionary<string, string> Documents);
