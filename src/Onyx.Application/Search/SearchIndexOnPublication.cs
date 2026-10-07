using Onyx.Application.Abstractions.Messaging;
using Onyx.Domain.Content;

namespace Onyx.Application.Search;

/// <summary>Event-driven indexing (Worker): a page going live, being re-published or going offline is re-indexed.</summary>
public sealed class SearchIndexOnPublication(SearchIndexer indexer) : IDomainEventHandler<ContentPublicationChanged>
{
    public async Task HandleAsync(ContentPublicationChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await indexer.ReindexAsync(domainEvent.ContentItemId, cancellationToken).ConfigureAwait(false);
    }
}
