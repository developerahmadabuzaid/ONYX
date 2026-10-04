using Onyx.Application.Email;
using Onyx.Infrastructure.Email.Templates;

namespace Onyx.Infrastructure.Email;

/// <summary>Exposes the admin-editable templates (their placeholders and default copy) to the application layer.</summary>
internal sealed class EmailTemplateCatalog(IEnumerable<IEmailTemplate> templates) : IEmailTemplateCatalog
{
    private readonly IReadOnlyList<EmailTemplateDescriptor> _descriptors = templates
        .OfType<IEditableEmailTemplate>()
        .OrderBy(template => template.Key, StringComparer.Ordinal)
        .Select(template => new EmailTemplateDescriptor(
            template.Key, template.Languages, template.Placeholders, template.RequiredPlaceholders, template.HasAction, template.DefaultCopy))
        .ToList();

    public IReadOnlyList<EmailTemplateDescriptor> List() => _descriptors;

    public EmailTemplateDescriptor? Find(string templateKey) => _descriptors.FirstOrDefault(descriptor => descriptor.Key == templateKey);
}
