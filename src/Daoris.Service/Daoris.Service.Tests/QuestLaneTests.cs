using System.Security.Cryptography;
using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A quest addresses a lane (D115 §2.2, DEV4): `repository:lane`, or `repository:lane+lane`. The
/// exchange splits the address once; the quest keeps `to` as the repository, so everything keyed on a
/// repository goes on reading one, and gains `lanes`. The registration's lanes are what the address is
/// judged by, and a quest from a repository to one of its own lanes is work for another session, so it
/// is allowed where one to itself with no lane is not.
/// </summary>
public sealed class QuestLaneTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-lanes-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestExchange _exchange = null!;
    private KnowledgeService _service = null!;
    private QuestStore _quests = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-01T10:00:00Z");

    public async Task InitializeAsync()
    {
        // One repository that declares two lanes, one that declares none — written as manifests and
        // imported, as the registry is made, and the lanes then sent as `connect` sends them.
        foreach (var name in new[] { "engine", "game" })
        {
            var dir = Path.Combine(_root, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""
                { "source": "s", "packs": [], "domain": { "summary": "the {{name}}", "owns": ["o"], "accepts": ["a"] } }
                """);
        }

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, Now);

        var engine = (await _service.RegistryAsync()).Named("engine")!;
        await _service.RegisterAsync(
            engine with { Lanes = [new("core", "Core", "The runtime."), new("assets", "Assets", "The pipeline.")] }, Now);

        _exchange = new QuestExchange(_service, _quests);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<QuestPublishOutcome> Publish(string to, string from = "game", string title = "Cap the frame's work") =>
        _exchange.PublishAsync(from, to, title, "It runs unbounded; here is the trace.", Now);

    [Fact]
    public async Task A_quest_to_a_declared_lane_is_the_repositorys_with_its_lane()
    {
        var outcome = await Publish("engine:core");

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Equal("engine", outcome.Quest!.To);
        Assert.Equal(["core"], outcome.Quest.Lanes);
        Assert.Contains("to `engine:core`", outcome.Message);

        var held = Assert.Single(await _quests.ListAsync(receiver: "engine"));
        Assert.Equal(["core"], held.Lanes);
    }

    /// <summary>
    /// Several lanes are one quest whichever order they are named in, spelled as the registration
    /// spells them, each once.
    /// </summary>
    [Fact]
    public async Task Several_lanes_are_kept_sorted_once_each_and_in_their_declared_spelling()
    {
        var first = await Publish("engine:assets+CORE+core");
        var again = await Publish("engine:core+assets");

        Assert.Equal(["assets", "core"], first.Quest!.Lanes);
        Assert.Equal(first.Quest.Id, again.Quest!.Id);
    }

    /// <summary>
    /// The id widens only when there are lanes (D115 §2.2), as it did for a chain's parent: every quest
    /// published before keeps the id it was quoted by, and a lane's quest is not the repository's.
    /// </summary>
    [Fact]
    public async Task The_id_widens_only_with_lanes()
    {
        var whole = await Publish("engine");
        var laned = await Publish("engine:core");

        var before = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("game->engine:Cap the frame's work")))[..12]
            .ToLowerInvariant();
        Assert.Equal(before, whole.Quest!.Id);
        Assert.Empty(whole.Quest.Lanes);
        Assert.NotEqual(whole.Quest.Id, laned.Quest!.Id);
        Assert.Equal(QuestStore.MakeId("game", "engine", "Cap the frame's work", lanes: ["core"]), laned.Quest.Id);
    }

    /// <summary>A lane nobody declared is refused, naming the lanes there are and how to address one.</summary>
    [Fact]
    public async Task A_lane_the_registration_does_not_declare_is_refused_naming_its_lanes()
    {
        var outcome = await Publish("engine:nope");

        Assert.Equal(QuestPublishRefusal.UnknownLane, outcome.Refusal);
        Assert.Null(outcome.Quest);
        Assert.Contains("`nope`", outcome.Message);
        Assert.Contains("`core`", outcome.Message);
        Assert.Contains("`assets`", outcome.Message);
        Assert.Empty(await _quests.ListAsync());
    }

    [Theory]
    [InlineData("engine:")]
    [InlineData("engine:+")]
    public async Task An_address_that_names_no_lane_after_its_colon_is_refused_naming_its_lanes(string to)
    {
        var outcome = await Publish(to);

        Assert.Equal(QuestPublishRefusal.UnknownLane, outcome.Refusal);
        Assert.Contains("`core`", outcome.Message);
        Assert.Contains("`assets`", outcome.Message);
    }

    /// <summary>A lane on a repository that declares none is refused, saying to address the repository.</summary>
    [Fact]
    public async Task A_lane_on_a_repository_that_declares_none_is_refused_saying_to_address_the_repository()
    {
        var outcome = await Publish("game:core", from: "engine");

        Assert.Equal(QuestPublishRefusal.UnknownLane, outcome.Refusal);
        Assert.Contains("declares no lanes", outcome.Message);
        Assert.Contains("`game`", outcome.Message);
        Assert.Empty(await _quests.ListAsync());
    }

    /// <summary>
    /// D115 amends the exchange's self-address refusal: work for another lane is work for another
    /// session, so a repository may ask one of its own lanes. To itself with no lane is still refused,
    /// with the sentence it always had.
    /// </summary>
    [Fact]
    public async Task A_repository_may_ask_its_own_lane_and_never_itself_whole()
    {
        var whole = await Publish("engine", from: "engine");
        Assert.Equal(QuestPublishRefusal.SelfAddressed, whole.Refusal);
        Assert.Equal("That is the repository you are in — a quest is work for someone else. Use its own backlog.", whole.Message);

        var own = await Publish("engine:core", from: "engine");
        Assert.Equal(QuestPublishRefusal.None, own.Refusal);
        Assert.Equal(("engine", "engine"), (own.Quest!.From, own.Quest.To));
        Assert.Equal(["core"], own.Quest.Lanes);

        var unknown = await Publish("engine:nope", from: "engine");
        Assert.Equal(QuestPublishRefusal.UnknownLane, unknown.Refusal);
    }

    /// <summary>A quest's lanes survive its history: the log replays them, and the wire carries them.</summary>
    [Fact]
    public async Task A_quests_lanes_are_in_its_history_and_cross_the_wire()
    {
        var quest = (await Publish("engine:assets+core")).Quest!;
        var history = await _quests.HistoryAsync(quest.Id);

        Assert.Equal(["assets", "core"], QuestLog.Replay(history)!.Lanes);

        var pushed = QuestWire.ReadPush(QuestWire.Push(0, history))!.Value;
        Assert.Equal(["assets", "core"], pushed.Operations[0].Published!.Lanes);

        var whole = (await Publish("engine", title: "Whole")).Quest!;
        Assert.DoesNotContain("lanes", QuestWire.Push(0, await _quests.HistoryAsync(whole.Id)));
    }

    /// <summary>
    /// A remote keeps a pushed quest's lanes as its machine's exchange judged them, but a lane no
    /// registration could declare is not a lane, and a record naming one would lie.
    /// </summary>
    [Fact]
    public async Task A_remote_refuses_a_pushed_quest_whose_lane_no_registration_could_declare()
    {
        var registered = await _service.RegistryAsync();
        var asked = new Quest("abcdefabcdef", "game", "engine", "t", "b", QuestStatus.Open, null, Now, Now);

        Assert.Null(_exchange.JudgeReceived(asked with { Lanes = ["core"] }, registered));
        Assert.Contains("lane", _exchange.JudgeReceived(asked with { Lanes = ["Not A Lane"] }, registered));
    }
}
