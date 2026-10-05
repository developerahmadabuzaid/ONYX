using FluentValidation;

namespace Onyx.Application.Email;

public sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public const int AddressMaxLength = 254;
    public const int NameMaxLength = 200;
    public const int MaxModelEntries = 30;
    public const int ModelValueMaxLength = 4_000;

    public EmailRequestValidator()
    {
        RuleFor(request => request.TemplateKey).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LanguageCode).NotEmpty().Matches("^[a-z]{2}$");
        RuleFor(request => request.ToAddress).NotEmpty().MaximumLength(AddressMaxLength).EmailAddress();

        // Names end up in a header; line breaks would allow header injection.
        RuleFor(request => request.ToName)
            .MaximumLength(NameMaxLength)
            .Must(name => name is null || name.IndexOfAny(['\r', '\n']) < 0)
            .WithMessage("Recipient name must not contain line breaks.");

        RuleFor(request => request.Model)
            .NotNull()
            .Must(model => model.Count <= MaxModelEntries)
            .WithMessage($"At most {MaxModelEntries} template values are allowed.")
            .Must(model => model.Values.All(value => value.Length <= ModelValueMaxLength))
            .WithMessage("A template value is too long.");

        // Attachments are code-generated calendar files only; the allow-list keeps the outbox from carrying arbitrary payloads.
        RuleFor(request => request.Attachments)
            .Must(attachments => attachments is null || attachments.Count <= MaxAttachments)
            .WithMessage($"At most {MaxAttachments} attachments are allowed.");
        RuleForEach(request => request.Attachments).ChildRules(attachment =>
        {
            attachment.RuleFor(item => item.FileName).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$");
            attachment.RuleFor(item => item.ContentType).Must(AllowedAttachmentTypes.Contains).WithMessage("Attachment type is not allowed.");
            attachment.RuleFor(item => item.Content).NotEmpty().MaximumLength(AttachmentMaxLength);
        });
    }

    public const int MaxAttachments = 2;
    public const int AttachmentMaxLength = 32_000;

    public static IReadOnlySet<string> AllowedAttachmentTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "text/calendar" };
}
