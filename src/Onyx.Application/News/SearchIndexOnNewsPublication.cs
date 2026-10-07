using Onyx.Application.Abstractions.Messaging;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>Re-indexes (or removes) an item whenever what the public sees of it changes.</summary>
public sealed class SearchIndexOnNewsPublication(NewsSearchIndexer indexer) : IDomainEventHandler<NewsItemPublicationChanged>
{
    public async Task HandleAsync(NewsItemPublicationChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await indexer.ReindexAsync(domainEvent.NewsItemId, cancellationToken).ConfigureAwait(false);
    }
}
