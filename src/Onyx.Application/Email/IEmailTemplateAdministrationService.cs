using Onyx.Contracts.Messaging;
using Onyx.Domain.Common;

namespace Onyx.Application.Email;

public interface IEmailTemplateAdministrationService
{
    Task<IReadOnlyList<EmailTemplateResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<EmailTemplateResponse>> SaveCopyAsync(string templateKey, string languageCode, SaveEmailTemplateCopyRequest request, CancellationToken cancellationToken);

    /// <summary>Removes the override so the code default is used again.</summary>
    Task<Result<EmailTemplateResponse>> ResetCopyAsync(string templateKey, string languageCode, CancellationToken cancellationToken);
}
