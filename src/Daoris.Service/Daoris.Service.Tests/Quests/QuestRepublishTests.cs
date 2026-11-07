using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The same sender, receiver and title name one standing quest: asked again while it is open or taken, the publish is a retry and
/// answers that quest, said; asked again after it closed, the words are a new ask and make a new quest, the closed one unchanged.
/// </summary>
public sealed class QuestRepublishTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-republish-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-07T09:00:00Z");

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

    private Task<QuestPublishOutcome> Ask(string body, DateTimeOffset? at = null) =>
        _exchange.PublishAsync("game", "engine", "Expose the frame cap", body, at ?? Now);

    [Theory]
    [InlineData(QuestStatus.Open)]
    [InlineData(QuestStatus.Taken)]
    public async Task A_publish_matching_a_standing_quest_answers_it_and_says_it_was_already_asked(QuestStatus status)
    {
        var first = (await Ask("The game needs it to pace.")).Quest!;
        if (status == QuestStatus.Taken) await _quests.MoveAsync(first.Id, QuestStatus.Taken, null, Now.AddMinutes(1));

        var retry = await Ask("The game needs it to pace.", Now.AddMinutes(2));

        Assert.Equal(QuestPublishRefusal.None, retry.Refusal);
        Assert.Equal(first.Id, retry.Quest!.Id);
        Assert.Equal(status, retry.Quest.Status);
        Assert.Contains("already", retry.Message, StringComparison.Ordinal);
        Assert.Single(await _quests.ListAsync(receiver: "engine"));
        Assert.Single(await _quests.HistoryAsync(first.Id), each => each.Kind == QuestOperationKind.Published);
    }

    [Theory]
    [InlineData(QuestStatus.Done)]
    [InlineData(QuestStatus.Declined)]
    public async Task A_publish_matching_only_a_closed_quest_makes_a_new_quest_and_leaves_the_closed_one(QuestStatus closed)
    {
        var first = (await Ask("The game needs it to pace.")).Quest!;
        await _quests.MoveAsync(first.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _quests.MoveAsync(first.Id, closed, "Closed.", Now.AddMinutes(2));

        var again = await Ask("A new body: the cap must be per window.", Now.AddMinutes(3));

        Assert.Equal(QuestPublishRefusal.None, again.Refusal);
        Assert.NotEqual(first.Id, again.Quest!.Id);
        Assert.Equal(QuestStatus.Open, again.Quest.Status);
        Assert.Equal("A new body: the cap must be per window.", again.Quest.Body);
        Assert.DoesNotContain("already", again.Message, StringComparison.Ordinal);
        Assert.Equal(closed, (await _quests.FindAsync(first.Id))!.Status);
        Assert.Equal("The game needs it to pace.", (await _quests.FindAsync(first.Id))!.Body);
    }

    [Fact]
    public async Task The_new_quest_is_itself_one_a_retry_answers_and_a_second_close_makes_a_third()
    {
        var first = (await Ask("one")).Quest!;
        await _quests.MoveAsync(first.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _quests.MoveAsync(first.Id, QuestStatus.Done, "Done.", Now.AddMinutes(2));

        var second = (await Ask("two", Now.AddMinutes(3))).Quest!;
        var retry = (await Ask("two", Now.AddMinutes(4))).Quest!;
        await _quests.MoveAsync(second.Id, QuestStatus.Taken, null, Now.AddMinutes(5));
        await _quests.MoveAsync(second.Id, QuestStatus.Done, "Done.", Now.AddMinutes(6));
        var third = (await Ask("three", Now.AddMinutes(7))).Quest!;

        Assert.Equal(second.Id, retry.Id);
        Assert.Equal(3, new[] { first.Id, second.Id, third.Id }.Distinct().Count());
        Assert.Equal((QuestStatus.Open, "three"), (third.Status, third.Body));
        Assert.Equal(third.Id, Assert.Single(await _quests.ListAsync(receiver: "engine")).Id);
    }
}
