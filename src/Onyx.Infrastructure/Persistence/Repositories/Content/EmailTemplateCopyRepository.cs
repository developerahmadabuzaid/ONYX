using Microsoft.EntityFrameworkCore;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Application.Knowledge;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class EmailTemplateCopyRepository(ApplicationDbContext dbContext) : IEmailTemplateCopyRepository
{
    public Task<EmailTemplateCopy?> FindAsync(string templateKey, string languageCode, CancellationToken cancellationToken) =>
        dbContext.EmailTemplateCopies.SingleOrDefaultAsync(copy => copy.TemplateKey == templateKey && copy.LanguageCode == languageCode, cancellationToken);

    public async Task<IReadOnlyList<EmailTemplateCopy>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.EmailTemplateCopies.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(EmailTemplateCopy copy) => dbContext.EmailTemplateCopies.Add(copy);

    public void Remove(EmailTemplateCopy copy) => dbContext.EmailTemplateCopies.Remove(copy);

    public void SetExpectedVersion(EmailTemplateCopy copy, uint version) => dbContext.Entry(copy).Property(entry => entry.Version).OriginalValue = version;
}
