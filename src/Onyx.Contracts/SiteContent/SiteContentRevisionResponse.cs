namespace Onyx.Contracts.SiteContent;

/// <summary>A published version of a document, newest first.</summary>
public sealed record SiteContentRevisionResponse(int Number, DateTimeOffset PublishedAtUtc, Guid? PublishedById, string Json);
