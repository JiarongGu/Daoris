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
    private async Task OpenAsync()
    {
        _quests = await QuestStore.OpenAsync(_connection);
        _exchange = new QuestExchange(_service, _quests, remotes: _remotes);
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> PendingAsync()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM quest_take_binds";
        return (long)(await command.ExecuteScalarAsync())!;
    }

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

    /// <summary>A machine's remotes map, read when asked, which the test wires after the graph was named.</summary>
    private sealed class Remotes : IRemotes
    {
        public bool Wired { get; set; }

        public IRemote? For(string workspace) => Wired ? new UnreachableRemote() : null;
    }
}
