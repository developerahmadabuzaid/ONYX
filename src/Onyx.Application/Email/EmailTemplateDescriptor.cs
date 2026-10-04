namespace Onyx.Application.Email;

/// <summary>What admins may edit for a code-owned template: languages, allowed and required placeholders, and its default copy.</summary>
public sealed record EmailTemplateDescriptor(
    string Key,
    IReadOnlyCollection<string> Languages,
    IReadOnlyCollection<string> Placeholders,
    IReadOnlyCollection<string> RequiredPlaceholders,
    bool HasAction,
    Func<string, EmailCopy> DefaultCopy);
