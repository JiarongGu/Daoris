using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The publish/respond judgement — who may be addressed, what a refusal says, what declining
/// requires — lives in ONE place, because there are two hosts. Written per host it would drift, and
/// one host would end up refusing what the other accepts, which for a quest system is the worst
/// available bug: the same ask deliverable or not depending on which door it came through.
/// </summary>
public sealed class QuestExchangeTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-exchange-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestExchange _exchange = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-18T10:00:00Z");

    private sealed class NoSource : IKnowledgeSource
    {
        public string Name => "test";
        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>([]);
    }

    public async Task InitializeAsync()
    {
        // The family this exchange sees: one declared adopter, one adopter that has said nothing,
        // and one repository that has not adopted at all.
        Repo("Declared", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Owns the engine.", "owns": ["rendering"], "accepts": ["a bug"] }
            }
            """);
        Repo("Quiet", """{ "source": "s", "packs": [] }""");
        Repo("Stranger", null);

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new NoSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry(_root));

        _exchange = new QuestExchange(service, quests);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string? manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        if (manifest is not null) File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    private Task<QuestPublishOutcome> Publish(string to, string from = "Asker") =>
        _exchange.PublishAsync(from, to, "Do the thing", "Here is why, with evidence.", Now);

    [Fact]
    public async Task Publishing_to_yourself_is_refused()
    {
        var outcome = await Publish("Asker");

        Assert.Equal(QuestPublishRefusal.SelfAddressed, outcome.Refusal);
        Assert.Null(outcome.Quest);
    }

    /// <summary>
    /// A quest for a repository with no client has nobody to read it — and the refusal must name who
    /// CAN be asked, because "no" with no alternative leaves the asker exactly where they started.
    /// </summary>
    [Fact]
    public async Task Publishing_to_a_non_adopter_is_refused_and_names_the_addressable()
    {
        var outcome = await Publish("Stranger");

        Assert.Equal(QuestPublishRefusal.NotAddressable, outcome.Refusal);
        Assert.Contains("Declared", outcome.Addressable);
        Assert.Contains("Quiet", outcome.Addressable);
        Assert.DoesNotContain("Stranger", outcome.Addressable);
        Assert.Contains("has not adopted", outcome.Message);
    }

    /// <summary>Adoption gates addressing; declaration does not — but the asker is warned (D34).</summary>
    [Fact]
    public async Task Publishing_to_an_undeclared_adopter_succeeds_with_a_caution()
    {
        var outcome = await Publish("Quiet");

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Equal(QuestStatus.Open, outcome.Quest!.Status);
        Assert.Contains("has not declared", outcome.Message);
    }

    [Fact]
    public async Task Publishing_to_a_declared_adopter_is_clean()
    {
        var outcome = await Publish("Declared");

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.DoesNotContain("has not declared", outcome.Message);
        Assert.Contains(outcome.Quest!.Id, outcome.Message);
    }

    [Fact]
    public async Task Declining_without_a_reason_is_refused()
    {
        var published = await Publish("Declared");

        var outcome = await _exchange.RespondAsync(published.Quest!.Id, "decline", null, Now);

        Assert.Equal(QuestRespondRefusal.MissingReason, outcome.Refusal);
    }

    [Fact]
    public async Task An_unknown_action_is_refused()
    {
        var outcome = await _exchange.RespondAsync("abc123", "postpone", null, Now);

        Assert.Equal(QuestRespondRefusal.UnknownAction, outcome.Refusal);
    }

    [Fact]
    public async Task An_unknown_id_is_not_found()
    {
        var outcome = await _exchange.RespondAsync("zzzzzz", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.NotFound, outcome.Refusal);
    }

    /// <summary>A leading `#` is how ids are printed, so responding with one pasted back must work.</summary>
    [Fact]
    public async Task Take_then_done_moves_the_status()
    {
        var published = await Publish("Declared");

        var taken = await _exchange.RespondAsync($"#{published.Quest!.Id}", "take", null, Now);
        var done = await _exchange.RespondAsync(published.Quest.Id, "done", "Landed.", Now.AddDays(1));

        Assert.Equal(QuestStatus.Taken, taken.Quest!.Status);
        Assert.Equal(QuestStatus.Done, done.Quest!.Status);
    }

    /// <summary>
    /// The cross-machine race, at the layer where it resolves (D47 §5): the second take is refused
    /// naming the state, and the message tells the losing session what to do — stand down.
    /// </summary>
    [Fact]
    public async Task Taking_a_taken_quest_is_refused_toward_standing_down()
    {
        var published = await Publish("Declared");
        await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now);

        var second = await _exchange.RespondAsync(published.Quest.Id, "take", null, Now.AddMinutes(1));

        Assert.Equal(QuestRespondRefusal.AlreadyTaken, second.Refusal);
        Assert.Contains("already taken", second.Message);
        Assert.Contains("stand down", second.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One title is one quest forever (D46 §3): closed means immovable, in both directions.</summary>
    [Fact]
    public async Task A_closed_quest_refuses_every_move()
    {
        var published = await Publish("Declared");
        await _exchange.RespondAsync(published.Quest!.Id, "done", "Landed.", Now);

        var retake = await _exchange.RespondAsync(published.Quest.Id, "take", null, Now.AddDays(1));
        var redecline = await _exchange.RespondAsync(published.Quest.Id, "decline", "second thoughts", Now.AddDays(1));

        Assert.Equal(QuestRespondRefusal.Closed, retake.Refusal);
        Assert.Equal(QuestRespondRefusal.Closed, redecline.Refusal);
        Assert.Contains("does not move", retake.Message);
    }

    /// <summary>
    /// Outside work is first-class (D46): someone who just did the thing closes the quest without
    /// ever having taken it, and the table must allow that.
    /// </summary>
    [Fact]
    public async Task Done_straight_from_open_is_outside_work_and_fine()
    {
        var published = await Publish("Declared");

        var done = await _exchange.RespondAsync(published.Quest!.Id, "done", "Already had it.", Now);

        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        Assert.Equal(QuestStatus.Done, done.Quest!.Status);
    }
}
