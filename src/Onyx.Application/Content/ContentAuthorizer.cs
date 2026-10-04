using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// Content type scope on top of the generic <c>Content.*</c> lifecycle permission that the API already enforced:
/// reading an item needs <c>{Type}.View</c>, changing it needs <c>{Type}.Edit</c>. Enforced server-side for every operation.
/// </summary>
public sealed class ContentAuthorizer(ICurrentAdmin currentAdmin)
{
    public static readonly Error TypeForbidden = Error.Forbidden("Content.TypeForbidden", "You do not have access to this type of content.");

    public Task<ActorPermissions> ActorAsync(CancellationToken cancellationToken) => currentAdmin.GetPermissionsAsync(cancellationToken);

    public static bool Has(ActorPermissions actor, string permission)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.IsSuperAdmin || actor.Permissions.Contains(permission);
    }

    public static Error? CheckRead(ActorPermissions actor, ContentType type) => Has(actor, ViewPermission(type)) ? null : TypeForbidden;

    public static Error? CheckEdit(ActorPermissions actor, ContentType type) => Has(actor, EditPermission(type)) ? null : TypeForbidden;

    public static IReadOnlyCollection<ContentType> ReadableTypes(ActorPermissions actor) =>
        Enum.GetValues<ContentType>().Where(type => Has(actor, ViewPermission(type))).ToList();

    public static string ViewPermission(ContentType type) => type switch
    {
        ContentType.ServicePillar => Permissions.Pillars.View,
        ContentType.Service => Permissions.Services.View,
        ContentType.Industry => Permissions.Industries.View,
        ContentType.Page => Permissions.Pages.View,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string EditPermission(ContentType type) => type switch
    {
        ContentType.ServicePillar => Permissions.Pillars.Edit,
        ContentType.Service => Permissions.Services.Edit,
        ContentType.Industry => Permissions.Industries.Edit,
        ContentType.Page => Permissions.Pages.Edit,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
