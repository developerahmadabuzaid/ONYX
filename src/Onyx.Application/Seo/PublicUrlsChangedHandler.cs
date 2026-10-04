using Onyx.Application.Abstractions.Messaging;
using Onyx.Domain.Content;

namespace Onyx.Application.Seo;

/// <summary>Forwards changed public URLs to search-engine change notification (no-op while it is inactive).</summary>
public sealed class PublicUrlsChangedHandler(ISearchEngineNotifier notifier) : IDomainEventHandler<PublicUrlsChanged>
{
    public Task HandleAsync(PublicUrlsChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.IsActive && domainEvent.Paths.Count > 0
            ? notifier.NotifyAsync(domainEvent.Paths, cancellationToken)
            : Task.CompletedTask;
    }
}
