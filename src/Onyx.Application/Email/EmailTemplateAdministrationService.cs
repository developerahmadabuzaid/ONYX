using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Contracts.Messaging;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Messaging;

namespace Onyx.Application.Email;

/// <summary>
/// Admin-editable email wording per language. Only text changes: layout, encoding and links (including reset and invitation
/// URLs) stay code-owned, so editing copy can never weaken authentication flows.
/// </summary>
public sealed class EmailTemplateAdministrationService(
    IEmailTemplateCatalog catalog,
    IEmailTemplateCopyRepository copies,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork) : IEmailTemplateAdministrationService
{
    private static readonly Error NotFound = Error.NotFound("EmailTemplate.NotFound", "Email template not found.");

    public async Task<IReadOnlyList<EmailTemplateResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var overrides = await copies.ListAsync(cancellationToken).ConfigureAwait(false);
        return catalog.List().Select(template => ToResponse(template, overrides)).ToList();
    }

    public async Task<Result<EmailTemplateResponse>> SaveCopyAsync(
        string templateKey, string languageCode, SaveEmailTemplateCopyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var template = catalog.Find(templateKey);
        if (template is null || !template.Languages.Contains(languageCode))
        {
            return NotFound;
        }

        var copy = new EmailCopy(request.Subject ?? string.Empty, request.Heading ?? string.Empty, request.Paragraphs ?? [], request.ActionLabel, request.Footer);
        if (EmailCopyRules.Validate(copy, template) is { } invalid)
        {
            return invalid;
        }

        var existing = await copies.FindAsync(templateKey, languageCode, cancellationToken).ConfigureAwait(false);
        var entity = existing ?? EmailTemplateCopy.Create(templateKey, languageCode);
        if (existing is not null && request.Version is { } version)
        {
            copies.SetExpectedVersion(existing, version);
        }

        var updated = entity.Update(copy.Subject, copy.Heading, copy.Paragraphs, copy.ActionLabel, copy.Footer);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        if (existing is null)
        {
            copies.Add(entity);
        }

        auditTrail.Record(AuditAction.EmailTemplateCopyChanged, AuditCategory.System, nameof(EmailTemplateCopy), $"{templateKey}:{languageCode}",
            new { template = templateKey, language = languageCode });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(template, await copies.ListAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<EmailTemplateResponse>> ResetCopyAsync(string templateKey, string languageCode, CancellationToken cancellationToken)
    {
        var template = catalog.Find(templateKey);
        if (template is null || !template.Languages.Contains(languageCode))
        {
            return NotFound;
        }

        var existing = await copies.FindAsync(templateKey, languageCode, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            copies.Remove(existing);
            auditTrail.Record(AuditAction.EmailTemplateCopyReset, AuditCategory.System, nameof(EmailTemplateCopy), $"{templateKey}:{languageCode}",
                new { template = templateKey, language = languageCode });
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ToResponse(template, await copies.ListAsync(cancellationToken).ConfigureAwait(false));
    }

    private static EmailTemplateResponse ToResponse(EmailTemplateDescriptor template, IReadOnlyList<EmailTemplateCopy> overrides) => new(
        template.Key,
        template.Placeholders.ToList(),
        template.RequiredPlaceholders.ToList(),
        template.HasAction,
        template.Languages.Select(language =>
        {
            var custom = overrides.FirstOrDefault(copy => copy.TemplateKey == template.Key && copy.LanguageCode == language);
            if (custom is not null)
            {
                return new EmailTemplateCopyResponse(language, true, custom.Subject, custom.Heading, custom.Paragraphs, custom.ActionLabel, custom.Footer, custom.Version);
            }

            var defaults = template.DefaultCopy(language);
            return new EmailTemplateCopyResponse(language, false, defaults.Subject, defaults.Heading, defaults.Paragraphs, defaults.ActionLabel, defaults.Footer, null);
        }).ToList());
}
