using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-CLOSE1o (the fabric events design §E5's cutover; D173's CLOSE1b note, *After review*, choice 2): the closure graph the driver
/// names for an open quest's take is owned by the session it was named for and the publication it was named on, expires, and is judged
/// and consumed inside the take's own write, with the effective remote read there. A store from before the owner was kept is upgraded,
/// and its pending rows, whose owner cannot be shown, are discarded. Only the valid case appends Bound, with the Taken, in one write.
/// </summary>
public sealed class QuestTakeBindTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-takebind-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private KnowledgeService _service = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private readonly Remotes _remotes = new();
    private Quest _open = null!;

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T09:00:00Z");
    private const string Graph = "gph-0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";
    private const string Session = "ses-takes-its-quest";
    private const string Title = "Write the bridge report";

    /// <summary>The quest table's bind rows as a build before FABRIC-CLOSE1o made them: a quest id, a graph, a moment, and no owner.</summary>
    private const string OldSchema = """
        DROP TABLE quest_take_binds;
        CREATE TABLE quest_take_binds (
          id    TEXT PRIMARY KEY,
          graph TEXT NOT NULL,
          at    TEXT NOT NULL
        );
        """;

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);

        await OpenAsync();
        _open = (await _exchange.PublishAsync("checker", "reports", Title, "Through the bridge.", Now)).Quest!;
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    /// <summary>The store opened over the connection as a host opens it, upgrading what it finds, and an exchange over it.</summary>
    private async Task OpenAsync(TimeProvider? time = null)
    {
        _quests = await QuestStore.OpenAsync(_connection, time: time);
        _exchange = new QuestExchange(_service, _quests, remotes: _remotes);
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The pending binds, in the table builds before FABRIC-CLOSE1w write and in the one this build owns its binds in.</summary>
    private async Task<long> PendingAsync()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM quest_take_binds) + (SELECT COUNT(*) FROM quest_owned_take_binds)";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<object?> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync();
    }

    /// <summary>How a build before FABRIC-CLOSE1o named a graph for a take: the quest's id, its graph and a moment, upserted.</summary>
    private const string OlderNaming = "INSERT INTO quest_take_binds (id, graph, at) VALUES ($id, $graph, $at) "
        + "ON CONFLICT(id) DO UPDATE SET graph = excluded.graph, at = excluded.at";

    /// <summary>How a build before FABRIC-CLOSE1w read the graph at a take: whatever row the quest's id names, bound by the id alone.</summary>
    private const string OlderTake = "DELETE FROM quest_take_binds WHERE id = $id RETURNING graph";

    private const string OtherGraph = "gph-1111111111111111111111111111111111111111111111111111111111111111";

    [Theory]
    [InlineData("valid")]
    [InlineData("legacy owner")]
    [InlineData("wrong session")]
    [InlineData("replaced publication")]
    [InlineData("expiry crossed while waiting")]
    [InlineData("remote enabled since")]
    public async Task An_upgraded_store_binds_a_take_only_for_the_session_and_publication_it_was_named_for_before_it_expires_and_off_a_remote(
        string variant)
    {
        // A store from before the owner was kept, holding one pending row that names only the quest, as CLOSE1l wrote it.
        await ExecuteAsync(OldSchema);
        if (variant == "legacy owner")
        {
            await ExecuteAsync(
                "INSERT INTO quest_take_binds (id, graph, at) VALUES ($id, $graph, $at)",
                ("$id", _open.Id), ("$graph", Graph), ("$at", Now.ToString("O")));
        }

        await OpenAsync();
        // The upgrade discards a row whose owner cannot be shown, and keeps the quest's history whole.
        Assert.Equal(0, await PendingAsync());
        Assert.Equal([QuestOperationKind.Published], (await _quests.HistoryAsync(_open.Id)).Select(op => op.Kind));

        if (variant != "legacy owner")
        {
            Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(_open.Id, Graph, Now, atTake: true, session: Session)).Refusal);
            Assert.Equal(1, await PendingAsync());
        }

        var taker = Session;
        var at = Now.AddMinutes(1);
        switch (variant)
        {
            case "wrong session":
                taker = "ses-another-session";
                break;
            case "replaced publication":
                Assert.Equal(QuestDeleteRefusal.None, (await _exchange.DeleteAsync(_open.Id, Now)).Refusal);
                Assert.Equal(_open.Id, (await _exchange.PublishAsync("checker", "reports", Title, "Through the bridge.", Now.AddSeconds(30))).Quest!.Id);
                break;
            case "expiry crossed while waiting":
                at = Now + QuestStore.TakeBindLife + TimeSpan.FromSeconds(1);
                break;
            case "remote enabled since":
                _remotes.Wired = true;
                break;
        }

        var take = await _exchange.RespondAsync(_open.Id, "take", null, at, session: taker);

        Assert.Equal(QuestRespondRefusal.None, take.Refusal);
        var kinds = (await _quests.HistoryAsync(_open.Id)).Select(op => op.Kind).ToList();
        // Consumed in the take's own write, whatever it made of it.
        Assert.Equal(0, await PendingAsync());
        if (variant == "valid")
        {
            Assert.Equal([QuestOperationKind.Taken, QuestOperationKind.Bound], kinds.TakeLast(2));
            Assert.Equal(Graph, take.Quest!.Closure);
            Assert.Contains("`ready`", take.Message, StringComparison.Ordinal);
            return;
        }

        Assert.Equal(QuestOperationKind.Taken, kinds[^1]);
        Assert.DoesNotContain(QuestOperationKind.Bound, kinds);
        Assert.Null(take.Quest!.Closure);
        Assert.DoesNotContain("`ready`", take.Message, StringComparison.Ordinal);
        if (variant == "expiry crossed while waiting")
        {
            // Said once, in the take's own answer: the session told `ready` at its start finishes with `done`.
            Assert.Single(take.Message.Split("expired", StringSplitOptions.None).Skip(1));
            Assert.Contains("`done`", take.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_ordinary_take_with_no_graph_named_is_exactly_as_before()
    {
        // The owner's install: nothing named, so the take is today's, word for word, whichever session takes it.
        var take = await _exchange.RespondAsync(_open.Id, "take", null, Now, session: Session);

        Assert.Equal(QuestRespondRefusal.None, take.Refusal);
        Assert.Equal($"Quest `#{_open.Id}` is now Taken.", take.Message);
        Assert.Equal([QuestOperationKind.Published, QuestOperationKind.Taken], (await _quests.HistoryAsync(_open.Id)).Select(op => op.Kind));
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(_open.Id, "done", "Built.", Now)).Refusal);
    }

    [Fact]
    public async Task An_older_build_writes_into_the_upgraded_table_and_its_unowned_row_never_binds()
    {
        // A build before FABRIC-CLOSE1o names its own columns only: the owner's are nullable, so its write lands, and owns nothing.
        await ExecuteAsync(
            "INSERT INTO quest_take_binds (id, graph, at) VALUES ($id, $graph, $at) "
            + "ON CONFLICT(id) DO UPDATE SET graph = excluded.graph, at = excluded.at",
            ("$id", _open.Id), ("$graph", Graph), ("$at", Now.ToString("O")));

        var take = await _exchange.RespondAsync(_open.Id, "take", null, Now.AddMinutes(1), session: Session);

        Assert.Equal(QuestRespondRefusal.None, take.Refusal);
        Assert.DoesNotContain(await _quests.HistoryAsync(_open.Id), op => op.Kind == QuestOperationKind.Bound);
        Assert.Equal(0, await PendingAsync());

        // And the upgrade runs again over an upgraded store, changing nothing.
        await OpenAsync();
        Assert.Equal(QuestOperationKind.Taken, (await _quests.HistoryAsync(_open.Id))[^1].Kind);
    }

    [Fact]
    public async Task A_bind_at_the_take_that_names_no_session_is_refused_and_keeps_nothing()
    {
        var refused = await _exchange.BindAsync(_open.Id, Graph, Now, atTake: true);

        Assert.Equal(QuestRespondRefusal.NotInGraph, refused.Refusal);
        Assert.Contains("session", refused.Message, StringComparison.Ordinal);
        Assert.Equal(0, await PendingAsync());
    }

    /// <summary>
    /// FABRIC-CLOSE1v: the plain bind, on a quest already taken, reads the same remote map the take's bind reads, so a workspace that syncs
    /// through a remote is refused it and its quest closes by its done. Off a remote, the same bind lands, as it did.
    /// </summary>
    [Fact]
    public async Task A_plain_bind_in_a_workspace_with_a_remote_is_refused_and_appends_nothing()
    {
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(_open.Id, "take", null, Now)).Refusal);
        _remotes.Wired = true;

        var refused = await _exchange.BindAsync(_open.Id, Graph, Now);

        Assert.Equal(QuestRespondRefusal.NotInGraph, refused.Refusal);
        Assert.Contains("remote", refused.Message, StringComparison.Ordinal);
        Assert.Contains("closes by its done", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(await _quests.HistoryAsync(_open.Id), op => op.Kind == QuestOperationKind.Bound);
        Assert.Null((await _quests.FindAsync(_open.Id))!.Closure);

        _remotes.Wired = false;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(_open.Id, Graph, Now)).Refusal);
        Assert.Equal(Graph, (await _quests.FindAsync(_open.Id))!.Closure);
    }

    /// <summary>
    /// FABRIC-CLOSE1w: a take asked before the bind's expiry that queues behind another write until past it is judged at the moment its
    /// own write began, so it binds nothing, saying the bind expired, as a take asked late does.
    /// </summary>
    [Fact]
    public async Task A_take_that_waits_for_its_write_past_the_expiry_binds_nothing()
    {
        var time = new WaitingTime();
        await OpenAsync(time);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(_open.Id, Graph, Now, atTake: true, session: Session)).Refusal);

        // Another request holds the store's write; the take, asked a minute after the naming, queues behind it past the bind's life.
        var release = new TaskCompletionSource();
        var holding = ConnectionGate.For(_connection).RunAsync(() => release.Task, CancellationToken.None);
        var asked = _exchange.RespondAsync(_open.Id, "take", null, Now.AddMinutes(1), session: Session);
        time.Pass(QuestStore.TakeBindLife);
        release.SetResult();
        await holding;
        var take = await asked;

        Assert.Equal(QuestRespondRefusal.None, take.Refusal);
        Assert.Equal(QuestOperationKind.Taken, (await _quests.HistoryAsync(_open.Id))[^1].Kind);
        Assert.DoesNotContain(await _quests.HistoryAsync(_open.Id), op => op.Kind == QuestOperationKind.Bound);
        Assert.Null(take.Quest!.Closure);
        Assert.Contains("expired", take.Message, StringComparison.Ordinal);
        Assert.Equal(0, await PendingAsync());
    }

    /// <summary>
    /// FABRIC-CLOSE1w: a build before FABRIC-CLOSE1o upserts a graph for the quest's id alone, so over a bind this build named it once
    /// replaced the graph and kept the owner, and the owner's take bound the older build's graph. Its write now never reaches an owned
    /// bind: the owner's take binds the graph named for it, and the older row is consumed with it, owning nothing.
    /// </summary>
    [Fact]
    public async Task An_older_builds_naming_never_replaces_the_graph_an_owned_bind_names()
    {
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(_open.Id, Graph, Now, atTake: true, session: Session)).Refusal);
        await ExecuteAsync(OlderNaming, ("$id", _open.Id), ("$graph", OtherGraph), ("$at", Now.AddSeconds(30).ToString("O")));

        var take = await _exchange.RespondAsync(_open.Id, "take", null, Now.AddMinutes(1), session: Session);

        Assert.Equal(QuestRespondRefusal.None, take.Refusal);
        Assert.Equal(
            [QuestOperationKind.Taken, QuestOperationKind.Bound], (await _quests.HistoryAsync(_open.Id)).Select(op => op.Kind).TakeLast(2));
        Assert.Equal(Graph, take.Quest!.Closure);
        Assert.Equal(0, await PendingAsync());
    }

    /// <summary>
    /// FABRIC-CLOSE1w: a build before this one takes by reading the row the quest's id names and binding its graph, whoever takes and
    /// however late. An owned bind is never in the table it reads, so its take finds nothing to bind, and the bind still waits for the
    /// session it was named for.
    /// </summary>
    [Fact]
    public async Task An_older_builds_take_finds_no_owned_bind_to_bind_by_the_id_alone()
    {
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(_open.Id, Graph, Now, atTake: true, session: Session)).Refusal);

        Assert.Null(await ScalarAsync(OlderTake, ("$id", _open.Id)));

        Assert.Equal(1, await PendingAsync());
        var take = await _exchange.RespondAsync(_open.Id, "take", null, Now.AddMinutes(1), session: Session);
        Assert.Equal(Graph, take.Quest!.Closure);
    }

    /// <summary>
    /// FABRIC-CLOSE1w: the owner's install names no graph. Opened by this build, a store holds no bind, its take is today's word for
    /// word, and an older build's own naming and take over it land and read as they always did, refused nothing.
    /// </summary>
    [Fact]
    public async Task An_unarmed_store_is_unchanged_for_its_own_take_and_for_an_older_build()
    {
        await OpenAsync();
        Assert.Equal(0, await PendingAsync());

        await ExecuteAsync(OlderNaming, ("$id", _open.Id), ("$graph", Graph), ("$at", Now.ToString("O")));
        Assert.Equal(Graph, await ScalarAsync(OlderTake, ("$id", _open.Id)));

        var take = await _exchange.RespondAsync(_open.Id, "take", null, Now, session: Session);
        Assert.Equal(QuestRespondRefusal.None, take.Refusal);
        Assert.Equal($"Quest `#{_open.Id}` is now Taken.", take.Message);
        Assert.Equal([QuestOperationKind.Published, QuestOperationKind.Taken], (await _quests.HistoryAsync(_open.Id)).Select(op => op.Kind));
        Assert.Equal(0, await PendingAsync());
    }

    /// <summary>A monotonic clock the test moves, standing for the time a take spent queued for its write.</summary>
    private sealed class WaitingTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Pass(TimeSpan span) => Interlocked.Add(ref _ticks, span.Ticks);
    }

    /// <summary>A machine's remotes map, read when asked, which the test wires after the graph was named.</summary>
    private sealed class Remotes : IRemotes
    {
        public bool Wired { get; set; }

        public IRemote? For(string workspace) => Wired ? new UnreachableRemote() : null;
    }
}
