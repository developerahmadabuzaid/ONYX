using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>
/// A template whose wording admins may edit per language. The link (e.g. a reset or invitation URL) is produced by code from
/// the model and is never part of the editable copy.
/// </summary>
public interface IEditableEmailTemplate : IEmailTemplate
{
    /// <summary>Model values that may appear as <c>{placeholder}</c> in the wording.</summary>
    IReadOnlyCollection<string> Placeholders { get; }

    /// <summary>Placeholders the wording must keep (e.g. how long a link stays valid).</summary>
    IReadOnlyCollection<string> RequiredPlaceholders { get; }

    bool HasAction { get; }

    EmailCopy DefaultCopy(string languageCode);

    /// <summary>The code-owned action URL, built from the model; null when the template has no button.</summary>
    Uri? ActionUrl(IReadOnlyDictionary<string, string> model);
}
