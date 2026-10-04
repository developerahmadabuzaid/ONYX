namespace Onyx.Application.Email;

/// <summary>The admin-editable email templates (implemented by the infrastructure that owns the templates).</summary>
public interface IEmailTemplateCatalog
{
    IReadOnlyList<EmailTemplateDescriptor> List();

    EmailTemplateDescriptor? Find(string templateKey);
}
