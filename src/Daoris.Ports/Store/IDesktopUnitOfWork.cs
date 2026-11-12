using System.Data.Common;

namespace Daoris.Ports.Store;

/// <summary>
/// The gate to the desktop's store: the application writes its SQL against the standard library's connection and transaction and
/// never names an engine's types. A read takes no write lock; a write is one transaction of a family that moves the family's
/// revision and wakes its readers only after it commits.
/// </summary>
public interface IDesktopUnitOfWork
{
    /// <summary>Runs <paramref name="read"/> over a connection that holds no write lock.</summary>
    ValueTask<T> ReadAsync<T>(Func<DbConnection, CancellationToken, ValueTask<T>> read, CancellationToken ct);

    /// <summary>
    /// Runs <paramref name="write"/> in one transaction of <paramref name="family"/>: committed when it returns, rolled back when it
    /// throws, and the readers woken once it has committed.
    /// </summary>
    ValueTask<T> WriteAsync<T>(string family, Func<DbTransaction, CancellationToken, ValueTask<T>> write, CancellationToken ct);
}
