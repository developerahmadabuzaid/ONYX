using Onyx.Domain.Common;

namespace Onyx.Application.Abstractions.Persistence;

/// <summary>Commits all changes made through repositories and outbox ports in the current scope as one transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction, for operations that combine row locks or set-based statements
    /// with tracked changes. Commits only when the work succeeds; a failure result or an exception rolls everything back.
    /// </summary>
    Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken);
}
