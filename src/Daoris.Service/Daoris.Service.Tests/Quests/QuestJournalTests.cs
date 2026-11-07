using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>quests</c> cache journals what the replay says the quest became,
/// one <c>opened</c> row where a quest begins and one <c>state</c> row for each move of its status, whoever's operation moved it: this
/// machine's, a remote's integrated by sync, or a rebase that finds this machine's close lost. An operation kept that moves nothing and a
/// cache written again with the same status journal nothing. Three stores synced through a remote's, as <see cref="QuestSyncTests"/> does.
/// </summary>
public sealed class QuestJournalTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T10:00:00Z");

    private readonly Dictionary<QuestStore, SqliteConnection> _connections = [];
    private QuestStore _a = null!;
    private QuestStore _b = null!;
    private QuestStore _remote = null!;

    public async Task InitializeAsync()
    {
        _a = await OpenAsync();
        _b = await OpenAsync();
        _remote = await OpenAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections.Values) await connection.DisposeAsync();
    }

    private async Task<QuestStore> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = await QuestStore.OpenAsync(connection);
        _connections[store] = connection;
        return store;
    }

    private Task<QuestSyncReport> SyncAsync(QuestStore machine) =>
        QuestSync.RunAsync(machine, _ => true, new StoreRemote(_remote), Workspaces.Default);

    private sealed record Moved(string Record, string Change, string? StateOld, string? StateNew, string? Quest, string? Workspace);

    /// <summary>The <c>quests</c> family's rows of <paramref name="store"/>, in order.</summary>
    private async Task<List<Moved>> MovesAsync(QuestStore store)
    {
        await using var command = _connections[store].CreateCommand();
        command.CommandText = """
            SELECT record, change, state_old, state_new, quest, workspace FROM event_journal WHERE family = 'quests' ORDER BY position
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Moved>();
        while (await reader.ReadAsync())
        {
            string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
            rows.Add(new Moved(reader.GetString(0), reader.GetString(1), Text(2), Text(3), Text(4), Text(5)));
        }

        return rows;
    }

    private static List<(string Change, string? From, string? To)> Steps(IEnumerable<Moved> rows) =>
        [.. rows.Select(row => (row.Change, row.StateOld, row.StateNew))];

    [Fact]
    public async Task A_publish_is_one_opened_row_and_each_move_of_its_status_one_state_row_with_no_text()
    {
        var quest = await _a.PublishAsync("Asker", "Owner", "A private title", "A private body", Now, workspace: "team");
        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _a.MoveAsync(quest.Id, QuestStatus.Done, "A private note", Now.AddMinutes(2));

        var rows = await MovesAsync(_a);

        Assert.Equal([("opened", null, "Open"), ("state", "Open", "Taken"), ("state", "Taken", "Done")], Steps(rows));
        Assert.All(rows, row => Assert.Equal((quest.Id, quest.Id, "team"), (row.Record, row.Quest, row.Workspace)));
        Assert.DoesNotContain("private", string.Join("|", rows).ToLowerInvariant());
    }

    /// <summary>A cache written again whole with the same status, as the replay writes it for an operation that moves nothing, is no move.</summary>
    [Fact]
    public async Task A_cache_written_again_with_its_status_unchanged_journals_nothing()
    {
        var quest = await _a.PublishAsync("Asker", "Owner", "Title", "Body", Now);
        var before = await MovesAsync(_a);

        // The cache's own upsert over the row it holds, then a write naming the status it keeps.
        await using (var command = _connections[_a].CreateCommand())
        {
            command.CommandText = """
                INSERT INTO quests SELECT * FROM quests WHERE id = $id
                ON CONFLICT (id) DO UPDATE SET title = 'again', status = excluded.status;
                UPDATE quests SET updated = 'u', status = status WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", quest.Id);
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(before, await MovesAsync(_a));
    }

    /// <summary>
    /// A remote's publish integrated by sync begins the quest here: one <c>opened</c>. A second pass brings nothing new and journals nothing,
    /// and the other machine's take arrives as the move it is, never as a new quest.
    /// </summary>
    [Fact]
    public async Task A_publish_integrated_by_sync_is_one_opened_row_and_a_second_pass_journals_nothing()
    {
        var quest = await _a.PublishAsync("Asker", "Federated", "Title", "Body", Now);
        await SyncAsync(_a);
        await SyncAsync(_b);
        await SyncAsync(_b);

        Assert.Equal([("opened", null, "Open")], Steps(await MovesAsync(_b)));

        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Equal([("opened", null, "Open"), ("state", "Open", "Taken")], Steps(await MovesAsync(_b)));
    }

    /// <summary>
    /// 🔴 The rebase that finds this machine's take lost carries its done with it: the cache moves out of its close, <c>Done</c> to
    /// <c>Taken</c>, the one row a <c>quest.close-lost</c> is told from. The log's rewrite in place journals nothing of its own. On the
    /// winner's machine the losing take and its done are operations kept that move nothing, and journal nothing.
    /// </summary>
    [Fact]
    public async Task A_rebase_that_loses_this_machines_done_moves_the_cache_from_done_to_taken()
    {
        var quest = await _a.PublishAsync("Asker", "Federated", "Title", "Body", Now);
        await SyncAsync(_a);
        await SyncAsync(_b);

        await _a.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        await _b.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(2));
        await _b.MoveAsync(quest.Id, QuestStatus.Done, "Finished offline.", Now.AddHours(3));
        await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Equal(QuestStatus.Taken, (await _b.FindAsync(quest.Id))!.Status);
        Assert.Equal(
            [("opened", null, "Open"), ("state", "Open", "Taken"), ("state", "Taken", "Done"), ("state", "Done", "Taken")],
            Steps(await MovesAsync(_b)));

        await SyncAsync(_a);
        Assert.Equal(QuestStatus.Taken, (await _a.FindAsync(quest.Id))!.Status);
        Assert.Equal([("opened", null, "Open"), ("state", "Open", "Taken")], Steps(await MovesAsync(_a)));
    }
}
