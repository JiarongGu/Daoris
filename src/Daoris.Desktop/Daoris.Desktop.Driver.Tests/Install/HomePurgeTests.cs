using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// `daoris home purge --history`: which of a home's entries and store tables are history, the plan that lists them and touches
/// nothing, the purge that moves them beside the install and empties the tables after copying the stores, and its refusal while Daoris runs.
/// The service's own store is a stand-in here with the tables' names; <c>HomePurgeProcessTests</c> purges the real one and starts it again.
/// </summary>
public sealed class HomePurgeTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 30, 5, TimeSpan.Zero);

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-purge-" + Guid.NewGuid().ToString("N")[..8]);

    private string Install => Path.Combine(_root, "install");

    private string Home => Path.Combine(Install, "data");

    private string Purged => Path.Combine(Install, HomePurge.BackupFolder, "20261119T083005Z");

    public HomePurgeTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ——— What is history.

    /// <summary>🔴 Every entry of the home's table is history or configuration, never both and never neither: a new entry is classified where it is added.</summary>
    [Fact]
    public void Every_home_entry_is_classified_once()
    {
        var history = HomeHistory.Entries.Select(entry => entry.Name).ToList();
        var kept = HomeHistory.Kept.Select(entry => entry.Name).ToList();
        Assert.Empty(history.Intersect(kept));
        Assert.Equal(HomeEntry.Table.Select(entry => entry.Name).Order(), history.Concat(kept).Order());
    }

    /// <summary>🔴 Every table this build's desktop store makes is history or configuration, once.</summary>
    [Fact]
    public void Every_desktop_store_table_is_classified_once()
    {
        using (DesktopStore.Open(DaorisHome.Entry(Home, HomeEntry.DesktopDb), new DesktopStoreOptions { Settled = () => StoreSettled.Yes })) { }
        using var connection = new SqliteConnection($"Data Source={DaorisHome.Entry(Home, HomeEntry.DesktopDb)}");
        connection.Open();
        var made = HomePurge.Names(connection).Where(name => !name.StartsWith("sqlite_", StringComparison.Ordinal)).Order().ToList();
        Assert.Empty(HomeHistory.DesktopTables.Intersect(HomeHistory.DesktopKept));
        var named = HomeHistory.DesktopTables.Concat(HomeHistory.DesktopKept).ToList();
        Assert.True(!made.Except(named).Any(), "made but not named: " + string.Join(", ", made.Except(named)));
        Assert.True(!named.Except(made).Any(), "named but not made: " + string.Join(", ", named.Except(made)));
    }

    // ——— The plan.

    /// <summary>🔴 The plan lists every history path and table with its size and count, no configuration, and changes nothing.</summary>
    [Fact]
    public void The_plan_lists_history_and_no_configuration_and_changes_nothing()
    {
        Seed();

        var (code, said) = Run("purge", "--history", "--plan");

        Assert.Equal(0, code);
        var plan = HomePurge.Plan(Home);
        Assert.Equal(
            ["data/notices.json", "data/sessions", "data/workflows/quests.json", "data/workflows/runs", "work/files/quests"],
            plan.Paths.Select(path => path.Relative));
        Assert.Equal(2, Assert.Single(plan.Paths, path => path.Relative == "data/sessions").Files);
        Assert.Contains(plan.Tables, table => table is { Store: "knowledge", Table: "sessions", Rows: 2 });
        Assert.Contains(plan.Tables, table => table is { Store: "knowledge", Table: "event_journal", Rows: 1 });
        Assert.Contains(plan.Tables, table => table is { Store: "desktop-db", Table: "news_said", Rows: 1 });
        Assert.DoesNotContain(plan.Tables, table => table.Table is "registrations" or "index_entries_6" or "roles");
        foreach (var path in plan.Paths) Assert.Contains(path.Relative, said);
        Assert.Contains("data/knowledge.db sessions  2 rows", said);
        Assert.Contains("Nothing was changed", said);
        Assert.True(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));
        Assert.Equal(2, Rows(DaorisHome.Entry(Home, HomeEntry.Knowledge), "sessions"));
        Assert.False(Directory.Exists(Path.Combine(Install, HomePurge.BackupFolder)));
    }

    // ——— The purge.

    /// <summary>🔴 The purge moves each history path into the dated folder at its own place and empties the history tables after copying each store; configuration is unchanged.</summary>
    [Fact]
    public void The_purge_moves_history_beside_the_install_and_keeps_the_configuration()
    {
        Seed();
        var config = Configuration().ToDictionary(path => path, File.ReadAllText);

        var (code, said) = Run("purge", "--history", "--yes");

        Assert.Equal(0, code);
        Assert.Contains("the configuration stays", said);
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.Notices)));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Workflows, "runs")));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.QuestFiles, "q-1")));
        Assert.True(File.Exists(Path.Combine(Purged, "data", "sessions", "s-1", "events.jsonl")));
        Assert.True(File.Exists(Path.Combine(Purged, "work", "files", "quests", "q-1", "brief.md")));
        Assert.True(File.Exists(Path.Combine(Purged, "data", "workflows", "quests.json")));
        Assert.True(File.Exists(Path.Combine(Purged, HomePurge.SummaryFile)));
        foreach (var (path, text) in config) Assert.Equal(text, File.ReadAllText(path));

        var knowledge = DaorisHome.Entry(Home, HomeEntry.Knowledge);
        Assert.Equal(0, Rows(knowledge, "sessions"));
        Assert.Equal(0, Rows(knowledge, "event_journal"));
        Assert.Equal(1, Rows(knowledge, "registrations"));
        Assert.Equal(1, Rows(knowledge, "index_entries_6"));
        Assert.Equal(2, Rows(Path.Combine(Purged, "data", "knowledge.db"), "sessions"));
        var desktop = DaorisHome.Entry(Home, HomeEntry.DesktopDb);
        Assert.Equal(0, Rows(desktop, "news_said"));
        Assert.Equal(1, Rows(desktop, "roles"));
        Assert.Equal(1, Rows(Path.Combine(Purged, "data", "desktop.db"), "news_said"));

        // A second purge finds nothing more to move.
        Assert.Empty(HomePurge.Plan(Home).Paths);
    }

    /// <summary>🔴 A running desktop holds the home: the purge is refused, saying how to close it, and nothing moves.</summary>
    [Fact]
    public void A_running_desktop_refuses_the_purge_and_nothing_moves()
    {
        Seed();
        using var desktop = HomeLease.TryAcquire(Home, LeaseKind.Desktop, out _);
        Assert.NotNull(desktop);

        var (code, said) = Run("purge", "--history", "--yes");

        Assert.Equal(1, code);
        Assert.Contains("Close Daoris", said);
        Assert.Contains("nothing was moved", said);
        Assert.True(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));
        Assert.Equal(2, Rows(DaorisHome.Entry(Home, HomeEntry.Knowledge), "sessions"));
        Assert.False(Directory.Exists(Path.Combine(Install, HomePurge.BackupFolder)));
    }

    /// <summary>A session's terminal lists and never purges; words the door does not answer are its usage.</summary>
    [Fact]
    public void A_sessions_terminal_may_list_but_not_purge_and_other_words_are_the_usage()
    {
        Seed();
        Assert.Equal(1, HomePurgeCommand.Run(["purge", "--history", "--yes"], Home, () => Now, null, "s-9", out var said));
        Assert.Contains("a session's", said);
        Assert.Equal(0, HomePurgeCommand.Run(["purge", "--history", "--plan"], Home, () => Now, null, "s-9", out _));
        Assert.True(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));

        Assert.Equal(2, HomePurgeCommand.Run(["purge", "--yes"], Home, () => Now, null, null, out said));
        Assert.Contains(HomePurgeCommand.Usage, said);
        Assert.Equal(2, HomePurgeCommand.Run(["purge", "--history", "--plan", "--yes"], Home, () => Now, null, null, out _));
        Assert.Equal(2, HomePurgeCommand.Run(["clear"], Home, () => Now, null, null, out _));
    }

    private (int Code, string Said) Run(params string[] args)
    {
        var code = HomePurgeCommand.Run(args, Home, () => Now, null, null, out var said);
        return (code, said);
    }

    /// <summary>A home with history and configuration of each kind: files, a desktop store and a stand-in of the service's.</summary>
    private void Seed()
    {
        Write(DaorisHome.Entry(Home, HomeEntry.Sessions, "s-1", "events.jsonl"), "{}\n");
        Write(DaorisHome.Entry(Home, HomeEntry.Sessions, "s-2", "events.jsonl"), "{}\n");
        Write(DaorisHome.Entry(Home, HomeEntry.Notices), "[]");
        Write(DaorisHome.Entry(Home, HomeEntry.QuestFiles, "q-1", "brief.md"), "the brief\n");
        Write(DaorisHome.Entry(Home, HomeEntry.Workflows, "runs", "r-1.json"), "{}");
        Write(DaorisHome.Entry(Home, HomeEntry.Workflows, "quests.json"), "{}");
        foreach (var path in Configuration()) Write(path, $"{{\"kept\":\"{Path.GetFileName(path)}\"}}");

        using (DesktopStore.Open(DaorisHome.Entry(Home, HomeEntry.DesktopDb), new DesktopStoreOptions { Settled = () => StoreSettled.Yes })) { }
        Sql(DaorisHome.Entry(Home, HomeEntry.DesktopDb),
            "INSERT INTO news_said (key, at) VALUES ('told', '2026-11-01');"
            + "INSERT INTO roles (id, state, record, revision, at) VALUES ('build', 'set', '{}', 1, '2026-11-01');");
        Sql(DaorisHome.Entry(Home, HomeEntry.Knowledge),
            "CREATE TABLE sessions (id TEXT); INSERT INTO sessions VALUES ('s-1'), ('s-2');"
            + "CREATE TABLE event_journal (position INTEGER PRIMARY KEY AUTOINCREMENT, record TEXT); INSERT INTO event_journal (record) VALUES ('s-1');"
            + "CREATE TABLE registrations (repository TEXT); INSERT INTO registrations VALUES ('engine');"
            + "CREATE TABLE index_entries_6 (id TEXT); INSERT INTO index_entries_6 VALUES ('a');");
        SqliteConnection.ClearAllPools();
    }

    /// <summary>Configuration a purge keeps: the driver's file, the accounts, a workflow's definition and the casts, a key, a tree's file.</summary>
    private IEnumerable<string> Configuration() =>
    [
        DaorisHome.Entry(Home, HomeEntry.DriverConfig), DaorisHome.Entry(Home, HomeEntry.Accounts),
        DaorisHome.Entry(Home, HomeEntry.Workflows, "docs-to-pr.json"), DaorisHome.Entry(Home, HomeEntry.Workflows, "casts.json"),
        DaorisHome.Entry(Home, HomeEntry.Keys), DaorisHome.Entry(Home, HomeEntry.Trees, "work", "engine", "t-1", "kept.json"),
    ];

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void Sql(string store, string sql)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(store)!);
        using var connection = new SqliteConnection($"Data Source={store}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Rows(string store, string table)
    {
        using var connection = new SqliteConnection($"Data Source={store};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }
}
