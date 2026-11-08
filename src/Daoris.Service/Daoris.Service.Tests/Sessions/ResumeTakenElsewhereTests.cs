using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A resume goes back to its taker's take as a carry-on does, so it asks whose the take is too: another machine's take that waited
/// on an answered question is not resumed here.
/// </summary>
public sealed class ResumeTakenElsewhereTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-19T10:00:00Z");
    private readonly List<SqliteConnection> _connections = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
    }

    private async Task<SqliteConnection> OpenedAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        return connection;
    }

    [Fact]
    public async Task A_taken_quest_that_waited_is_not_resumed_over_another_machines_take()
    {
        var quests = await QuestStore.OpenAsync(await OpenedAsync());
        var remote = await QuestStore.OpenAsync(await OpenedAsync());
        var other = await QuestStore.OpenAsync(await OpenedAsync());
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);
        var ledger = new SessionLedger(quests, await SessionStore.OpenAsync(await OpenedAsync()), service);
        Task Sync(QuestStore machine) => QuestSync.RunAsync(machine, _ => true, new StoreRemote(remote), Workspaces.Default);

        var quest = await quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var question = await quests.PublishAsync("Asker", "Asker", "Is the header a free string?", "Here is why.", Now);
        await Sync(quests);
        await Sync(other);
        Assert.True((await other.MoveAsync(quest.Id, QuestStatus.Taken, null, Now)).Moved);
        await other.WaitAsync(quest.Id, question.Id, Now);
        await Sync(other);
        await Sync(quests);
        await quests.MoveAsync(question.Id, QuestStatus.Done, "A free string.", Now.AddMinutes(5));

        var refused = await ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(6));

        Assert.Equal(SessionOpenRefusal.TakenElsewhere, refused.Refusal);
        Assert.Null(refused.Session);
        Assert.Equal($"Quest `#{quest.Id}` is taken on another machine: the take is theirs, so it is not resumed here over it.", refused.Message);
    }
}
