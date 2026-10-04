using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Builds email content from wording (default or admin-edited) plus model values; values stay plain text.</summary>
public static class EditableEmail
{
    public static EmailContent Compose(IEditableEmailTemplate template, EmailCopy copy, IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(model);

        var url = template.ActionUrl(model);
        return new EmailContent(
            EmailCopyRules.Fill(copy.Subject, model),
            EmailCopyRules.Fill(copy.Heading, model),
            copy.Paragraphs.Select(paragraph => EmailCopyRules.Fill(paragraph, model)).ToList(),
            url is not null && copy.ActionLabel is { } label ? new EmailAction(EmailCopyRules.Fill(label, model), url) : null,
            copy.Footer is null ? null : EmailCopyRules.Fill(copy.Footer, model));
    }
}
