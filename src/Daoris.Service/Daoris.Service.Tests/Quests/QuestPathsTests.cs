using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// INTEGRATE1l (the integration design §9 point 1): an ask and a quest may carry <c>paths</c>, the repository-relative globs the work
/// declares it touches. Written by the publisher, kept in the publish operation and on the wire; an intake's draft adds to the ask's and
/// never removes them. None is every quest from before and one that declared none.
/// </summary>
public sealed class QuestPathsTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-paths-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
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
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files, asks: _asks);
        _desk = new AskDesk(service, _asks, _exchange, files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<QuestPublishOutcome> Publish(string title, params string[] paths) =>
        _exchange.PublishAsync(new QuestAsk("game", "engine", title, "Why, and the trace.") { Paths = paths }, Now);

    [Fact]
    public async Task A_publish_keeps_its_paths_once_each_in_order_and_one_declaring_none_keeps_none()
    {
        var declared = (await Publish("Cap the frame's work", " src/frame/** ", "docs/frame.md", "src/frame/**")).Quest!;
        var plain = (await Publish("Log the frame's time")).Quest!;

        Assert.Equal(["docs/frame.md", "src/frame/**"], declared.Paths);
        Assert.Empty(plain.Paths);
        Assert.Equal(["docs/frame.md", "src/frame/**"], (await _quests.FindAsync(declared.Id))!.Paths);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:/work/frame.cs")]
    [InlineData("src\\frame.cs")]
    [InlineData("src/../secrets")]
    [InlineData(" ")]
    public async Task A_path_that_is_no_repository_relative_glob_is_refused_and_nothing_is_kept(string path)
    {
        var outcome = await Publish("Cap the frame's work", "src/frame/**", path);

        Assert.Equal(QuestPublishRefusal.BadPaths, outcome.Refusal);
        Assert.Empty(await _quests.ListAsync(receiver: "engine"));
    }

    [Fact]
    public async Task Paths_are_in_the_history_and_cross_the_wire_and_a_quest_declaring_none_writes_none()
    {
        var declared = (await Publish("Cap the frame's work", "src/frame/**")).Quest!;
        var plain = (await Publish("Log the frame's time")).Quest!;
        var history = await _quests.HistoryAsync(declared.Id);

        Assert.Equal(["src/frame/**"], QuestLog.Replay(history)!.Paths);
        Assert.Equal(["src/frame/**"], QuestWire.ReadPush(QuestWire.Push(0, history))!.Value.Operations[0].Published!.Paths);
        Assert.DoesNotContain("\"paths\"", QuestWire.Push(0, await _quests.HistoryAsync(plain.Id)));
    }

    [Fact]
    public void A_pushed_quest_whose_paths_no_publish_would_keep_is_not_whole()
    {
        const string json = """{"base":0,"operations":[{"machine":"m","sequence":1,"quest":"abcdefabcdef","kind":"published","at":"2026-10-31T09:00:00Z","asked":{"from":"game","to":"engine","title":"t","body":"b","paths":["../up"],"links":[],"attachments":[],"then":[]}}]}""";

        Assert.Null(QuestWire.ReadPush(json));
        Assert.Null(QuestWire.ReadPush(json.Replace("[\"../up\"]", "[7]")));
        Assert.NotNull(QuestWire.ReadPush(json.Replace("../up", "src/up/**")));
    }

    /// <summary>A store from before the field has no column: it gains one, and its quests declare none.</summary>
    [Fact]
    public async Task A_store_from_before_the_paths_opens_and_its_quests_declare_none()
    {
        await using var older = new SqliteConnection("Data Source=:memory:");
        await older.OpenAsync();
        var store = await QuestStore.OpenAsync(older);
        await store.PublishAsync("game", "engine", "Cap the frame's work", "b", Now);
        await using (var drop = older.CreateCommand())
        {
            drop.CommandText = "ALTER TABLE quests DROP COLUMN paths";
            await drop.ExecuteNonQueryAsync();
        }

        var reopened = await QuestStore.OpenAsync(older);

        Assert.Empty(Assert.Single(await reopened.ListAsync(receiver: "engine")).Paths);
    }

    /// <summary>
    /// The person's declaration on the ask stands whatever the intake's draft says: an agent's own declaration adds paths and never
    /// removes another's, so it cannot release the hold the person's paths make.
    /// </summary>
    [Fact]
    public async Task An_intakes_draft_adds_its_paths_to_the_asks_and_never_removes_them()
    {
        var ask = (await _desk.AskAsync(new AskRequest("default", "cap the frame's work") { Paths = ["src/frame/**"] }, Now)).Ask!;
        Assert.Equal(["src/frame/**"], ask.Paths);
        Assert.Equal(["src/frame/**"], (await _asks.FindAsync(ask.Id))!.Paths);

        var published = await _desk.PublishAsync(
            ask.Id, "engine", Now, draft: new AskDraft("Cap the frame", "Cap its work.") { Paths = ["docs/frame.md"] });

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal(["docs/frame.md", "src/frame/**"], published.Quest!.Paths);
    }

    [Fact]
    public async Task An_ask_whose_paths_are_no_repository_relative_globs_is_refused()
    {
        var outcome = await _desk.AskAsync(new AskRequest("default", "cap the frame's work") { Paths = ["../elsewhere"] }, Now);

        Assert.Equal(AskRefusal.BadPaths, outcome.Refusal);
        Assert.Null(outcome.Ask);
    }
}
