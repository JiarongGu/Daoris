using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Daoris.Knowledge;

/// <summary>
/// One unit of work at a time on a connection: every command a store runs, not only every transaction (SQLITETX1).
/// </summary>
/// <remarks>
/// <para>A host holds ONE connection for all its stores and answers requests at once, and a
/// <see cref="SqliteConnection"/> is not safe to use from two threads: the transaction it has open and the list of
/// commands it made are plain fields. Gating only the transactions (SYNC1) left every other command free to run beside
/// one. On the install that answered a say 500 with <i>Execute requires the command to have a transaction object</i> (a
/// command made before another request's <c>BEGIN</c> and run after it), and it left the database inside a transaction
/// no object owned (the corrupted list threw after <c>BEGIN</c> had run), so every later <c>BEGIN</c> failed until the
/// host restarted (KNOW500), and every write in between was never committed.</para>
///
/// <para>So a store reaches the connection only inside <see cref="RunAsync{T}"/>: a <see cref="Command"/> made outside
/// it throws. The gate is reentrant within one flow of work, because a transaction's work calls the stores' own
/// methods (the ledger's check and write inside <c>SessionStore.ExclusiveAsync</c>), and a command made there joins the
/// transaction open on the connection, which can only be that flow's own.</para>
///
/// <para>Keyed by the connection, not the store, because two stores over one connection collide as surely as two calls
/// on one store. Across processes the file's own locks serialize them, which is what <c>BEGIN IMMEDIATE</c> is for.</para>
/// </remarks>
internal sealed class ConnectionGate
{
    private static readonly ConditionalWeakTable<SqliteConnection, ConnectionGate> Gates = new();

    /// <summary>The gates this flow of work holds, innermost first.</summary>
    private static readonly AsyncLocal<Hold?> Holding = new();

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _turn = new(1, 1);

    private ConnectionGate(SqliteConnection connection) => _connection = connection;

    public static ConnectionGate For(SqliteConnection connection) =>
        Gates.GetValue(connection, opened => new ConnectionGate(opened));

    /// <summary>Whether this flow of work holds the gate, so a command it makes is its own.</summary>
    public bool Held => HeldHere() is not null;

    /// <summary>A command on the connection, for the flow that holds the gate; it joins the transaction open there.</summary>
    /// <exception cref="InvalidOperationException">Made outside <see cref="RunAsync{T}"/>.</exception>
    public SqliteCommand Command() =>
        Held
            ? _connection.CreateCommand()
            : throw new InvalidOperationException(
                "A command was made outside the connection's gate, where it can meet another request's open transaction "
                + "(SQLITETX1). Run it inside the gate.");

    public Task RunAsync(Func<Task> work, CancellationToken ct) =>
        RunAsync(async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }, ct);

    /// <summary>
    /// Run <paramref name="work"/> with the connection to itself: after every other request's work ends, and before the
    /// next begins. Within work that already holds it, it runs at once.
    /// </summary>
    /// <remarks>
    /// The wait honours <paramref name="ct"/>; the work is its own. A unit that began on a clean connection ends on one:
    /// a transaction it left open, by an exception or without one, is rolled back before the next unit begins, and one it
    /// left without throwing fails it, because nothing it wrote there was kept. A transaction a caller holds outside
    /// every gate (another host's, in a test) is joined, as a statement made while one is open always joins it.
    /// </remarks>
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        if (Held) return await work().ConfigureAwait(false);

        await _turn.WaitAsync(ct).ConfigureAwait(false);
        var hold = new Hold(this, Holding.Value);
        Holding.Value = hold;
        try
        {
            var outside = OpenTransaction();
            if (outside is null) RollBackOrphan();

            T result;
            try
            {
                result = await work().ConfigureAwait(false);
            }
            catch
            {
                if (outside is null) RollBackQuietly();
                throw;
            }

            if (outside is null && RollBackLeft())
            {
                throw new InvalidOperationException(
                    "A unit of work ended with a transaction still open on the connection. It was rolled back, so "
                    + "nothing written in it was kept (SQLITETX1).");
            }

            return result;
        }
        finally
        {
            hold.Live = false;
            Holding.Value = hold.Outer;
            _turn.Release();
        }
    }

    /// <summary>
    /// <paramref name="work"/> as one transaction, <c>BEGIN IMMEDIATE</c>, inside the gate: committed when the work
    /// returns, a refusal that wrote nothing included, and rolled back when it throws.
    /// </summary>
    /// <remarks>
    /// <para>The wait honours the caller; the transaction does not. Once <c>BEGIN</c> has run, a cancelled request (a
    /// client that went away) must not throw halfway, so the work is handed a token nobody cancels (REV3).</para>
    ///
    /// <para>Asked for inside work that already holds one, the work is part of that transaction: SQLite does not nest
    /// them, and the outer one commits or rolls back for both.</para>
    /// </remarks>
    public Task<T> InTransactionAsync<T>(Func<SqliteTransaction, CancellationToken, Task<T>> work, CancellationToken ct) =>
        RunAsync(async () =>
        {
            var hold = HeldHere()!;
            if (hold.Transaction is { } open) return await work(open, CancellationToken.None).ConfigureAwait(false);

            await using var transaction = _connection.BeginTransaction(deferred: false);
            hold.Transaction = transaction;
            try
            {
                var result = await work(transaction, CancellationToken.None).ConfigureAwait(false);
                await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                return result;
            }
            finally
            {
                hold.Transaction = null;
            }
        }, ct);

    private Hold? HeldHere()
    {
        for (var hold = Holding.Value; hold is not null; hold = hold.Outer)
        {
            if (hold.Live && ReferenceEquals(hold.Gate, this)) return hold;
        }

        return null;
    }

    /// <summary>The transaction object the connection has open: a new command is handed it.</summary>
    private SqliteTransaction? OpenTransaction()
    {
        if (_connection.State != ConnectionState.Open) return null;
        using var probe = _connection.CreateCommand();
        return probe.Transaction;
    }

    /// <summary>Whether the database itself is inside a transaction, whatever object does or does not own it.</summary>
    private bool InTransaction() =>
        _connection.State == ConnectionState.Open && raw.sqlite3_get_autocommit(_connection.Handle) == 0;

    /// <summary>
    /// A transaction the database holds and no object owns: nothing can commit it, so it can only have been left behind,
    /// and every <c>BEGIN</c> after it fails.
    /// </summary>
    private void RollBackOrphan()
    {
        if (OpenTransaction() is null && InTransaction()) RollBack();
    }

    /// <summary>Roll back whatever a unit of work left open; whether there was anything.</summary>
    private bool RollBackLeft()
    {
        var left = false;
        if (OpenTransaction() is { } transaction)
        {
            left = true;
            try
            {
                // Disposing an unfinished transaction rolls it back and lets the connection forget it.
                transaction.Dispose();
            }
            catch (SqliteException)
            {
                // The database's own state, read next, says whether it is still open.
            }
        }

        if (InTransaction())
        {
            left = true;
            RollBack();
        }

        return left;
    }

    /// <summary>On a unit's way out with its own exception, which is the one to answer with.</summary>
    private void RollBackQuietly()
    {
        try
        {
            RollBackLeft();
        }
        catch (Exception)
        {
            // The next unit's entry rolls back an orphan again.
        }
    }

    private void RollBack()
    {
        using var rollback = _connection.CreateCommand();
        rollback.CommandText = "ROLLBACK";
        rollback.ExecuteNonQuery();
    }

    /// <summary>One flow's hold on one gate, and the transaction it has open there.</summary>
    private sealed class Hold(ConnectionGate gate, Hold? outer)
    {
        public ConnectionGate Gate { get; } = gate;

        public Hold? Outer { get; } = outer;

        /// <summary>False once released: a task that captured this flow and outlived its hold holds nothing.</summary>
        public bool Live { get; set; } = true;

        public SqliteTransaction? Transaction { get; set; }
    }
}
