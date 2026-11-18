using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// `daoris home purge --history` over a real service: a fixture install's home holds an ask, its quest and a session in the service's own
/// store beside configuration, the purge runs with the host closed, and the host started again over the same home lists none of them
/// while its registry, the home's settings and the desktop store's roles remain. The service is its own host in a process of its own
/// (<see cref="RealServiceHost"/>), so every table the real store makes is judged by the same lists the purge empties by.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class HomePurgeProcessTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 30, 5, TimeSpan.Zero);

    private readonly string _scratch = Path.Combine(TestHomes.Root, "daoris-purgeproc-" + Guid.NewGuid().ToString("N")[..8]);

    private string Install => Path.Combine(_scratch, "install");

    private string Home => Path.Combine(Install, "data");

    public HomePurgeProcessTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 A fixture home purged while nothing runs: the restarted host lists no session, quest or ask, the history files are in the dated folder, and
    /// the registry, the home's settings and the desktop store's roles are as they were. Every table the real service store made is named by the purge's lists.
    /// </summary>
    [Fact]
    public async Task A_purged_home_lists_no_history_to_the_host_and_keeps_its_configuration()
    {
        var checkout = Path.Combine(_scratch, "checkouts", "Engine");
        Directory.CreateDirectory(checkout);
        string session;
        using (var first = await RealServiceHost.StartAsync(_scratch, Home))
        {
            using var client = new ServiceClient(first.Url, null);
            var (registered, said) = await client.RegisterAsync(new JsonObject
            {
                ["repository"] = "Engine",
                ["root"] = checkout,
                ["packs"] = new JsonArray(),
                ["domain"] = new JsonObject { ["summary"] = "Owns the engine.", ["owns"] = new JsonArray("the engine"), ["accepts"] = new JsonArray("engine work") },
            });
            Assert.True(registered, said);
            var asked = await client.AskAsync("default", "tune the engine's start", [], [], to: "Engine");
            Assert.True(asked.Ok, asked.Message);
            var (opened, why) = await client.OpenSessionAsync(asked.QuestId!, "claude-code");
            session = opened ?? throw new InvalidOperationException(why);
            Assert.NotEmpty(await client.EveryAskAsync());
            Assert.NotEmpty(await client.EveryQuestAsync());
            Assert.NotEmpty(await client.SessionRecordsAsync());
        }

        SeedFiles(session);
        var config = Configuration().ToDictionary(path => path, File.ReadAllText);

        // Every table the real store made is history or kept, never neither: a table added to the service is classified where it is added.
        var knowledge = DaorisHome.Entry(Home, HomeEntry.Knowledge);
        var made = Names(knowledge).Where(name => !name.StartsWith("sqlite_", StringComparison.Ordinal) && !IsPartOfAVirtualTable(name)).ToList();
        var named = HomeHistory.ServiceTables.Concat(HomeHistory.ServiceKept).ToList();
        Assert.Empty(made.Where(name => !name.StartsWith(HomeHistory.IndexPrefix, StringComparison.Ordinal)).Except(named));

        var code = HomePurgeCommand.Run(["purge", "--history", "--yes"], Home, () => Now, null, null, out var purged);
        Assert.True(code == 0, purged);

        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Workflows, "runs")));
        Assert.True(File.Exists(Path.Combine(Install, HomePurge.BackupFolder, "20261119T083005Z", HomePurge.SummaryFile)));
        foreach (var (path, text) in config) Assert.Equal(text, File.ReadAllText(path));

        using var second = await RealServiceHost.StartAsync(_scratch, Home);
        using var after = new ServiceClient(second.Url, null);
        Assert.Empty(await after.SessionRecordsAsync());
        Assert.Empty(await after.EveryQuestAsync());
        Assert.Empty(await after.EveryAskAsync());
        Assert.Equal(1, Rows(knowledge, "quest_forgotten"));
        Assert.Equal("Engine", Assert.Single(await after.RegistrationsAsync()).Repository);
        Assert.Equal(0, Rows(DaorisHome.Entry(Home, HomeEntry.DesktopDb), "notices"));
        Assert.Equal(1, Rows(DaorisHome.Entry(Home, HomeEntry.DesktopDb), "roles"));
        Assert.Equal(0, Rows(DaorisHome.Entry(Home, HomeEntry.DesktopDb), "news_said"));
    }

    /// <summary>🔴 A service holding the home refuses the purge, saying how to close it, and nothing moves or is emptied.</summary>
    [Fact]
    public async Task A_running_service_refuses_the_purge()
    {
        using var host = await RealServiceHost.StartAsync(_scratch, Home);
        using var client = new ServiceClient(host.Url, null);
        var (registered, said) = await client.RegisterAsync(new JsonObject
        {
            ["repository"] = "Engine",
            ["root"] = _scratch,
            ["packs"] = new JsonArray(),
            ["domain"] = new JsonObject { ["summary"] = "Owns the engine.", ["owns"] = new JsonArray("the engine"), ["accepts"] = new JsonArray("engine work") },
        });
        Assert.True(registered, said);
        var asked = await client.AskAsync("default", "tune the engine's start", [], [], to: "Engine");
        Assert.True(asked.Ok, asked.Message);
        SeedFiles("s-1");
        // What the desktop leaves for its service: its address, with the process that holds it.
        using (var service = Process.GetProcessById(host.Id))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RunningAddress.PathIn(Home))!);
            File.WriteAllText(RunningAddress.PathIn(Home), new JsonObject
            {
                ["url"] = host.Url, ["pid"] = host.Id, ["started"] = service.StartTime.ToUniversalTime().Ticks,
                ["since"] = Now.ToString("o"), ["home"] = HomeIdentity.Ensure(Home),
            }.ToJsonString());
        }

        var code = HomePurgeCommand.Run(["purge", "--history", "--yes"], Home, () => Now, null, null, out var said2);

        Assert.Equal(1, code);
        Assert.Contains("service runs", said2);
        Assert.Contains("Close Daoris", said2);
        Assert.Contains("nothing was moved", said2);
        Assert.True(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions)));
        Assert.False(Directory.Exists(Path.Combine(Install, HomePurge.BackupFolder)));
        Assert.NotEmpty(await client.EveryAskAsync());
    }

    /// <summary>History a home leaves behind: a session's files, a run's record, the notices, and the desktop store's rows of each kind.</summary>
    private void SeedFiles(string session)
    {
        Write(DaorisHome.Entry(Home, HomeEntry.Sessions, session, "events.jsonl"), "{}\n");
        Write(DaorisHome.Entry(Home, HomeEntry.Workflows, "runs", "r-1.json"), "{}");
        Write(DaorisHome.Entry(Home, HomeEntry.Notices), "[]");
        foreach (var path in Configuration()) Write(path, $"{{\"kept\":\"{Path.GetFileName(path)}\"}}");
        using (DesktopStore.Open(DaorisHome.Entry(Home, HomeEntry.DesktopDb), new DesktopStoreOptions { Settled = () => StoreSettled.Yes })) { }
        using var connection = new SqliteConnection($"Data Source={DaorisHome.Entry(Home, HomeEntry.DesktopDb)};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO news_said (key, at) VALUES ('told', '2026-11-01');"
            + "INSERT INTO notices (id, kind, at, read) VALUES ('n-1', 'done', '2026-11-18T00:00:00Z', 0);"
            + "INSERT INTO roles (id, state, record, revision, at) VALUES ('build', 'set', '{}', 1, '2026-11-01');";
        command.ExecuteNonQuery();
    }

    private IEnumerable<string> Configuration() =>
    [
        DaorisHome.Entry(Home, HomeEntry.DriverConfig), DaorisHome.Entry(Home, HomeEntry.Accounts),
        DaorisHome.Entry(Home, HomeEntry.Workflows, "docs-to-pr.json"), DaorisHome.Entry(Home, HomeEntry.Workflows, "casts.json"),
        DaorisHome.Entry(Home, HomeEntry.Permissions), DaorisHome.Entry(Home, HomeEntry.PluginState),
        DaorisHome.Entry(Home, HomeEntry.Plugins, "stand-in", "plugin.json"), DaorisHome.Entry(Home, HomeEntry.Keys),
    ];

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static IReadOnlyList<string> Names(string store)
    {
        using var connection = new SqliteConnection($"Data Source={store};Mode=ReadOnly;Pooling=False");
        connection.Open();
        return [.. HomePurge.Names(connection)];
    }

    /// <summary>The shadow tables a full-text index makes for itself (<c>work_records_fts_data</c> and the like), which belong to it.</summary>
    private static bool IsPartOfAVirtualTable(string name) =>
        new[] { "_data", "_idx", "_content", "_docsize", "_config" }.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal) && name.Contains("_fts", StringComparison.Ordinal));

    private static long Rows(string store, string table)
    {
        using var connection = new SqliteConnection($"Data Source={store};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }
}
