using System.Data.Common;
using Daoris.Ports.Store;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every <see cref="IDesktopStoreEngine"/> must pass, written once against the port: a write that returns is committed and
/// one that throws is rolled back, each write moves its family's revision, the readers are woken only after the commit, the writer
/// stamp is read under the lock and a stamp past this process's refuses the write, and a second writer waits on the lock and is then
/// refused with nothing written. The in-memory fake and the SQLite adapter each run them, so a fake that drifts fails at the adapter.
/// </summary>
public abstract class UnitOfWorkContract : IDisposable
{
    private readonly List<IDesktopStoreEngine> _opened = [];

    /// <summary>A place for a store named <paramref name="name"/>, one the case owns.</summary>
    protected abstract string Place(string name);

    /// <summary>The engine at <paramref name="place"/>.</summary>
    protected abstract IDesktopStoreEngine Open(string place, DesktopStoreEngineOptions options);

    /// <summary>Whether the readers of the store at <paramref name="place"/> have been woken since it is made.</summary>
    protected abstract bool Woken(string place);

    /// <summary>Lets go what the place held.</summary>
    protected virtual void Release(string place)
    {
    }

    private readonly List<string> _places = [];

    private string Fresh()
    {
        var place = Place(Guid.NewGuid().ToString("N"));
        _places.Add(place);
        return place;
    }

    private IDesktopStoreEngine Engine(string place, int writer = 1, TimeSpan? busy = null)
    {
        var engine = Open(place, new DesktopStoreEngineOptions(writer, new MovedClock(), "contract", busy ?? TimeSpan.FromMilliseconds(250)));
        _opened.Add(engine);
        return engine;
    }

    private static void Run(DbConnection connection, DbTransaction? transaction, string sql, params (string Name, object Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        command.ExecuteNonQuery();
    }

    private static long Count(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar() ?? 0L);
    }

    /// <summary>The first step the application hands an engine: the engine's own tables and one of a family's.</summary>
    private static void Schema(DbConnection connection, DbTransaction transaction)
    {
        Run(connection, transaction, "CREATE TABLE store_version (one INTEGER PRIMARY KEY CHECK (one = 1), version INTEGER NOT NULL, writer INTEGER NOT NULL, build TEXT NOT NULL, at TEXT NOT NULL)");
        Run(connection, transaction, "CREATE TABLE store_revisions (family TEXT PRIMARY KEY, revision INTEGER NOT NULL)");
        Run(connection, transaction, "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
    }

    private IDesktopStoreEngine Stored(string place, int writer = 1, TimeSpan? busy = null)
    {
        var engine = Engine(place, writer, busy);
        Assert.True(engine.Apply(1, 1, Schema));
        return engine;
    }

    [Fact]
    public void A_file_nobody_wrote_has_no_stamp_and_a_step_gives_it_one_that_is_never_lowered()
    {
        var engine = Engine(Fresh(), writer: 2);

        Assert.Equal(new StoreStamp(0, 0), engine.Stamp());
        Assert.True(engine.Apply(1, 2, Schema));
        Assert.Equal(new StoreStamp(1, 2), engine.Stamp());
        Assert.False(engine.Apply(1, 2, (_, _) => throw new InvalidOperationException("a step already run is not run again")));
        Assert.True(engine.Apply(2, 1, (connection, transaction) => Run(connection, transaction, "CREATE TABLE more (id INTEGER)")));
        Assert.Equal(new StoreStamp(2, 2), engine.Stamp());
    }

    [Fact]
    public void A_write_that_returns_is_committed_and_a_read_sees_it()
    {
        var engine = Stored(Fresh());

        var stamp = engine.Write("items", (connection, transaction, held) =>
        {
            Run(connection, transaction, "INSERT INTO items (name) VALUES ($name)", ("$name", "kept"));
            return held;
        });

        Assert.Equal(new StoreStamp(1, 1), stamp);
        Assert.Equal(1, engine.Read(connection => Count(connection, "SELECT COUNT(*) FROM items WHERE name = 'kept'")));
    }

    [Fact]
    public void A_write_that_throws_is_rolled_back_and_moves_no_revision()
    {
        var engine = Stored(Fresh());

        Assert.Throws<InvalidOperationException>(() => engine.Write<int>("items", (connection, transaction, _) =>
        {
            Run(connection, transaction, "INSERT INTO items (name) VALUES ('lost')");
            throw new InvalidOperationException("the work failed");
        }));

        Assert.Equal(0, engine.Read(connection => Count(connection, "SELECT COUNT(*) FROM items")));
        Assert.Equal(0, engine.Read(connection => Count(connection, "SELECT COUNT(*) FROM store_revisions")));
    }

    [Fact]
    public void Each_write_moves_its_familys_revision_by_one_and_a_write_of_no_family_moves_none()
    {
        var engine = Stored(Fresh());

        engine.Write("items", (_, _, _) => 0);
        engine.Write("items", (_, _, _) => 0);
        engine.Write(null, (_, _, _) => 0);

        Assert.Equal(2, engine.Read(connection => Count(connection, "SELECT revision FROM store_revisions WHERE family = 'items'")));
        Assert.Equal(1, engine.Read(connection => Count(connection, "SELECT COUNT(*) FROM store_revisions")));
    }

    [Fact]
    public void The_readers_are_woken_after_the_commit_and_never_during_the_work()
    {
        var place = Fresh();
        var engine = Stored(place);
        Assert.False(Woken(place));

        var duringWork = true;
        engine.Write("items", (_, _, _) => duringWork = Woken(place));

        Assert.False(duringWork);
        Assert.True(Woken(place));
    }

    [Fact]
    public void A_write_past_the_stamp_this_process_writes_with_is_refused_and_writes_nothing()
    {
        var place = Fresh();
        var older = Stored(place, writer: 1);
        var newer = Engine(place, writer: 5);
        Assert.True(newer.Apply(2, 5, (connection, transaction) => Run(connection, transaction, "CREATE TABLE later (id INTEGER)")));

        var refused = Assert.Throws<StoreNewerException>(() => older.Write("items", (connection, transaction, _) =>
        {
            Run(connection, transaction, "INSERT INTO items (name) VALUES ('refused')");
            return 0;
        }));

        Assert.Equal(5, refused.Stamped);
        Assert.Equal(1, refused.Own);
        Assert.Equal(0, newer.Read(connection => Count(connection, "SELECT COUNT(*) FROM items")));
    }

    [Fact]
    public void A_write_before_the_first_step_is_refused_for_want_of_a_schema()
    {
        var engine = Engine(Fresh());

        Assert.Throws<StoreWithoutSchemaException>(() => engine.Write("items", (_, _, _) => 0));
    }

    [Fact]
    public void A_second_writer_waits_on_the_lock_and_is_refused_with_nothing_written_while_the_first_holds_it()
    {
        var place = Fresh();
        var first = Stored(place);
        var second = Engine(place);
        using var inside = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holding = Task.Run(() => first.Write("items", (connection, transaction, _) =>
        {
            Run(connection, transaction, "INSERT INTO items (name) VALUES ('first')");
            inside.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return 0;
        }));
        Assert.True(inside.Wait(TimeSpan.FromSeconds(10)));

        try
        {
            Assert.Throws<StoreLockBusyException>(() => second.Write("items", (connection, transaction, _) =>
            {
                Run(connection, transaction, "INSERT INTO items (name) VALUES ('second')");
                return 0;
            }));
        }
        finally
        {
            release.Set();
            holding.GetAwaiter().GetResult();
        }

        Assert.Equal(1, second.Read(connection => Count(connection, "SELECT COUNT(*) FROM items")));
        Assert.Equal(0, second.Read(connection => Count(connection, "SELECT COUNT(*) FROM items WHERE name = 'second'")));
    }

    [Fact]
    public async Task The_asynchronous_gate_commits_moves_the_revision_and_reads_what_it_wrote()
    {
        var engine = Stored(Fresh());

        var written = await engine.WriteAsync("items", async (transaction, ct) =>
        {
            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO items (name) VALUES ('async')";
            return await command.ExecuteNonQueryAsync(ct);
        }, CancellationToken.None);
        var seen = await engine.ReadAsync(async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM items WHERE name = 'async'";
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        }, CancellationToken.None);

        Assert.Equal(1, written);
        Assert.Equal(1, seen);
        Assert.Equal(1, engine.Read(connection => Count(connection, "SELECT revision FROM store_revisions WHERE family = 'items'")));
    }

    /// <summary>Lets go every engine the cases opened and what each place held.</summary>
    public void Dispose()
    {
        foreach (var engine in _opened) engine.Dispose();
        foreach (var place in _places) Release(place);
        GC.SuppressFinalize(this);
    }
}
