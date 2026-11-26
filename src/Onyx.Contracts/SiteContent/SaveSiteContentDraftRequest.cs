namespace Onyx.Contracts.SiteContent;

/// <summary>The full working copy to save. <see cref="Version"/> is null only when the document does not exist yet.</summary>
public sealed record SaveSiteContentDraftRequest(string Json, uint? Version);
