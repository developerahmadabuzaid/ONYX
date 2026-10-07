using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>An item went public, stopped being public, or changed what the public sees (search index, sitemap).</summary>
public sealed record NewsItemPublicationChanged(Guid NewsItemId) : IDomainEvent;
