namespace Onyx.Application.Abstractions.Persistence;

/// <summary>Commits all changes made through repositories and outbox ports in the current scope as one transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
