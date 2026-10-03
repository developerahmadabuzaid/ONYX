using FluentValidation;
using Onyx.Application.Abstractions;
using Onyx.Application.Email;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Email;

/// <summary>Validates, renders and adds the email to the current unit of work. The caller's SaveChanges persists it atomically.</summary>
internal sealed class EmailOutbox(
    ApplicationDbContext dbContext,
    IEmailTemplateRenderer renderer,
    IValidator<EmailRequest> validator,
    IRequestContext requestContext,
    TimeProvider timeProvider) : IEmailOutbox
{
    public async Task<Result<Guid>> QueueAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray(), StringComparer.Ordinal);
            return Error.Validation("Email.Invalid", "The email request is invalid.", errors);
        }

        var rendered = renderer.Render(request.TemplateKey, request.LanguageCode, request.Model);
        if (rendered.IsFailure)
        {
            return rendered.Error!;
        }

        var message = EmailMessage.Create(
            timeProvider.GetUtcNow(),
            request.TemplateKey,
            request.LanguageCode,
            request.ToAddress.Trim(),
            request.ToName?.Trim(),
            rendered.Value.Subject,
            rendered.Value.HtmlBody,
            rendered.Value.TextBody,
            requestContext.CorrelationId);

        dbContext.EmailMessages.Add(message);
        return message.Id;
    }
}
