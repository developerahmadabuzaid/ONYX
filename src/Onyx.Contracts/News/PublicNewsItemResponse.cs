namespace Onyx.Contracts.News;

/// <summary>
/// A published item: Onyx's headline and summary with the attribution every item must show (publisher, link to the original,
/// the source's publication time when known, Onyx's publication time). <see cref="Indexable"/> false means <c>noindex, follow</c>
/// and no alternates are declared.
/// </summary>
public sealed record PublicNewsItemResponse(
    string Title,
    string Summary,
    string Path,
    string Publisher,
    string OriginalUrl,
    DateTimeOffset? SourcePublishedAtUtc,
    DateTimeOffset PublishedAtUtc,
    PublicNewsCategoryResponse? Category,
    bool Indexable,
    IReadOnlyDictionary<string, string> Alternates);
