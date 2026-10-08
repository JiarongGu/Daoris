using System.Diagnostics;
using System.Text;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KSCHEMA1: an older build never drops a newer index, and never refuses the rest of the store. Every host on a
/// machine opens the one file, and a connector an update left behind is an older build. It read any version but its
/// own as one to rebuild, so it dropped the newer index, stamped its own older version and forgot every repository's
/// fed commit, and the newer host failed every knowledge route. A store whose index is one version ahead now opens
/// with nothing dropped or stamped: its quests, sessions, asks, keys and registry work, and every operation on the
/// index answers the refusal, naming both versions. Refusing the whole store would leave an install an update rolled
/// back (D139) with no host at all.
/// </summary>
public sealed class NewerIndexTests : IDisposable
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
    public async Task A_store_one_version_ahead_opens_and_only_its_index_refuses_writing_nothing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            await store.ReplaceRepositoryAsync("alpha", [Entry("A lesson", "What was learned.")]);
        }

        Stamp(Database, Ahead);
        var before = Snapshot(Database);

        await using (var opened = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            Assert.False(opened.Rebuilt);
            var refusal = Assert.IsType<NewerIndexException>(opened.Refusal);
            Assert.Equal(Ahead, refusal.Found);
            Assert.Equal(SqliteKnowledgeStore.SchemaVersion, refusal.Known);
            Assert.Equal(Path.GetFullPath(Database), refusal.Store);
            Assert.Contains("written by a newer Daoris", refusal.Message);
            Assert.Contains($"its schema is version {Ahead}, and this build's is {SqliteKnowledgeStore.SchemaVersion}", refusal.Message);
            Assert.Contains("quests, sessions and asks work as before", refusal.Message);
            Assert.Contains("Update this Daoris", refusal.Message);
            // A door answers it, and a browser or a keyed caller is never told a machine path (D46, D47 §4).
            Assert.DoesNotContain(_root, refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(refusal.Build, refusal.Message, StringComparison.OrdinalIgnoreCase);

            await Assert.ThrowsAsync<NewerIndexException>(() => opened.AllAsync());
            await Assert.ThrowsAsync<NewerIndexException>(() => opened.FindAsync("anything"));
            await Assert.ThrowsAsync<NewerIndexException>(() => opened.CountByRepositoryAsync());
            await Assert.ThrowsAsync<NewerIndexException>(
                () => opened.ReplaceRepositoryAsync("alpha", [Entry("Older", "As an older build reads it.")]));
            await Assert.ThrowsAsync<NewerIndexException>(
                () => new SqliteKnowledgeSearch(opened).SearchAsync(new KnowledgeQuery("lesson")));
        }

        Assert.Equal(before, Snapshot(Database));
        Assert.Contains("A lesson", before);
        Assert.Equal(Ahead, Version(Database));
    }

    /// <summary>
    /// The whole composition, as each host opens it: it starts, and what is not derived works as it does over an index
    /// of this build's own. Each operation on the index refuses, the index's tables and the fed commit stay as they were,
    /// and nothing forgets which commit the index holds (REV3's rule is for a rebuild, and nothing was rebuilt).
    /// </summary>
    [Fact]
    public async Task A_composed_service_over_a_newer_index_starts_and_its_records_work_while_its_index_refuses()
    {
        var options = new ServiceOptions(Path.Combine(_root, "unread"), Database);
        string quest;
        await using (var composed = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
            Assert.Null(composed.IndexRefusal);
            await composed.Service.RegisterAsync(Open, Now);
            Assert.True((await composed.Service.FeedAsync(
                "Open", [Entry("A fed lesson", "Fed from a commit.")],
                new FeedProvenance("aaaa1111bbbb2222", Now, "main", "person@machine-a"))).Accepted);
            quest = (await composed.Quests.PublishAsync("Open", "Elsewhere", "A held quest", "Why it is asked.", Now)).Id;
            await composed.Keys.MintAsync("person@machine-a", TimeSpan.FromDays(1), Now);
        }

        Stamp(Database, Ahead);
        var index = Snapshot(Database, IndexTable);

        await using (var composed = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
            var refusal = Assert.IsType<NewerIndexException>(composed.IndexRefusal);
            Assert.Equal(Ahead, refusal.Found);

            // What is not derived works.
            Assert.NotNull(await composed.Quests.FindAsync(quest));
            Assert.NotNull(await composed.Quests.PublishAsync("Open", "Elsewhere", "A second quest", "Asked after.", Now));
            var session = await composed.Sessions.CreateAsync(quest, "Open", "an-adapter", Now);
            Assert.NotNull(await composed.Sessions.FindAsync(session.Id));
            await composed.Keys.MintAsync("person@machine-b", TimeSpan.FromDays(1), Now);
            Assert.Equal(2, (await composed.Keys.ListAsync()).Count);
            Assert.NotNull((await composed.Asks.AskAsync(new AskRequest(Workspaces.Default, "An ask made over a newer index."), Now)).Ask);
            Assert.Contains(await composed.Service.RegistryAsync(), registration => registration.Repository == "Open");

            // The index refuses, each way into it.
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.SearchAsync(new KnowledgeQuery("lesson")));
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.AnswerAsync(new KnowledgeQuery("lesson")));
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.FindAsync("anything"));
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.SummarizeAsync());
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.FindConvergenceAsync());
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.LocalEntriesAsync("Open"));
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.RefreshAsync());
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.FeedAsync(
                "Open", [Entry("An older reading", "Fed to an older build.")],
                new FeedProvenance("cccc3333dddd4444", Now.AddHours(1), "main", "person@machine-b")));

            // A retire would leave the index serving what it took off the map, so it is refused whole.
            await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.RetireAsync("Open"));
            Assert.Contains(await composed.Service.RegistryAsync(), registration => registration.Repository == "Open");

            Assert.Equal("aaaa1111bbbb2222", (await composed.Service.HeldAsync("Open")).Knowledge);
        }

        await using (var keys = await ServiceFactory.OpenKeysAsync(options))
        {
            Assert.Equal(2, (await keys.Keys.ListAsync()).Count);
        }

        Assert.Equal(index, Snapshot(Database, IndexTable));
        Assert.Contains("A fed lesson", index);
        Assert.Equal(Ahead, Version(Database));
    }

    /// <summary>
    /// With a model the search is the hybrid, whose tiers each fold a throw into a failure and answer nothing: over a
    /// newer index it answers the refusal instead, so nobody reads "no matches" from an index this build cannot read.
    /// </summary>
    [Fact]
    public async Task A_search_with_a_model_answers_the_refusal_rather_than_nothing()
    {
        var options = new ServiceOptions(Path.Combine(_root, "unread"), Database);
        await using (await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
        }

        Stamp(Database, Ahead);

        await using var composed = await ServiceFactory.CreateAsync(
            options, new DimensionEmbedder(["lesson", "learned"]), source: new EmptyKnowledgeSource());

        Assert.True(composed.SemanticEnabled);
        await Assert.ThrowsAsync<NewerIndexException>(() => composed.Service.AnswerAsync(new KnowledgeQuery("lesson")));
    }

    /// <summary>
    /// The connector's tools over a newer index: each one that reads the index answers the refusal as its text, and the
    /// registry, which a quest is addressed by, answers as before.
    /// </summary>
    [Fact]
    public async Task The_connectors_index_tools_answer_the_refusal_and_its_registry_answers()
    {
        var options = new ServiceOptions(Path.Combine(_root, "unread"), Database);
        await using (var composed = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
            await composed.Service.RegisterAsync(Open, Now);
        }

        Stamp(Database, Ahead);

        await using var refused = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource());
        var tools = new KnowledgeTools(
            refused.Service, refused.Quests, refused.Exchange, new AmbientWorkspace(_root), refused.Asks);
        var sentence = refused.IndexRefusal!.Message;

        Assert.Equal(sentence, await tools.SearchAsync("lesson"));
        Assert.Equal(sentence, await tools.GetAsync("anything"));
        Assert.Equal(sentence, await tools.RepositoriesAsync(workspace: "all"));
        Assert.Equal(sentence, await tools.ConvergenceAsync(workspace: "all"));
        Assert.Equal(sentence, await tools.RefreshAsync());
        Assert.Contains("Open", await tools.RegistryAsync(workspace: "all"));
    }

    /// <summary>
    /// The read and the stamp are one write transaction. Otherwise an older build that read its own version a moment
    /// before a newer one stamped the file wrote its older number back over the newer tables, and the newer build's
    /// next start rebuilt what it had just made.
    /// </summary>
    [Fact]
    public async Task An_older_build_opening_while_a_newer_one_stamps_waits_and_then_leaves_the_index_alone()
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

        await using (var opened = await opening.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            Assert.NotNull(opened.Refusal);
            Assert.False(opened.Rebuilt);
        }

        Assert.Equal(Ahead, Version(Database));
    }

    /// <summary>An older index is still rebuilt as before: only a newer one is refused.</summary>
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

        Assert.Null(reopened.Refusal);
        Assert.True(reopened.Rebuilt);
        Assert.Empty(await reopened.AllAsync());
        Assert.Equal(SqliteKnowledgeStore.SchemaVersion, Version(Database));
    }

    /// <summary>
    /// The connector a session's harness starts, as it starts it: over a newer index the real executable starts and
    /// stays up, says the refusal once on its standard error with the folder it runs from, and writes it once to its
    /// machine log as <c>index.refused</c>, by the two versions and never a path.
    /// </summary>
    [Fact]
    public async Task The_connector_over_a_newer_index_starts_and_says_so_once()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            await store.ReplaceRepositoryAsync("alpha", [Entry("A lesson", "What was learned.")]);
        }

        Stamp(Database, Ahead);
        var before = Snapshot(Database, IndexTable);
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
        var said = new StringBuilder();
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is not null) lock (said) said.AppendLine(line.Data);
        };
        process.OutputDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        try
        {
            IReadOnlyList<string> logged = [];
            for (var attempt = 0; attempt < 150 && !process.HasExited; attempt++)
            {
                logged = McpLogLines(home);
                if (logged.Any(line => line.Contains("\"event\":\"app.started\"", StringComparison.Ordinal))) break;
                await Task.Delay(200);
            }

            Assert.False(process.HasExited, $"the connector ended over a newer index: {said}");
            var refused = Assert.Single(logged, line => line.Contains("\"event\":\"index.refused\"", StringComparison.Ordinal));
            Assert.Contains("\"level\":\"warn\"", refused);
            Assert.Contains($"\"found\":{Ahead}", refused);
            Assert.Contains($"\"known\":{SqliteKnowledgeStore.SchemaVersion}", refused);
            Assert.DoesNotContain("knowledge.db", refused, StringComparison.Ordinal);
            Assert.DoesNotContain(Path.GetFileName(_root), refused, StringComparison.Ordinal);

            string error;
            lock (said) error = said.ToString();
            Assert.Contains("written by a newer Daoris", error);
            Assert.Contains(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), error);
            Assert.Single(error.Split('\n', StringSplitOptions.RemoveEmptyEntries), line => line.Contains("newer Daoris", StringComparison.Ordinal));
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(15_000)) process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }

        Assert.Equal(before, Snapshot(Database, IndexTable));
        Assert.Equal(Ahead, Version(Database));
    }

    private static Registration Open => new(
        "Open", Adopted: true, "A repository.", ["its area"], ["a quest"], [], 0,
        Joined: true, SharesKnowledge: true, DefaultBranch: "main");

    private static KnowledgeEntry Entry(string title, string body) =>
        new("alpha", EntryKind.Decision, Provenance.Canonical, title, body, "docs/DECISIONS.md", title);

    /// <summary>The index's own tables, the full-text index's shadow tables and its indexes among them.</summary>
    private static bool IndexTable(string name) => name.StartsWith("entries", StringComparison.Ordinal)
        || name.StartsWith("ix_entries", StringComparison.Ordinal);

    private static IReadOnlyList<string> McpLogLines(string home)
    {
        var folder = Path.Combine(home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*.mcp.jsonl")
            .SelectMany(path =>
            {
                // The connector holds the file open for appending; read beside it.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .ToList();
    }

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
    /// Every object the file declares that <paramref name="include"/> names (all of them by default), and every row of
    /// each table, read without writing: the virtual table and the shadow tables the full-text index keeps included.
    /// </summary>
    private static string Snapshot(string database, Func<string, bool>? include = null)
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
        foreach (var (type, name, sql) in objects.Where(each => include?.Invoke(each.Name) ?? true))
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
