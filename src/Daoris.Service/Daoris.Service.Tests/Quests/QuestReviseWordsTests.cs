using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A quest published under a request's key may have its title and body moved while it is open or taken, which is how a later landing
/// retargets the one pin-move quest of a family edge instead of opening a second. Any other quest, and a closed one, keeps its words.
/// </summary>
public sealed class QuestReviseWordsTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-revise-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-26T09:00:00Z");

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "core", "web" })
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

    private async Task<Quest> Ask(string? request) =>
        (await _exchange.PublishAsync(new QuestAsk("core", "web", "Move the pin", "To the first commit.") { Request = request }, Now)).Quest!;

    [Fact]
    public async Task An_open_request_quest_takes_new_words_and_keeps_its_key_weight_and_paths()
    {
        var made = await Ask("family.core.web/pin/1");

        var moved = await _exchange.ReviseWordsAsync(made.Id, "Move the pin to the second", "To the second commit.", Now.AddHours(1));

        Assert.Equal(QuestRespondRefusal.None, moved.Refusal);
        Assert.Equal("Move the pin to the second", moved.Quest!.Title);
        Assert.Equal("To the second commit.", moved.Quest.Body);
        Assert.Equal("family.core.web/pin/1", moved.Quest.Request);
        Assert.Equal(QuestStatus.Open, moved.Quest.Status);
        Assert.Equal(made.Id, moved.Quest.Id);
    }

    [Fact]
    public async Task A_taken_one_is_retargeted_too_and_the_same_words_again_move_nothing()
    {
        var made = await Ask("family.core.web/pin/1");
        await _quests.MoveAsync(made.Id, QuestStatus.Taken, null, Now.AddMinutes(1));

        var first = await _exchange.ReviseWordsAsync(made.Id, "Move the pin to the second", "Second.", Now.AddHours(1));
        var again = await _exchange.ReviseWordsAsync(made.Id, "Move the pin to the second", "Second.", Now.AddHours(2));

        Assert.Equal(QuestRespondRefusal.None, first.Refusal);
        Assert.Equal(QuestStatus.Taken, first.Quest!.Status);
        Assert.Equal(QuestRespondRefusal.None, again.Refusal);
        Assert.Equal(1, (await _quests.HistoryAsync(made.Id)).Count(operation => operation.Kind == QuestOperationKind.Revised));
    }

    [Fact]
    public async Task A_closed_quest_a_plain_ask_and_an_unknown_id_keep_their_words()
    {
        var closed = await Ask("family.core.web/pin/1");
        await _quests.MoveAsync(closed.Id, QuestStatus.Declined, "red", Now.AddMinutes(1));
        var plain = await Ask(null);

        Assert.Equal(QuestRespondRefusal.Closed, (await _exchange.ReviseWordsAsync(closed.Id, "t", "b", Now.AddHours(1))).Refusal);
        Assert.Equal(QuestRespondRefusal.NotRequested, (await _exchange.ReviseWordsAsync(plain.Id, "t", "b", Now.AddHours(1))).Refusal);
        Assert.Equal(QuestRespondRefusal.NotFound, (await _exchange.ReviseWordsAsync("nosuchid", "t", "b", Now.AddHours(1))).Refusal);
        Assert.Equal("Move the pin", (await _quests.FindAsync(closed.Id))!.Title);
        Assert.Equal("Move the pin", (await _quests.FindAsync(plain.Id))!.Title);
    }

    [Fact]
    public async Task Words_that_are_empty_are_refused()
    {
        var made = await Ask("family.core.web/pin/1");

        Assert.Equal(QuestRespondRefusal.BadAnswer, (await _exchange.ReviseWordsAsync(made.Id, " ", "b", Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.BadAnswer, (await _exchange.ReviseWordsAsync(made.Id, "t", "", Now)).Refusal);
    }
}
