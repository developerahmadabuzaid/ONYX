using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>Default call to action for a content type, or the global default when <see cref="ContentType"/> is null.</summary>
public sealed class CtaDefault : Entity, IAuditable
{
    private CtaDefault()
    {
    }

    public ContentType? ContentType { get; private set; }

    public Guid CallToActionId { get; private set; }

    public static CtaDefault Create(ContentType? contentType, Guid callToActionId) =>
        new() { ContentType = contentType, CallToActionId = callToActionId };

    public void Change(Guid callToActionId) => CallToActionId = callToActionId;
}
