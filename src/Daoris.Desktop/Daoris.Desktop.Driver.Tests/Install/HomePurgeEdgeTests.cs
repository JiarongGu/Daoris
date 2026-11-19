using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// `daoris home purge --history` at its edges: a store another process holds, a home that is no install's, a path with spaces, a second
/// purge and one with nothing to purge, every kind of holder, a session still running, a store the environment moved, the quests a purge
/// forgets, and the lines between history and configuration that a restart depends on. <c>HomePurgeTests</c> holds the common path.
/// </summary>
public sealed class HomePurgeEdgeTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 30, 5, TimeSpan.Zero);

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris purge edge " + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "plain home");

    private string Folder => DaorisHome.FolderOf(Home);

    private string Knowledge => DaorisHome.Entry(Home, HomeEntry.Knowledge);

    private string Desktop => DaorisHome.Entry(Home, HomeEntry.DesktopDb);

    public HomePurgeEdgeTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 A store another process holds for writing stops the purge before either store or any file changes, and says which.</summary>
    [Fact]
    public void A_store_another_process_holds_stops_the_purge_before_anything_changes()
    {
        Seed();
        using var holder = Open(Knowledge);
        Sql(holder, "BEGIN IMMEDIATE");

        var (code, said) = Run("purge", "--history", "--yes");

        Assert.Equal(1, code);
        Assert.Contains("knowledge.db", said);
        Assert.Contains("held by another process", said);
        Assert.Contains("nothing was changed", said);
        Assert.Equal(1, Rows(Desktop, "news_said"));
        Assert.Equal(2, Rows(Knowledge, "sessions"));
        Assert.True(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));
        Assert.False(Directory.Exists(Path.Combine(Folder, HomePurge.BackupFolder)));
    }

    /// <summary>🔴 A home that is no install's keeps its backup in its own <c>purged/</c>, which a later plan never lists as history.</summary>
    [Fact]
    public void A_home_that_is_no_installs_keeps_the_backup_in_its_own_purged_folder()
    {
        Seed();

        var (code, said) = Run("purge", "--history", "--yes");

        Assert.True(code == 0, said);
        var purged = Path.Combine(Folder, HomePurge.BackupFolder, "20261119T083005Z");
        Assert.True(File.Exists(Path.Combine(purged, "data", "sessions", "s-1", "events.jsonl")));
        Assert.True(File.Exists(Path.Combine(purged, "data", "knowledge.db")));
        Assert.True(File.Exists(Path.Combine(purged, HomePurge.SummaryFile)));
        Assert.Empty(HomePurge.Plan(Home).Paths);
        Assert.Equal(0, Rows(Knowledge, "sessions"));
        Assert.Equal(2, Rows(Path.Combine(purged, "data", "knowledge.db"), "sessions"));
    }

    /// <summary>🔴 A second <c>--yes</c>, and one on a home with nothing in it, say there is nothing to do and make no folder and no copy.</summary>
    [Fact]
    public void A_purge_with_nothing_to_purge_says_so_and_changes_nothing()
    {
        Seed();
        Assert.Equal(0, Run("purge", "--history", "--yes").Code);
        var folders = Directory.GetDirectories(Path.Combine(Folder, HomePurge.BackupFolder));

        var code = HomePurgeCommand.Run(["purge", "--history", "--yes"], Home, () => Now.AddMinutes(1), null, null, out var said);

        Assert.Equal(0, code);
        Assert.Contains("nothing to purge", said);
        Assert.Equal(folders, Directory.GetDirectories(Path.Combine(Folder, HomePurge.BackupFolder)));

        var empty = Path.Combine(_root, "empty home");
        Directory.CreateDirectory(empty);
        Assert.Equal(0, HomePurgeCommand.Run(["purge", "--history", "--yes"], empty, () => Now, null, null, out var none));
        Assert.Contains("nothing to purge", none);
        Assert.False(Directory.Exists(Path.Combine(empty, HomePurge.BackupFolder)));
    }

    /// <summary>🔴 Whatever holds the home, an instance's lease or a verb's, refuses the purge and nothing moves.</summary>
    [Theory]
    [InlineData(LeaseKind.Headless)]
    [InlineData(LeaseKind.Verb)]
    public void Any_holder_of_the_home_refuses_the_purge(LeaseKind kind)
    {
        Seed();
        using var held = HomeLease.TryAcquire(Home, kind, out _, kind == LeaseKind.Verb ? "agent update" : null);
        Assert.NotNull(held);

        var (code, said) = Run("purge", "--history", "--yes");

        Assert.Equal(1, code);
        Assert.Contains("Close Daoris", said);
        Assert.Contains("nothing was moved", said);
        Assert.Equal(2, Rows(Knowledge, "sessions"));
        Assert.True(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));
    }

    /// <summary>🔴 The lease the host took for this very verb, before its door ran, is the purge's own and refuses nothing.</summary>
    [Fact]
    public void The_lease_the_host_took_for_the_purge_is_its_own()
    {
        Seed();
        using var host = HomeLease.ForVerb(Home, "daoris home purge", out _);
        Assert.NotNull(host);

        var (code, said) = Run("purge", "--history", "--yes");

        Assert.Equal(0, code);
        Assert.Contains("the configuration stays", said);
        Assert.Equal(0, Rows(Knowledge, "sessions"));
    }

    /// <summary>🔴 A session whose process still runs holds the home, though no lease says so: its connector writes the store.</summary>
    [Fact]
    public void A_running_session_refuses_the_purge()
    {
        Seed();
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        var marker = Path.Combine(DaorisHome.Entry(Home, HomeEntry.SessionPids), "a1b2c3d4.pid");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        File.WriteAllText(marker, $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");

        var (code, said) = Run("purge", "--history", "--yes");

        Assert.Equal(1, code);
        Assert.Contains("session", said);
        Assert.Contains("nothing was moved", said);
        Assert.Equal(2, Rows(Knowledge, "sessions"));
    }

    /// <summary>🔴 A knowledge store the environment moved is the one the purge reads, copies and empties; the home's own is not touched.</summary>
    [Fact]
    public void A_knowledge_store_the_environment_moved_is_the_one_purged()
    {
        Seed();
        var wired = Path.Combine(_root, "elsewhere", "index.db");
        Directory.CreateDirectory(Path.GetDirectoryName(wired)!);
        File.Copy(Knowledge, wired);
        Sql(wired, "DELETE FROM sessions WHERE id = 's-2'");

        var plan = HomePurge.Plan(Home, wired);
        Assert.Contains(plan.Tables, table => table is { Table: "sessions", Rows: 1 });

        var code = HomePurgeCommand.Run(["purge", "--history", "--yes"], Home, () => Now, null, null, wired, out var said);

        Assert.True(code == 0, said);
        Assert.Equal(0, Rows(wired, "sessions"));
        Assert.Equal(2, Rows(Knowledge, "sessions"));
        Assert.Equal(1, Rows(Path.Combine(Folder, HomePurge.BackupFolder, "20261119T083005Z", "index.db"), "sessions"));
    }

    /// <summary>
    /// 🔴 Every quest purged is forgotten, so a remote's operations on it make no half quest; a remote the person retired stays retired; an
    /// operation still pending or applying stays; one that finished goes.
    /// </summary>
    [Fact]
    public void The_purged_quests_are_forgotten_and_the_operations_still_open_are_kept()
    {
        Seed();

        Assert.Equal(0, Run("purge", "--history", "--yes").Code);

        Assert.Equal(["q-1", "q-2", "q-3"], Column(Knowledge, "SELECT id FROM quest_forgotten ORDER BY id"));
        Assert.Equal(["remote-a"], Column(Knowledge, "SELECT machine FROM quest_retired"));
        Assert.Equal(0, Rows(Knowledge, "quests"));
        Assert.Equal(0, Rows(Knowledge, "quest_log"));
        Assert.Equal(["m-applying", "m-awaiting", "m-staged"], Column(Knowledge, "SELECT id FROM memory_ops ORDER BY id"));
    }

    /// <summary>
    /// 🔴 What a restart settles stays: the place receipts, and the branches and landings of trees that are kept; what belongs with
    /// the pipe's waiting presses goes with it; and the requests and pid markers, run state, stay while the sessions folder moves.
    /// </summary>
    [Fact]
    public void What_a_restart_settles_stays_and_the_sent_presses_go_with_the_waiting_ones()
    {
        Seed();
        var kept = new[]
        {
            DaorisHome.Entry(Home, HomeEntry.PlaceReceipts, "digest.json"), DaorisHome.Entry(Home, HomeEntry.Landings),
            DaorisHome.Entry(Home, HomeEntry.SessionBranches),
            DaorisHome.Entry(Home, HomeEntry.Requests, "r-1.json"), Path.Combine(Folder, "run", "sessions", "s-1.pid"),
        };
        foreach (var path in kept) Write(path, "{}");
        Write(DaorisHome.Entry(Home, HomeEntry.PipeSent, "p-1.json"), "{}");
        Write(DaorisHome.Entry(Home, HomeEntry.PipeWaiting, "p-2.json"), "{}");
        Write(DaorisHome.Entry(Home, HomeEntry.Sessions, "s-3", "events.jsonl"), "{}\n");

        Assert.Equal(0, Run("purge", "--history", "--yes").Code);

        foreach (var path in kept) Assert.True(File.Exists(path), path);
        var purged = Path.Combine(Folder, HomePurge.BackupFolder, "20261119T083005Z");
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.PipeSent, "p-1.json")));
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.PipeWaiting, "p-2.json")));
        Assert.True(File.Exists(Path.Combine(purged, "profiles", "pipe-sent", "p-1.json")));
        Assert.True(File.Exists(Path.Combine(purged, "data", "sessions", "s-3", "events.jsonl")));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions, "s-3")));
    }

    private (int Code, string Said) Run(params string[] args)
    {
        var code = HomePurgeCommand.Run(args, Home, () => Now, null, null, out var said);
        return (code, said);
    }

    /// <summary>A bare home with history and configuration: files, a desktop store and a stand-in of the service's.</summary>
    private void Seed()
    {
        Write(DaorisHome.Entry(Home, HomeEntry.Sessions, "s-1", "events.jsonl"), "{}\n");
        Write(DaorisHome.Entry(Home, HomeEntry.Sessions, "s-2", "events.jsonl"), "{}\n");
        Write(DaorisHome.Entry(Home, HomeEntry.DriverConfig), "{\"kept\":true}");
        using (DesktopStore.Open(Desktop, new DesktopStoreOptions { Settled = () => StoreSettled.Yes })) { }
        Sql(Desktop, "INSERT INTO news_said (key, at) VALUES ('told', '2026-11-01')");
        Sql(Knowledge,
            "CREATE TABLE sessions (id TEXT); INSERT INTO sessions VALUES ('s-1'), ('s-2');"
            + "CREATE TABLE event_journal (position INTEGER PRIMARY KEY AUTOINCREMENT, record TEXT); INSERT INTO event_journal (record) VALUES ('s-1');"
            + "CREATE TABLE quests (id TEXT); INSERT INTO quests VALUES ('q-1'), ('q-2');"
            + "CREATE TABLE quest_log (quest TEXT); INSERT INTO quest_log VALUES ('q-2'), ('q-3');"
            + "CREATE TABLE quest_forgotten (id TEXT PRIMARY KEY, at TEXT NOT NULL);"
            + "CREATE TABLE quest_retired (machine TEXT PRIMARY KEY, at TEXT NOT NULL); INSERT INTO quest_retired VALUES ('remote-a', '2026-11-01');"
            + "CREATE TABLE memory_ops (id TEXT PRIMARY KEY, state TEXT NOT NULL);"
            + "INSERT INTO memory_ops VALUES ('m-staged', 'staged'), ('m-awaiting', 'awaiting'), ('m-applying', 'applying'), ('m-done', 'applied'), ('m-gone', 'dropped');");
        SqliteConnection.ClearAllPools();
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static SqliteConnection Open(string store)
    {
        var connection = new SqliteConnection($"Data Source={store};Pooling=False");
        connection.Open();
        return connection;
    }

    private static void Sql(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void Sql(string store, string sql)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(store)!);
        using var connection = Open(store);
        Sql(connection, sql);
    }

    private static IReadOnlyList<string> Column(string store, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={store};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read()) values.Add(reader.GetString(0));
        return values;
    }

    private static long Rows(string store, string table) => long.Parse(Column(store, $"SELECT COUNT(*) FROM {table}")[0], System.Globalization.CultureInfo.InvariantCulture);
}
