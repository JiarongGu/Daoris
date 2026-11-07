using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The one remover of a closed quest. One that may have travelled is tombstoned, so a peer drops it and no fetch brings it back;
/// one that never left simply goes; one still open is refused.
/// </summary>
public sealed class QuestClosedRemovalTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-closed-removal-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-05T10:00:00Z");

    private readonly List<SqliteConnection> _connections = [];
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private KnowledgeService _service = null!;

    public async Task InitializeAsync()
    {
        Repo("Federated", """{ "source": "s", "packs": [], "domain": { "summary": "Shared.", "owns": ["its area"], "accepts": ["a quest"] }, "remote": { "join": true, "knowledge": false } }""");
        Repo("Homebody", """{ "source": "s", "packs": [], "domain": { "summary": "Local.", "owns": ["itself"], "accepts": ["a quest"] } }""");
        Repo("Asker", """{ "source": "s", "packs": [], "domain": { "summary": "Asks.", "owns": ["its own tree"], "accepts": ["a question"] } }""");

        var connection = await OpenAsync();
        _quests = await QuestStore.OpenAsync(connection);
        _sessions = await SessionStore.OpenAsync(connection);
        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, DateTimeOffset.UtcNow);
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        return connection;
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    private QuestExchange Exchange(IRemote? remote = null) =>
        new(_service, _quests, remote is null ? null : new OneRemote(remote), null, _sessions);

    private static async Task<Quest> DoneAsync(QuestExchange exchange, string to)
    {
        var quest = (await exchange.PublishAsync("Asker", to, "Finished work", "Done long ago.", Now)).Quest!;
        await exchange.RespondAsync(quest.Id, "take", null, Now.AddMinutes(1));
        await exchange.RespondAsync(quest.Id, "done", "Finished.", Now.AddMinutes(2));
        return quest;
    }

    [Fact]
    public async Task A_closed_quest_that_never_left_goes_with_its_history()
    {
        var exchange = Exchange();
        var quest = await DoneAsync(exchange, "Homebody");

        var removed = await exchange.RemoveClosedAsync(quest.Id, Now.AddDays(1));

        Assert.Equal(QuestDeleteRefusal.None, removed.Refusal);
        Assert.Null(await _quests.FindAsync(quest.Id));
        Assert.Empty(await _quests.HistoryAsync(quest.Id));
    }

    [Fact]
    public async Task A_closed_quest_that_travelled_is_tombstoned_and_the_remote_drops_it()
    {
        var remote = await QuestStore.OpenAsync(await OpenAsync());
        var exchange = Exchange(new StoreRemote(remote));
        var quest = await DoneAsync(exchange, "Federated");
        await QuestSync.RunAsync(_quests, _service, new StoreRemote(remote), Workspaces.Default);
        Assert.Equal(QuestStatus.Done, (await remote.FindAsync(quest.Id))!.Status);

        var removed = await exchange.RemoveClosedAsync(quest.Id, Now.AddDays(1));

        Assert.Equal(QuestDeleteRefusal.None, removed.Refusal);
        Assert.Contains("removal is in its history", removed.Message);
        Assert.Null(await _quests.FindAsync(quest.Id));
        Assert.Contains(await _quests.HistoryAsync(quest.Id), operation => operation.Kind == QuestOperationKind.Deleted);
        Assert.Null(await remote.FindAsync(quest.Id));
        Assert.Contains(await remote.HistoryAsync(quest.Id), operation => operation.Kind == QuestOperationKind.Deleted);
    }

    [Fact]
    public async Task An_open_quest_is_not_removed_by_the_closed_quests_remover()
    {
        var exchange = Exchange();
        var quest = (await exchange.PublishAsync("Asker", "Homebody", "Still open", "Not done.", Now)).Quest!;

        var removed = await exchange.RemoveClosedAsync(quest.Id, Now.AddDays(1));

        Assert.Equal(QuestDeleteRefusal.Kept, removed.Refusal);
        Assert.NotNull(await _quests.FindAsync(quest.Id));
    }
}
