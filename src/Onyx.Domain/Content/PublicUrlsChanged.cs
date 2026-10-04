using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// Public URLs whose answer changed (now live, updated, moved, redirected or gone). <see cref="Paths"/> are site paths with the
/// language prefix, unencoded (e.g. <c>/ar/الخدمات/x</c>). Consumed by search-engine change notification (IndexNow).
/// </summary>
public sealed record PublicUrlsChanged(Guid SourceId, IReadOnlyList<string> Paths) : IDomainEvent
{
    /// <summary>Upper bound per event; IndexNow accepts at most 10,000 URLs per request.</summary>
    public const int MaxPaths = 1_000;
}
