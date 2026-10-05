using Onyx.Domain.Insights;

namespace Onyx.Application.Insights;

public interface IAuthorRepository
{
    Task<Author?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Author>> ListAsync(CancellationToken cancellationToken);

    /// <summary>True when any content item currently credits the author.</summary>
    Task<bool> IsCreditedAsync(Guid id, CancellationToken cancellationToken);

    void Add(Author author);

    void Remove(Author author);

    void SetExpectedVersion(Author author, uint version);
}
