using Onyx.Application.Identity;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Loads content for an admin operation, enforcing type scope and (for changes) optimistic concurrency.</summary>
public sealed class ContentAccess(IContentRepository content, ContentAuthorizer authorizer)
{
    public async Task<Result<(ContentItem Item, ActorPermissions Actor)>> ForReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        var item = await content.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return ContentErrors.NotFound;
        }

        return ContentAuthorizer.CheckRead(actor, item.Type) is { } forbidden ? forbidden : (item, actor);
    }

    public async Task<Result<(ContentItem Item, ActorPermissions Actor)>> ForEditAsync(Guid id, uint version, CancellationToken cancellationToken)
    {
        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        var item = await content.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return ContentErrors.NotFound;
        }

        if (ContentAuthorizer.CheckEdit(actor, item.Type) is { } forbidden)
        {
            return forbidden;
        }

        content.SetExpectedVersion(item, version);
        return (item, actor);
    }
}
