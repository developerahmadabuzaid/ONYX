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

internal sealed class ContentPreviewTokenRepository(ApplicationDbContext dbContext) : IContentPreviewTokenRepository
{
    public void Add(ContentPreviewToken token) => dbContext.ContentPreviewTokens.Add(token);

    public Task<ContentPreviewToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.ContentPreviewTokens.AsNoTracking().SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
}
