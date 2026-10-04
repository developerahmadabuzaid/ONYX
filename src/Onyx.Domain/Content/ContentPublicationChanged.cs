using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>Raised when content goes live, is re-published or goes offline (future: search index, cache, sitemap regeneration).</summary>
public sealed record ContentPublicationChanged(Guid ContentItemId, bool IsLive) : IDomainEvent;
