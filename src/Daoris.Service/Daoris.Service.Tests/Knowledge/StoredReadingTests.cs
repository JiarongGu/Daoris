using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2a (the memory coordinator design's §5): every session's connector is its own process over the one
/// <c>knowledge.db</c>, so the index keeps each repository's reading beside its entries, and a write lands only where the
/// generation is still the one its writer read before reading the source. Each service here opens its own connection to
/// the file, as a second connector does.
/// </summary>
public sealed class StoredReadingTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-31T09:00:00Z");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-reading-" + Guid.NewGuid().ToString("N")[..8]);

    public StoredReadingTests() => Directory.CreateDirectory(_root);

    private string Database => Path.Combine(_root, "knowledge.db");

    public void Dispose()
    {
        SqlitePools.Release(Database);
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

    private static KnowledgeEntry Entry(string repository, string body) =>
        new(repository, EntryKind.Knowledge, Provenance.Local, "Probe lock", body, "docs/probe-lock.md");

    private static Registration Open(string repository, string? workspace = null) => new(
        repository, Adopted: true, "A repository.", ["its area"], ["a quest"], [], 0,
        Joined: true, SharesKnowledge: true, Workspace: workspace, DefaultBranch: "main");

    /// <summary>A source that answers with one entry, and when held, waits to be let go after saying it was asked.</summary>
    private sealed class HeldSource(string repository, string body) : IKnowledgeSource
    {
        private TaskCompletionSource? _release;

        public TaskCompletionSource Asked { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "held";

        public void Hold()
        {
            _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Release() => _release?.TrySetResult();

        public async Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default)
        {
            Asked.TrySetResult();
            if (_release is { } release) await release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            return [Entry(repository, body)];
        }
    }

    /// <summary>
    /// A store that lets another connector write once, after the first step a restamp takes: its read of what it held, where it
    /// read them before writing them back, or its one write, where it reads nothing.
    /// </summary>
    private sealed class Interleaved(IKnowledgeStore inner) : IKnowledgeStore
    {
        public Func<Task>? After { get; set; }

        private async Task FireAsync()
        {
            if (After is not { } after) return;
            After = null;
            await after();
        }

        public Task<bool> ReplaceRepositoryAsync(
            string repository, IReadOnlyList<KnowledgeEntry> entries, ReadingWrite reading,
            Func<CancellationToken, Task>? alongside = null, CancellationToken ct = default) =>
            inner.ReplaceRepositoryAsync(repository, entries, reading, alongside, ct);

        public async Task RestampAsync(string repository, string workspace, CancellationToken ct = default)
        {
            await inner.RestampAsync(repository, workspace, ct);
            await FireAsync();
        }

        public Task<IReadOnlyDictionary<string, long>> GenerationsAsync(CancellationToken ct = default) => inner.GenerationsAsync(ct);

        public Task<HeldTogether> HeldAsync(IReadOnlyCollection<string> ids, Func<KnowledgeEntry, bool> withFile, CancellationToken ct = default) =>
            inner.HeldAsync(ids, withFile, ct);

        public async Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default)
        {
            var all = await inner.AllAsync(ct);
            await FireAsync();
            return all;
        }

        public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) => inner.FindAsync(id, ct);

        public Task<IReadOnlyList<KnowledgeEntry>> OfFileAsync(string repository, string relativePath, CancellationToken ct = default) =>
            inner.OfFileAsync(repository, relativePath, ct);

        public Task<IReadOnlyDictionary<string, int>> CountByRepositoryAsync(CancellationToken ct = default) => inner.CountByRepositoryAsync(ct);

        public NewerIndexException? Refusal => inner.Refusal;
    }

    private async Task<SqliteKnowledgeStore> OpenStoreAsync() => await SqliteKnowledgeStore.OpenAsync(Database);

    [Fact]
    public async Task A_refresh_begun_before_another_connectors_write_is_refused()
    {
        await using var first = await OpenStoreAsync();
        await using var second = await OpenStoreAsync();
        var slow = new HeldSource("alpha", "Read before the other write.");
        var slower = new KnowledgeService(first, new SqliteKnowledgeSearch(first), slow);
        var quicker = new KnowledgeService(second, new SqliteKnowledgeSearch(second), new HeldSource("alpha", "Read after it."));

        slow.Hold();
        var begun = slower.RefreshAsync();
        await slow.Asked.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await quicker.RefreshAsync();
        slow.Release();
        await begun;

        Assert.Equal(["Read after it."], (await second.AllAsync()).Select(entry => entry.Body));
    }

    [Fact]
    public async Task A_retirement_racing_a_refresh_never_leaves_the_retired_repository_served()
    {
        await using var first = await OpenStoreAsync();
        await using var second = await OpenStoreAsync();
        var reading = new HeldSource("alpha", "What alpha said.");
        var refreshing = new KnowledgeService(first, new SqliteKnowledgeSearch(first), reading);
        var retiring = new KnowledgeService(
            second, new SqliteKnowledgeSearch(second), new EmptyKnowledgeSource(),
            registry: new Registry(), registrations: await RegistrationStore.OpenAsync(second.Connection));
        await retiring.RegisterAsync(Open("alpha"), Now);
        await refreshing.RefreshAsync();

        reading.Hold();
        var begun = refreshing.RefreshAsync();
        await reading.Asked.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(await retiring.RetireAsync("alpha"));
        reading.Release();
        await begun;

        Assert.DoesNotContain(await second.AllAsync(), entry => entry.Repository == "alpha");
    }

    [Fact]
    public async Task A_restamp_racing_a_refresh_never_writes_old_bodies_back()
    {
        await using var first = await OpenStoreAsync();
        await using var second = await OpenStoreAsync();
        var interleaved = new Interleaved(first);
        var moving = new KnowledgeService(
            interleaved, new SqliteKnowledgeSearch(first), new EmptyKnowledgeSource(),
            registry: new Registry(), registrations: await RegistrationStore.OpenAsync(first.Connection));
        var refreshing = new KnowledgeService(
            second, new SqliteKnowledgeSearch(second), new HeldSource("alpha", "The body as it now reads."),
            registry: new Registry(), registrations: await RegistrationStore.OpenAsync(second.Connection));
        await moving.RegisterAsync(Open("alpha"), Now);
        await first.ReplaceRepositoryAsync("alpha", [Entry("alpha", "The body as it read before.")]);

        interleaved.After = async () => await refreshing.RefreshAsync();
        await moving.RegisterAsync(Open("alpha", "elsewhere"), Now);

        Assert.Null(interleaved.After);
        var held = Assert.Single(await second.AllAsync());
        Assert.Equal("The body as it now reads.", held.Body);
        Assert.Equal("elsewhere", held.Workspace);
    }

    [Fact]
    public async Task A_fault_between_a_feeds_entries_and_its_provenance_then_a_restart_leaves_the_old_pair()
    {
        var options = new ServiceOptions(Path.Combine(_root, "unread"), Database);
        await using (var composed = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
            await composed.Service.RegisterAsync(Open("fed"), Now);
            Assert.True((await composed.Service.FeedAsync(
                "fed", [Entry("fed", "Fed first.")], new FeedProvenance("aaaa1111bbbb2222", Now, "main"))).Accepted);

            Run(Database, """
                CREATE TRIGGER fault_insert BEFORE INSERT ON feed_provenance BEGIN SELECT RAISE(ABORT, 'a fault at the provenance'); END;
                CREATE TRIGGER fault_update BEFORE UPDATE ON feed_provenance BEGIN SELECT RAISE(ABORT, 'a fault at the provenance'); END;
                """);
            await Assert.ThrowsAnyAsync<Exception>(() => composed.Service.FeedAsync(
                "fed", [Entry("fed", "Fed second.")], new FeedProvenance("cccc3333dddd4444", Now.AddHours(1), "main")));
        }

        Run(Database, "DROP TRIGGER fault_insert; DROP TRIGGER fault_update;");
        await using var restarted = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource());

        Assert.Equal(["Fed first."], (await restarted.Service.LocalEntriesAsync("fed")).Select(entry => entry.Body));
        Assert.Equal("aaaa1111bbbb2222", Assert.Single(await restarted.Service.SummarizeAsync()).Fed?.Commit);
    }

    /// <summary>
    /// An older build's connector, open before this build upgraded the file, still runs its own statements against the
    /// tables it knew. Nothing it writes reaches what this build reads (`compatible-builds`).
    /// </summary>
    [Fact]
    public async Task A_connector_opened_before_the_upgrade_cannot_write_what_this_build_reads()
    {
        await using var older = new SqliteConnection($"Data Source={Database};Pooling=False");
        await older.OpenAsync();
        Execute(older, OlderSchema);

        await using var store = await OpenStoreAsync();
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "Written by this build.")]);
        try
        {
            Execute(older, OlderReplace);
        }
        catch (SqliteException)
        {
            // The older build's tables went with the upgrade: its write fails, which is also a write that never landed.
        }

        Assert.Equal(["Written by this build."], (await store.AllAsync()).Select(entry => entry.Body));
        Assert.Empty(await new SqliteKnowledgeSearch(store).SearchAsync(new KnowledgeQuery("older")));
    }

    /// <summary>The index as the build before this one made it (schema 5), statement for statement.</summary>
    private static readonly string OlderSchema =
        $"""
        CREATE TABLE IF NOT EXISTS entries (
            id            TEXT PRIMARY KEY,
            repository    TEXT NOT NULL,
            kind          INTEGER NOT NULL,
            provenance    INTEGER NOT NULL,
            title         TEXT NOT NULL,
            body          TEXT NOT NULL,
            relative_path TEXT NOT NULL,
            anchor        TEXT,
            workspace     TEXT NOT NULL DEFAULT '{Workspaces.Default}',
            first_line    INTEGER,
            last_line     INTEGER
        );
        CREATE INDEX IF NOT EXISTS ix_entries_repository ON entries(repository);
        CREATE INDEX IF NOT EXISTS ix_entries_workspace ON entries(workspace);
        CREATE VIRTUAL TABLE IF NOT EXISTS entries_fts USING fts5(id UNINDEXED, title, body, tokenize='unicode61');
        PRAGMA user_version = 5;
        """;

    /// <summary>That build's replace of a repository, in its own transaction.</summary>
    private static readonly string OlderReplace =
        $"""
        BEGIN IMMEDIATE;
        DELETE FROM entries_fts WHERE id IN (SELECT id FROM entries WHERE repository = 'alpha');
        DELETE FROM entries WHERE repository = 'alpha';
        INSERT INTO entries (id, repository, kind, provenance, title, body, relative_path, anchor, workspace, first_line, last_line)
        VALUES ('alpha:docs/probe-lock.md', 'alpha', {(int)EntryKind.Knowledge}, {(int)Provenance.Local}, 'Probe lock', 'Written by an older build.', 'docs/probe-lock.md', NULL, '{Workspaces.Default}', NULL, NULL);
        INSERT INTO entries_fts (id, title, body) VALUES ('alpha:docs/probe-lock.md', 'Probe lock', 'Written by an older build.');
        COMMIT;
        """;

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Statements run by a connection of their own, as another process would.</summary>
    private static void Run(string database, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();
        Execute(connection, sql);
    }
}
