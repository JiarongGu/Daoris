using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// INTEGRATE1p (the integration design §17.5, R5's C32): a workflow's <c>request</c> node publishes its quest under the request's key,
/// <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>. The key widens the quest's id, as a chain step's parent does, so one transaction dedups: two runs
/// asking in the same words make two quests, and the same key sent again, after a restart or a lost answer, is its own quest back.
/// </summary>
public sealed class QuestRequestKeyTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-request-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-31T09:00:00Z");

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "engine", "game" })
        {
            var dir = Path.Combine(_root, "family", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""
                { "source": "s", "packs": [], "domain": { "summary": "the {{name}}", "owns": ["o"], "accepts": ["a"] } }
                """);
        }

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        _exchange = new QuestExchange(service, _quests, files: new QuestFiles(Path.Combine(_root, "home")));
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<QuestPublishOutcome> Ask(string? request) =>
        _exchange.PublishAsync(new QuestAsk("game", "engine", "Expose the frame cap", "The game needs it to pace.") { Request = request }, Now);

    /// <summary>C32: identical request words in two runs make distinct quests, and neither is the plain ask's.</summary>
    [Fact]
    public async Task Identical_words_from_two_runs_make_two_quests()
    {
        var first = (await Ask("run-a/ask-engine/1")).Quest!;
        var second = (await Ask("run-b/ask-engine/1")).Quest!;
        var plain = (await Ask(null)).Quest!;

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Id, plain.Id);
        Assert.Equal("run-a/ask-engine/1", first.Request);
        Assert.Equal("run-b/ask-engine/1", second.Request);
        Assert.Null(plain.Request);
        Assert.Equal(3, (await _quests.ListAsync(receiver: "engine")).Count);
    }

    /// <summary>C32: restarting either run sends its key again and gets its own quest back, whatever became of it since.</summary>
    [Fact]
    public async Task A_restarted_run_sends_its_key_again_and_reuses_its_own_quest()
    {
        var first = (await Ask("run-a/ask-engine/1")).Quest!;
        var other = (await Ask("run-b/ask-engine/1")).Quest!;
        await _quests.MoveAsync(first.Id, QuestStatus.Taken, null, Now.AddMinutes(1));

        var again = (await Ask("run-a/ask-engine/1")).Quest!;

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(QuestStatus.Taken, again.Status);
        Assert.NotEqual(other.Id, again.Id);
    }

    /// <summary>C32: a publish whose answer was lost is sent again under the same key, and the store holds one quest, never two.</summary>
    [Fact]
    public async Task A_lost_publish_answer_sent_again_makes_no_duplicate()
    {
        var sent = (await Ask("run-a/ask-engine/1")).Quest!;
        var resent = (await Ask("run-a/ask-engine/1")).Quest!;

        Assert.Equal(sent.Id, resent.Id);
        Assert.Single(await _quests.ListAsync(receiver: "engine"));
        Assert.Single(await _quests.HistoryAsync(sent.Id));
    }

    /// <summary>An intentional new publication of the node takes the next number, and is a quest of its own.</summary>
    [Fact]
    public async Task The_next_number_of_one_node_is_a_new_quest()
    {
        var first = (await Ask("run-a/ask-engine/1")).Quest!;
        var next = (await Ask("run-a/ask-engine/2")).Quest!;

        Assert.NotEqual(first.Id, next.Id);
    }

    [Theory]
    [InlineData("run-a/ask-engine")]
    [InlineData("run-a/ask-engine/0")]
    [InlineData("run-a/ask-engine/x")]
    [InlineData("run a/ask-engine/1")]
    [InlineData("/ask-engine/1")]
    public async Task A_key_that_is_no_run_node_and_number_is_refused(string request)
    {
        var outcome = await Ask(request);

        Assert.Equal(QuestPublishRefusal.BadRequest, outcome.Refusal);
        Assert.Empty(await _quests.ListAsync(receiver: "engine"));
    }

    [Fact]
    public async Task The_key_is_in_the_history_and_crosses_the_wire_and_a_plain_quest_writes_none()
    {
        var keyed = (await Ask("run-a/ask-engine/1")).Quest!;
        var plain = (await Ask(null)).Quest!;
        var history = await _quests.HistoryAsync(keyed.Id);

        Assert.Equal("run-a/ask-engine/1", QuestLog.Replay(history)!.Request);
        Assert.Equal("run-a/ask-engine/1", QuestWire.ReadPush(QuestWire.Push(0, history))!.Value.Operations[0].Published!.Request);
        Assert.DoesNotContain("\"request\"", QuestWire.Push(0, await _quests.HistoryAsync(plain.Id)));
        Assert.Equal("run-a/ask-engine/1", (await _quests.FindAsync(keyed.Id))!.Request);
    }

    /// <summary>The forgotten-id check reads the key too: a cleared request's own words under its key are refused, another key's are not.</summary>
    [Fact]
    public async Task The_forgotten_id_check_reads_the_key()
    {
        var keyed = (await Ask("run-a/ask-engine/1")).Quest!;

        Assert.Null(await _quests.ForgottenIdAsync("game", "engine", "Expose the frame cap", null, default, request: "run-a/ask-engine/1"));
        // As a clear does where a remote numbered it (HIST1b): its id kept as forgotten.
        await using (var forget = _connection.CreateCommand())
        {
            forget.CommandText = "INSERT INTO quest_forgotten (id, at) VALUES ($id, '2026-10-31T10:00:00Z')";
            forget.Parameters.AddWithValue("$id", keyed.Id);
            await forget.ExecuteNonQueryAsync();
        }

        Assert.Equal(keyed.Id, await _quests.ForgottenIdAsync("game", "engine", "Expose the frame cap", null, default, request: "run-a/ask-engine/1"));
        Assert.Null(await _quests.ForgottenIdAsync("game", "engine", "Expose the frame cap", null, default, request: "run-b/ask-engine/1"));
        Assert.Null(await _quests.ForgottenIdAsync("game", "engine", "Expose the frame cap", null, default));
        Assert.Equal(QuestPublishRefusal.Cleared, (await Ask("run-a/ask-engine/1")).Refusal);
    }
}
