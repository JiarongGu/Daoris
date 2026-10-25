using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-i (D212, the design §13.4's GitDoor, §19.10): the store opened read-only. It makes no file, writes none, never waits on a
/// writer under write-ahead logging, and reads a store a writer holds open mid-transaction and one a checkpoint is moving: the cases the
/// git door's start is measured over (<c>GitDoorStartTests</c>), held here for what they must answer.
/// </summary>
/// <remarks>Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment.</remarks>
public sealed class DesktopStoreReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "desktop-store-reader", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<IDisposable> _open = [];

    public DesktopStoreReaderTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var each in _open) each.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_root);
    }

    private string Db => Path.Combine(_root, "data", "desktop.db");

    private DesktopStore Writer()
    {
        var store = DesktopStore.Open(Db, new DesktopStoreOptions { Kind = "shell", Settled = () => StoreSettled.Yes });
        _open.Add(store);
        return store;
    }

    private DesktopStoreReader Reader()
    {
        var reader = DesktopStoreReader.Open(Db) ?? throw new InvalidOperationException("no store to read");
        _open.Add(reader);
        return reader;
    }

    private SqliteConnection Raw()
    {
        var connection = new SqliteConnection($"Data Source={Db};Pooling=False");
        connection.Open();
        _open.Add(connection);
        return connection;
    }

    private static void Run(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [Fact]
    public void A_store_that_is_not_there_is_read_as_none_and_is_not_made()
    {
        Assert.Null(DesktopStoreReader.Open(Db));

        Assert.False(File.Exists(Db));
        Assert.False(Directory.Exists(Path.GetDirectoryName(Db)));
    }

    [Fact]
    public void A_store_is_read_for_its_version_stamp_and_revisions_and_the_rows_a_statement_names()
    {
        var writer = Writer();
        writer.Write("settings", write => write.Execute("INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('settings', 'a', 1, 'x', 't', 'b', 0, 'left')"));
        writer.Write("settings", _ => { });

        var reader = Reader();

        Assert.Equal((1, 1), (reader.Version, reader.Writer));
        Assert.Equal(new Dictionary<string, long> { ["settings"] = 2 }, reader.Revisions());
        Assert.Equal(["settings"], reader.Query("SELECT family FROM imports", row => row.GetString(0)));
    }

    [Fact]
    public void A_reader_writes_nothing_whatever_it_is_asked()
    {
        Writer().Write("settings", _ => { });
        var reader = Reader();
        var before = Revisions(reader);

        Assert.Throws<SqliteException>(() => reader.Query("INSERT INTO store_revisions (family, revision) VALUES ('x', 9)", row => 0));
        Assert.Throws<SqliteException>(() => reader.Query("CREATE TABLE made_by_a_reader (x TEXT)", row => 0));
        Assert.Throws<SqliteException>(() => reader.Query("UPDATE store_version SET writer = 99", row => 0));

        Assert.Equal(before, Revisions(reader));
        Assert.Equal(1, reader.Writer);
    }

    [Fact]
    public void Reading_changes_neither_the_database_file_nor_the_stamp()
    {
        var writer = Writer();
        writer.Write("settings", _ => { });
        writer.Dispose();   // the last connection closed checkpoints the log into the file
        var before = Hash(Db);

        using (var reader = DesktopStoreReader.Open(Db)!)
        {
            _ = reader.Revisions();
            _ = reader.Version;
        }

        Assert.Equal(before, Hash(Db));
    }

    [Fact]
    public void A_store_a_newer_build_stamped_is_read_as_it_stands()
    {
        using (var newer = DesktopStore.Open(Db, new DesktopStoreOptions
        {
            Writer = 2,
            Steps = [.. DesktopStore.Steps, new StoreMigration(2, 2, write => write.Execute("CREATE TABLE raised (x TEXT)"))],
            Settled = () => StoreSettled.Yes,
        }))
        {
            newer.Write("settings", _ => { });
        }

        var reader = Reader();

        Assert.Equal((2, 2), (reader.Version, reader.Writer));
        Assert.Equal(1, reader.Revisions()["settings"]);
    }

    [Fact]
    public void A_busy_writer_mid_transaction_is_no_wait_for_a_reader_which_reads_what_was_committed()
    {
        var writer = Writer();
        writer.Write("settings", _ => { });
        using var holder = Raw();
        Run(holder, "BEGIN IMMEDIATE");
        Run(holder, "INSERT INTO store_revisions (family, revision) VALUES ('uncommitted', 1)");

        var reader = Reader();
        var seen = Revisions(reader);

        Assert.Equal(new Dictionary<string, long> { ["settings"] = 1 }, seen);
        Run(holder, "COMMIT");
        Assert.Equal(2, Revisions(reader).Count);
    }

    [Fact]
    public void A_checkpoint_beside_a_reader_leaves_what_it_read_and_what_it_reads_next()
    {
        var writer = Writer();
        for (var each = 0; each < 50; each++) writer.Write("settings", _ => { });
        using var holder = Raw();

        var reader = Reader();
        Assert.Equal(50, Revisions(reader)["settings"]);

        // A checkpoint moves the log into the file while the reader is open, and the writer goes on after it.
        Run(holder, "PRAGMA wal_checkpoint(PASSIVE)");
        writer.Write("settings", _ => { });
        Run(holder, "PRAGMA wal_checkpoint(RESTART)");
        writer.Write("settings", _ => { });

        Assert.Equal(52, Revisions(reader)["settings"]);
    }

    private static IReadOnlyDictionary<string, long> Revisions(DesktopStoreReader reader) => reader.Revisions();

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
}
