using Onyx.Domain.Messaging;

namespace Onyx.Application.Email;

public interface IEmailTemplateCopyRepository
{
    Task<EmailTemplateCopy?> FindAsync(string templateKey, string languageCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailTemplateCopy>> ListAsync(CancellationToken cancellationToken);

    void Add(EmailTemplateCopy copy);

    void Remove(EmailTemplateCopy copy);

    void SetExpectedVersion(EmailTemplateCopy copy, uint version);
}
