using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1x: a quest carries the mark its publisher or its intake put on the work, <c>design</c> where it is design or decision
/// work. Kept with the quest, in its publish operation and on the wire; none is every quest from before and an unmarked one.
/// </summary>
public sealed class QuestWeightTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-weight-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-30T09:00:00Z");

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
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(_service, _quests, files: files, asks: _asks);
        _desk = new AskDesk(_service, _asks, _exchange, files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<QuestPublishOutcome> Publish(string title, string? weight) =>
        _exchange.PublishAsync(new QuestAsk("game", "engine", title, "Why, and the trace.") { Weight = weight }, Now);

    [Fact]
    public async Task A_marked_publish_keeps_its_weight_and_an_unmarked_one_keeps_none()
    {
        var marked = (await Publish("Choose how the frame is capped", " design ")).Quest!;
        var plain = (await Publish("Cap the frame's work", null)).Quest!;

        Assert.Equal("design", marked.Weight);
        Assert.Null(plain.Weight);
        Assert.Equal("design", (await _quests.FindAsync(marked.Id))!.Weight);
        Assert.Null((await _quests.FindAsync(plain.Id))!.Weight);
    }

    [Fact]
    public async Task A_weight_that_is_neither_design_nor_exact_is_refused_and_nothing_is_kept()
    {
        var outcome = await Publish("Cap the frame's work", "heavy");

        Assert.Equal(QuestPublishRefusal.BadWeight, outcome.Refusal);
        Assert.Empty(await _quests.ListAsync(receiver: "engine"));
    }

    [Fact]
    public async Task A_weight_is_in_the_history_and_crosses_the_wire_and_an_unmarked_one_writes_none()
    {
        var marked = (await Publish("Choose how the frame is capped", "design")).Quest!;
        var plain = (await Publish("Cap the frame's work", null)).Quest!;
        var history = await _quests.HistoryAsync(marked.Id);

        Assert.Equal("design", QuestLog.Replay(history)!.Weight);
        Assert.Equal("design", QuestWire.ReadPush(QuestWire.Push(0, history))!.Value.Operations[0].Published!.Weight);
        Assert.DoesNotContain("\"weight\"", QuestWire.Push(0, await _quests.HistoryAsync(plain.Id)));
    }

    [Fact]
    public void A_pushed_quest_whose_weight_no_publish_would_keep_is_not_whole()
    {
        var json = """{"base":0,"operations":[{"machine":"m","sequence":1,"quest":"abcdefabcdef","kind":"published","at":"2026-10-30T09:00:00Z","asked":{"from":"game","to":"engine","title":"t","body":"b","weight":"heavy","links":[],"attachments":[],"then":[]}}]}""";

        Assert.Null(QuestWire.ReadPush(json));
        Assert.NotNull(QuestWire.ReadPush(json.Replace("heavy", "design")));
    }

    /// <summary>A store from before the field has no column: it gains one, and its quests are unmarked.</summary>
    [Fact]
    public async Task A_store_from_before_the_weight_opens_and_its_quests_are_unmarked()
    {
        await using var older = new SqliteConnection("Data Source=:memory:");
        await older.OpenAsync();
        var store = await QuestStore.OpenAsync(older);
        await store.PublishAsync("game", "engine", "Cap the frame's work", "b", Now);
        await using (var drop = older.CreateCommand())
        {
            drop.CommandText = "ALTER TABLE quests DROP COLUMN weight";
            await drop.ExecuteNonQueryAsync();
        }

        var reopened = await QuestStore.OpenAsync(older);

        Assert.Null(Assert.Single(await reopened.ListAsync(receiver: "engine")).Weight);
    }

    /// <summary>The intake marks through its draft, as it gives a short title; the desk has no rule of its own for design.</summary>
    [Fact]
    public async Task An_intakes_draft_carries_its_weight()
    {
        var ask = (await _desk.AskAsync(new AskRequest("default", "decide how the frame is capped"), Now)).Ask!;
        var published = await _desk.PublishAsync(
            ask.Id, "engine", Now, draft: new AskDraft("Decide the frame cap", "Which way to cap it.") { Weight = "design" });

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal("design", published.Quest!.Weight);
    }
}
