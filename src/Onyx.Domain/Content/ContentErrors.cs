using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

public static class ContentErrors
{
    public static readonly Error NotFound = Error.NotFound("Content.NotFound", "Content not found.");

    public static readonly Error Archived = Error.Conflict("Content.Archived", "Archived content cannot be edited. Restore it to a draft first.");

    public static Error InvalidTransition(ContentStatus from, string action) =>
        Error.Conflict("Content.InvalidTransition", $"Cannot {action} content that is {from}.");

    public static readonly Error NotLive = Error.Conflict("Content.NotLive", "The content is not live.");

    public static readonly Error HierarchyTooDeep = Error.Validation(
        "Content.HierarchyTooDeep", $"Content can be nested at most {ContentItem.MaxHierarchyDepth} levels deep (counting the top-level item).");

    public static readonly Error IncompleteTranslations =
        Error.Validation("Content.IncompleteTranslations", "Every configured language needs a title, a slug and content before approval or publishing.");

    public static readonly Error IndustryMinimum =
        Error.Validation("Content.IndustryMinimum", "An industry page needs a summary and at least three sections (context, problems, Onyx approach) in every language.");

    public static readonly Error ScheduleInPast = Error.Validation("Content.ScheduleInPast", "The scheduled time must be in the future.");

    public static readonly Error InvalidResolution =
        Error.Validation("Content.InvalidResolution", "Choose either a replacement page or Gone; a redirect needs a different, live target.");
}
