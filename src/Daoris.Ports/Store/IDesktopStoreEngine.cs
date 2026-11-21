using System.Data.Common;

namespace Daoris.Ports.Store;

/// <summary>The schema version and the oldest-writer stamp a store file holds; (0, 0) for a file nobody has written.</summary>
public readonly record struct StoreStamp(int Version, int Writer);

/// <summary>What a store is opened by an engine with: the writer stamp this process writes with, its clock, the build it records, and how long a write waits on the lock (<see cref="DefaultBusyWait"/> where null).</summary>
public sealed record DesktopStoreEngineOptions(int Writer, TimeProvider Clock, string Build, TimeSpan? BusyWait = null)
{
    public static TimeSpan DefaultBusyWait { get; } = TimeSpan.FromSeconds(5);
}

/// <summary>How a file is opened by <see cref="DesktopStoreEngines.Connect"/>.</summary>
public enum DesktopOpen
{
    /// <summary>Read and written, made where there is none.</summary>
    ReadWriteCreate,

    /// <summary>Read and written, never made.</summary>
    ReadWrite,

    /// <summary>Read alone: it writes nothing and waits on no writer for long.</summary>
    ReadOnly,
}

/// <summary>The write lock stayed held past the engine's wait: another process is writing, and nothing is written.</summary>
public sealed class StoreLockBusyException(DbException inner) : Exception("the store's write lock stayed held past the wait", inner);

/// <summary>The file's writer stamp is past this process's: it is read as it stands and written by no one here.</summary>
public sealed class StoreNewerException(int stamped, int own) : Exception($"the store's oldest writer {stamped} is past this build's {own}")
{
    /// <summary>The stamp the file holds.</summary>
    public int Stamped { get; } = stamped;

    /// <summary>The stamp this process writes with.</summary>
    public int Own { get; } = own;
}

/// <summary>A write reached a file that holds no schema yet: its first step has not run.</summary>
public sealed class StoreWithoutSchemaException() : Exception("the store holds no schema yet");

/// <summary>
/// The engine of one desktop store file: one connection behind one turn, every write one exclusive transaction that reads the writer
/// stamp under the lock, moves the family's revision and wakes its readers only after the commit. The application's schema steps and
/// its policy stay its own and are handed in: the engine knows the stamp, the revision and the lock, never a family.
/// </summary>
public interface IDesktopStoreEngine : IDesktopUnitOfWork, IDisposable
{
    /// <summary>The store's file.</summary>
    string Location { get; }

    /// <summary>The stamp the file holds now.</summary>
    StoreStamp Stamp();

    /// <summary>Runs <paramref name="read"/> over the connection in a snapshot of its own; it takes no write lock.</summary>
    T Read<T>(Func<DbConnection, T> read);

    /// <summary>
    /// One write transaction of <paramref name="family"/>: the lock taken, the stamp read under it and refused where it is past this
    /// process's, <paramref name="work"/>, the family's revision moved, the commit, then the readers woken. A null family moves no
    /// revision and wakes nothing. Rolled back where the work throws.
    /// </summary>
    /// <exception cref="StoreLockBusyException">The write lock stayed held past the wait.</exception>
    /// <exception cref="StoreNewerException">The file's stamp is past this process's.</exception>
    /// <exception cref="StoreWithoutSchemaException">The file holds no schema yet.</exception>
    T Write<T>(string? family, Func<DbConnection, DbTransaction, StoreStamp, T> work);

    /// <summary>
    /// One schema step in its own exclusive transaction, read again inside it: another process may have run it first. The stamp the step
    /// leaves is never lower than the one the file holds. False where the step has already run or the file is stamped past this process.
    /// </summary>
    bool Apply(int version, int writer, Action<DbConnection, DbTransaction> step);
}

/// <summary>
/// What a root hands for the desktop store: how a store file is opened as an engine, and how any file of the home is connected to for
/// a one-off read. The application names no engine's type: it asks these.
/// </summary>
/// <param name="Open">The store at a location, made if there is none; its folder is made, and no schema step has run.</param>
/// <param name="Connect">An opened connection to a file, with none of the store's own pragmas.</param>
/// <param name="ReleaseConnections">Lets go every pooled connection, so a file a reader has finished with can be moved.</param>
public sealed record DesktopStoreEngines(
    Func<string, DesktopStoreEngineOptions, IDesktopStoreEngine> Open,
    Func<string, DesktopOpen, DbConnection> Connect,
    Action ReleaseConnections)
{
    /// <summary>The pages a connection of <see cref="Connect"/> put on disk since the last ask, which starts the count again: under write-ahead
    /// logging the log's frames, a checkpoint's copy into the file aside; null where the engine cannot say.</summary>
    public Func<DbConnection, long?> PagesWritten { get; init; } = _ => null;

    /// <summary>Whether a failure of a connection of <see cref="Connect"/> is the file's lock held past the wait, which a later try may pass.</summary>
    public Func<Exception, bool> Busy { get; init; } = error => error is StoreLockBusyException;
}
