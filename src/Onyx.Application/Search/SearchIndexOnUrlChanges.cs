using Onyx.Application.Abstractions.Messaging;
using Onyx.Domain.Content;

namespace Onyx.Application.Search;

/// <summary>
/// Public URLs changed (moves, URL decisions, redirects): the source page and every page currently indexed under one of the
/// announced paths (for example children of a moved parent) are re-indexed, so stored URLs never point to old addresses.
/// </summary>
public sealed class SearchIndexOnUrlChanges(SearchIndexer indexer, ISearchDocumentRepository index) : IDomainEventHandler<PublicUrlsChanged>
{
    public async Task HandleAsync(PublicUrlsChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var affected = (await index.ListContentIdsByPathAsync(domainEvent.Paths, cancellationToken).ConfigureAwait(false))
            .Append(domainEvent.SourceId)
            .Distinct()
            .ToList();
        foreach (var contentItemId in affected)
        {
            await indexer.ReindexAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        }
    }
}
