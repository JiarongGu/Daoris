using System.Diagnostics;
using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KSCHEMA1: an older build never drops a newer store. Every host on a machine opens the one file, and a
/// connector left behind by an update is an older build. It read any version but its own as a mismatch to
/// rebuild, so it dropped the newer index, stamped its own older version, forgot every repository's fed
/// commit, and the newer host failed every knowledge route over tables it no longer recognised. A store
/// one version ahead now refuses to open, naming both versions, and every table and row stays as it was.
/// </summary>
public sealed class NewerStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T09:00:00Z");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-newer-" + Guid.NewGuid().ToString("N")[..8]);

    private string Database => Path.Combine(_root, "index", "knowledge.db");

    private static int Ahead => SqliteKnowledgeStore.SchemaVersion + 1;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 20 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public async Task A_store_one_version_ahead_refuses_naming_both_versions_and_keeps_every_row()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            await store.ReplaceRepositoryAsync("alpha", [Entry("A lesson", "What was learned.")]);
        }

        Stamp(Database, Ahead);
        var before = Snapshot(Database);

        var refusal = await Assert.ThrowsAsync<NewerStoreException>(() => SqliteKnowledgeStore.OpenAsync(Database));

        Assert.Equal(Ahead, refusal.Found);
        Assert.Equal(SqliteKnowledgeStore.SchemaVersion, refusal.Known);
        Assert.Contains("newer Daoris", refusal.Message);
        Assert.Contains($"version {Ahead}", refusal.Message);
        Assert.Contains($"this build's is {SqliteKnowledgeStore.SchemaVersion}", refusal.Message);
        Assert.Contains($"Update the Daoris in '{Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)}'", refusal.Message);
        Assert.Contains(Path.GetFullPath(Database), refusal.Message);
        Assert.Equal(refusal.Message, refusal.ToString());
        Assert.Equal(before, Snapshot(Database));
        Assert.Contains("A lesson", before);
        Assert.Equal(Ahead, Version(Database));
    }

    /// <summary>
    /// The whole composition, as each host opens it: the index refuses before any other store touches the
    /// file, so the quests, sessions, registrations and their fed commits, asks and keys stay whole too.
    /// Before, the older build's rebuild also forgot every repository's fed commit (REV3's rule, read as a
    /// rebuild it never was).
    /// </summary>
    [Fact]
    public async Task A_composed_service_over_a_newer_store_refuses_and_leaves_every_table_whole()
    {
        var options = new ServiceOptions(Path.Combine(_root, "unread"), Database);
        await using (var composed = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
            await composed.Service.RegisterAsync(
                new Registration("Open", Adopted: true, "A repository.", ["its area"], ["a quest"], [], 0,
                    Joined: true, SharesKnowledge: true, DefaultBranch: "main"),
                Now);
            Assert.True((await composed.Service.FeedAsync(
                "Open", [Entry("A fed lesson", "Fed from a commit.")],
                new FeedProvenance("aaaa1111bbbb2222", Now, "main", "person@machine-a"))).Accepted);
            var quest = await composed.Quests.PublishAsync("Open", "Elsewhere", "A held quest", "Why it is asked.", Now);
            await composed.Sessions.CreateAsync(quest.Id, "Open", "an-adapter", Now);
            await composed.Keys.MintAsync("person@machine-a", TimeSpan.FromDays(1), Now);
            Assert.NotNull((await composed.Asks.AskAsync(new AskRequest(Workspaces.Default, "An ask that is kept."), Now)).Ask);
        }

        Stamp(Database, Ahead);
        var before = Snapshot(Database);

        await Assert.ThrowsAsync<NewerStoreException>(
            () => ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()));
        await Assert.ThrowsAsync<NewerStoreException>(() => ServiceFactory.OpenKeysAsync(options));

        Assert.Equal(before, Snapshot(Database));
        foreach (var held in new[] { "A fed lesson", "aaaa1111bbbb2222", "A held quest", "an-adapter", "person@machine-a", "An ask that is kept." })
        {
            Assert.Contains(held, before);
        }

        Assert.Equal(Ahead, Version(Database));
    }

    /// <summary>
    /// The read and the stamp are one write transaction. Otherwise an older build that read its own version
    /// a moment before a newer one stamped the file wrote its older number back over the newer tables, and
    /// the newer build's next start rebuilt what it had just made.
    /// </summary>
    [Fact]
    public async Task An_older_build_opening_while_a_newer_one_stamps_waits_and_then_refuses()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (await SqliteKnowledgeStore.OpenAsync(Database))
        {
        }

        SqliteConnection.ClearAllPools();
        await using var newer = new SqliteConnection($"Data Source={Database};Pooling=False");
        await newer.OpenAsync();
        await using var transaction = newer.BeginTransaction(deferred: false);
        await using (var stamp = newer.CreateCommand())
        {
            stamp.Transaction = transaction;
            stamp.CommandText = $"PRAGMA user_version = {Ahead};";
            await stamp.ExecuteNonQueryAsync();
        }

        // On a thread of its own: the driver's asynchronous calls run synchronously, so a wait for the lock
        // would otherwise hold this thread, and the commit below would never run.
        var opening = Task.Run(() => SqliteKnowledgeStore.OpenAsync(Database));
        await Task.Delay(500);
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<NewerStoreException>(() => opening.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(Ahead, Version(Database));
    }

    /// <summary>An older store is still rebuilt as before: only a newer one refuses.</summary>
    [Fact]
    public async Task A_store_one_version_behind_is_still_rebuilt()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            await store.ReplaceRepositoryAsync("alpha", [Entry("A lesson", "What was learned.")]);
        }

        Stamp(Database, SqliteKnowledgeStore.SchemaVersion - 1);

        await using var reopened = await SqliteKnowledgeStore.OpenAsync(Database);

        Assert.True(reopened.Rebuilt);
        Assert.Empty(await reopened.AllAsync());
        Assert.Equal(SqliteKnowledgeStore.SchemaVersion, Version(Database));
    }

    /// <summary>
    /// The connector a session's harness starts, as it starts it: the real executable over a newer store
    /// says why in one sentence on its standard error, exits 2 as every other start refusal does, writes the
    /// refusal to its machine log, and leaves the store as it found it. A session then has no connector.
    /// </summary>
    [Fact]
    public async Task The_connector_over_a_newer_store_says_why_in_a_sentence_and_exits_2()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            await store.ReplaceRepositoryAsync("alpha", [Entry("A lesson", "What was learned.")]);
        }

        Stamp(Database, Ahead);
        var before = Snapshot(Database);
        var home = Path.Combine(_root, "home");
        var repositories = Path.Combine(_root, "repositories");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(repositories);

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "daoris-knowledge.dll"));
        foreach (var (name, value) in new Dictionary<string, string?>
                 {
                     [DaorisHome.Variable] = home,
                     [ServiceOptions.DatabaseVariable] = Database,
                     [ServiceOptions.RootVariable] = repositories,
                     [ServiceOptions.ModelVariable] = null,
                     [ServiceOptions.UrlVariable] = null,
                     [ServiceOptions.WindowVariable] = null,
                     [ServiceOptions.RepositoryVariable] = null,
                     [ServiceOptions.DocumentsVariable] = null,
                     [ServiceOptions.IndexVariable] = null,
                     [RemoteConfig.UrlVariable] = null,
                     [RemoteConfig.KeyVariable] = null,
                     [RemoteConfig.WorkspaceVariable] = null,
                     [RemoteConfig.PathVariable] = null,
                     [RuleProposalBox.HomeVariable] = null,
                     [IntakeScope.AskVariable] = null,
                     [IntakeScope.SessionVariable] = null,
                 })
        {
            if (value is null) start.Environment.Remove(name);
            else start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        var said = process.StandardError.ReadToEndAsync();
        var printed = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            Assert.Fail("the connector served over a newer store instead of refusing it");
        }

        process.WaitForExit();
        var error = (await said).Trim();
        await printed;

        Assert.Equal(2, process.ExitCode);
        Assert.Contains("newer Daoris", error);
        Assert.Contains($"version {Ahead}", error);
        Assert.Contains($"this build's is {SqliteKnowledgeStore.SchemaVersion}", error);
        Assert.DoesNotContain("Unhandled exception", error);
        Assert.DoesNotContain("   at ", error);
        Assert.Single(error.Split('\n'));
        Assert.Equal(before, Snapshot(Database));

        var logged = Directory.EnumerateFiles(Path.Combine(home, MachineLog.Folder), "*.mcp.jsonl")
            .SelectMany(File.ReadAllLines)
            .ToList();
        Assert.Contains(logged, line => line.Contains("\"event\":\"error\"", StringComparison.Ordinal)
            && line.Contains("\"where\":\"start\"", StringComparison.Ordinal)
            && line.Contains("newer Daoris", StringComparison.Ordinal));
    }

    private static KnowledgeEntry Entry(string title, string body) =>
        new("alpha", EntryKind.Decision, Provenance.Canonical, title, body, "docs/DECISIONS.md", title);

    /// <summary>What a newer (or older) build leaves in the file's header, as only that build would write it.</summary>
    private static void Stamp(string database, int version)
    {
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {version};";
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
    }

    private static int Version(string database)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>
    /// Every object the file declares, and every row of every table, read without writing: the virtual
    /// table and the shadow tables the full-text index keeps included.
    /// </summary>
    private static string Snapshot(string database)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();

        var objects = new List<(string Type, string Name, string? Sql)>();
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = "SELECT type, name, sql FROM sqlite_master ORDER BY type, name;";
            using var reader = schema.ExecuteReader();
            while (reader.Read()) objects.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        var text = new StringBuilder();
        foreach (var (type, name, sql) in objects)
        {
            text.Append(type).Append(' ').Append(name).Append(": ").AppendLine(sql);
            if (type != "table") continue;

            using var rows = connection.CreateCommand();
            rows.CommandText = $"SELECT * FROM \"{name}\";";
            using var reader = rows.ExecuteReader();
            while (reader.Read())
            {
                for (var column = 0; column < reader.FieldCount; column++)
                {
                    var value = reader.GetValue(column);
                    text.Append(value is byte[] bytes ? Convert.ToHexString(bytes) : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))
                        .Append('|');
                }

                text.AppendLine();
            }
        }

        return text.ToString();
    }
}
