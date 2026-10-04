using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public interface IContentPreviewTokenRepository
{
    void Add(ContentPreviewToken token);

    Task<ContentPreviewToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);
}
