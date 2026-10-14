using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// HIST1b (D153 point 3, the history-clearing design §3): a closed quest a remote numbered is FORGOTTEN on the machine that
/// clears it, over the real wire. Its rows, log and records go here and its id is kept; the fetches pass over it while the
/// cursors move past, so no later move and no cursor at zero brings it back; nothing is pushed and nothing travels, so the
/// remote and a teammate keep the team's copy, and a new store fetches it whole.
/// </summary>
public sealed class HistorySyncTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-historysync-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<SqliteConnection> _connections = [];
    private KnowledgeService _service = null!;
    private QuestStore _remoteQuests = null!;
    private SessionStore _remoteSessions = null!;
    private Machine _a = null!;
    private Machine _b = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T10:00:00Z");

    /// <summary>One machine: its store, known to the remote by its key, and its desk, wired to the remote.</summary>
    private sealed record Machine(string Key, SqliteConnection Connection, QuestStore Quests, SessionStore Sessions, HistoryDesk Desk);

    public async Task InitializeAsync()
    {
        Repo("Federated", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Shared with the team.", "owns": ["its area"], "accepts": ["a quest"] },
              "remote": { "join": true, "knowledge": false }
            }
            """);
        Repo("Homebody", """
            { "source": "s", "packs": [], "domain": { "summary": "Stays local.", "owns": ["itself"], "accepts": ["a quest"] } }
            """);
        Repo("Asker", """
            { "source": "s", "packs": [], "domain": { "summary": "Asks for things.", "owns": ["its own tree"], "accepts": [] } }
            """);

        var index = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            index, new LexicalKnowledgeSearch(index), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, DateTimeOffset.UtcNow);

        var remote = await OpenAsync();
        _remoteQuests = await QuestStore.OpenAsync(remote);
        _remoteSessions = await SessionStore.OpenAsync(remote);
        _a = await MachineAsync("a@one");
        _b = await MachineAsync("b@two");
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        return connection;
    }

    private async Task<Machine> MachineAsync(string key)
    {
        var connection = await OpenAsync();
        var quests = await QuestStore.OpenAsync(connection);
        var sessions = await SessionStore.OpenAsync(connection);
        var asks = await AskStore.OpenAsync(connection);
        return new(key, connection, quests, sessions,
            new HistoryDesk(quests, sessions, asks, _service, new OneRemote(Remote(key))));
    }

    /// <summary>The remote as <paramref name="key"/>'s machine reaches it: the real stores behind the real wire.</summary>
    private StoreRemote Remote(string key) => new(_remoteQuests, _remoteSessions, _service, key);

    /// <summary>One machine's pass, as <c>POST /api/sync</c> runs it: the quests, then the session records.</summary>
    private async Task<(QuestSyncReport Quests, SessionSyncReport Sessions)> SyncAsync(Machine machine, string workspace = Workspaces.Default)
    {
        var remote = Remote(machine.Key);
        var quests = await QuestSync.RunAsync(machine.Quests, _service, remote, workspace);
        var sessions = await SessionSync.RunAsync(machine.Sessions, machine.Quests, _service, remote, workspace);
        return (quests, sessions);
    }

    /// <summary>
    /// A shared quest finished on A after a failed try on B, every move and record of it carried both ways: what a team's
    /// workspace holds of finished work.
    /// </summary>
    private async Task<(Quest Quest, Session Mine, Session Theirs)> FinishedOnBothAsync(string title = "Shared work")
    {
        var quest = await _a.Quests.PublishAsync("Asker", "Federated", title, "why", Now);
        await SyncAsync(_a);
        await SyncAsync(_b);
        var theirs = await _b.Sessions.CreateAsync(quest.Id, "Federated", "stub", Now.AddMinutes(1));
        await _b.Sessions.SetStateAsync(theirs.Id, SessionState.Failed, "it fell over.", null, null, Now.AddMinutes(2));
        var mine = await _a.Sessions.CreateAsync(quest.Id, "Federated", "stub", Now.AddMinutes(3));
        await _a.Quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(4));
        await _a.Quests.MoveAsync(quest.Id, QuestStatus.Done, "Landed.", Now.AddMinutes(5));
        await _a.Sessions.SetStateAsync(mine.Id, SessionState.Completed, "landed.", null, null, Now.AddMinutes(5));
        foreach (var machine in new[] { _b, _a, _b, _a }) await SyncAsync(machine);
        return (quest, mine, theirs);
    }

    private static HistoryUnitRef Unit(Quest quest) => new(HistoryUnitKind.Quest, quest.Id);

    private async Task<long> RemoteThroughAsync() => (await _remoteQuests.OperationsSinceAsync(0, limit: 10_000)).Through;

    /// <summary>
    /// 🔴 A closed quest the remote numbered is forgotten here: its row, log and records go, its id is kept, and the next
    /// pass pushes nothing. The remote and the teammate keep the team's copy whole: no tombstone, no new operation.
    /// </summary>
    [Fact]
    public async Task A_cleared_quest_a_remote_numbered_is_forgotten_here_and_nothing_travels()
    {
        var (quest, mine, theirs) = await FinishedOnBothAsync();
        var copy = $"b@two/{theirs.Id}";
        var atRemote = (await _remoteQuests.HistoryAsync(quest.Id)).Count;

        var plan = await _a.Desk.PlanAsync(Unit(quest));
        Assert.True(plan.Clearable);
        Assert.Equal([quest.Id], plan.Forgotten);
        Assert.Equal([mine.Id], plan.Sessions);
        Assert.Equal([copy], plan.Teammates);

        var cleared = Assert.Single(await _a.Desk.ClearAsync([Unit(quest)], Now.AddHours(1)));

        Assert.True(cleared.Cleared);
        Assert.Contains("The remote for `default` keeps the team's copy; this machine will not fetch it again.", cleared.Message);
        Assert.True(await _a.Quests.ForgottenAsync(quest.Id));
        Assert.Null(await _a.Quests.FindAsync(quest.Id));
        Assert.Empty(await _a.Quests.HistoryAsync(quest.Id));
        Assert.Empty(await _a.Sessions.ListAsync(includeClosed: true));
        Assert.Empty(await _a.Quests.PendingAsync(Workspaces.Default, _ => true));

        var pass = await SyncAsync(_a);
        await SyncAsync(_b);

        Assert.Equal((0, 0, 0), (pass.Quests.Pushed, pass.Sessions.Pushed, pass.Sessions.Fetched));
        Assert.Null(await _a.Quests.FindAsync(quest.Id));
        Assert.Empty(await _a.Sessions.ListAsync(includeClosed: true));
        Assert.Equal(atRemote, (await _remoteQuests.HistoryAsync(quest.Id)).Count);
        Assert.DoesNotContain(await _remoteQuests.HistoryAsync(quest.Id), operation => operation.Kind == QuestOperationKind.Deleted);
        Assert.Equal(QuestStatus.Done, (await _remoteQuests.FindAsync(quest.Id))!.Status);
        Assert.NotNull(await _remoteSessions.FindAsync($"a@one/{mine.Id}"));
        Assert.Equal(QuestStatus.Done, (await _b.Quests.FindAsync(quest.Id))!.Status);
        Assert.NotNull(await _b.Sessions.FindAsync(theirs.Id));
        Assert.NotNull(await _b.Sessions.FindAsync($"a@one/{mine.Id}"));
    }

    /// <summary>
    /// A closed quest still takes a conflict and a dismissal from another machine (QuestLog's table), which a person on
    /// that machine must settle first. Made after the clear here, both are passed over and the cursor moves past them, so
    /// they make no half quest here.
    /// </summary>
    [Fact]
    public async Task A_later_move_on_a_forgotten_quest_is_passed_over_and_the_cursor_moves_past_it()
    {
        var quest = await _a.Quests.PublishAsync("Asker", "Federated", "Raced work", "why", Now);
        await SyncAsync(_a);
        await SyncAsync(_b);
        await _a.Quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _a.Quests.MoveAsync(quest.Id, QuestStatus.Done, "Landed.", Now.AddMinutes(2));
        await SyncAsync(_a);
        Assert.True(Assert.Single(await _a.Desk.ClearAsync([Unit(quest)], Now.AddHours(1))).Cleared);

        // B never saw the take: its decline, made offline, loses to the done and is kept as a conflict on the quest.
        await _b.Quests.MoveAsync(quest.Id, QuestStatus.Declined, "Not ours after all.", Now.AddMinutes(3));
        await SyncAsync(_b);
        var conflicted = await _b.Desk.PlanAsync(Unit(quest));
        Assert.Equal((HistoryRefusal.NeedsYou, quest.Id), (conflicted.Refusal!.Refusal, conflicted.Refusal.Quest));
        Assert.Equal($"A conflict on `#{quest.Id}` waits on you.", conflicted.Refusal.Message);

        await SyncAsync(_a);
        Assert.Null(await _a.Quests.FindAsync(quest.Id));

        await _b.Quests.DismissAsync(quest.Id, null, null, Now.AddMinutes(4));
        await SyncAsync(_b);
        Assert.True((await _b.Desk.PlanAsync(Unit(quest))).Clearable);
        await SyncAsync(_a);

        Assert.Null(await _a.Quests.FindAsync(quest.Id));
        Assert.Empty(await _a.Quests.HistoryAsync(quest.Id));
        Assert.Equal(await RemoteThroughAsync(), await _a.Quests.CursorAsync(Workspaces.Default));
    }

    /// <summary>
    /// 🔴 H4: a cursor at zero fetches the whole history, and a quest whose rows went would come back whole. Forgotten, it
    /// does not: not when the cursors are removed, as the hand purge removed them, and not when the circle is wired again
    /// under another name, whose cursor never moved.
    /// </summary>
    [Fact]
    public async Task A_cursor_at_zero_never_brings_a_forgotten_quest_back()
    {
        var (quest, _, theirs) = await FinishedOnBothAsync();
        await _a.Desk.ClearAsync([Unit(quest)], Now.AddHours(1));
        await using (var purge = _a.Connection.CreateCommand())
        {
            purge.CommandText = "DELETE FROM quest_cursor; DELETE FROM session_cursor";
            await purge.ExecuteNonQueryAsync();
        }

        Assert.Equal(0, await _a.Quests.CursorAsync(Workspaces.Default));
        var fromZero = await SyncAsync(_a);
        var renamed = await SyncAsync(_a, "renamed");

        Assert.Null(fromZero.Quests.Problem);
        Assert.Null(await _a.Quests.FindAsync(quest.Id));
        Assert.Empty(await _a.Quests.HistoryAsync(quest.Id));
        Assert.Null(await _a.Sessions.FindAsync($"b@two/{theirs.Id}"));
        Assert.Empty(await _a.Sessions.ListAsync(includeClosed: true));
        Assert.Equal((0, 0), (fromZero.Sessions.Fetched, renamed.Sessions.Fetched));
        Assert.Equal(await RemoteThroughAsync(), await _a.Quests.CursorAsync("renamed"));
    }

    /// <summary>
    /// A teammate's record of a forgotten quest that moves again on their machine comes down past its cursor, and is
    /// passed over: it would name a quest nothing here holds.
    /// </summary>
    [Fact]
    public async Task A_teammates_record_of_a_forgotten_quest_that_moves_again_is_passed_over()
    {
        var (quest, _, theirs) = await FinishedOnBothAsync();
        await _a.Desk.ClearAsync([Unit(quest)], Now.AddHours(1));
        var unrelated = await _b.Sessions.CreateAsync(null, "Federated", "stub", Now.AddHours(2), kind: SessionKind.Chat);

        await _b.Sessions.SetStateAsync(theirs.Id, SessionState.Failed, "looked again.", null, null, Now.AddHours(2));
        await SyncAsync(_b);
        var pass = await SyncAsync(_a);

        Assert.Equal(1, pass.Sessions.Fetched);
        Assert.Null(await _a.Sessions.FindAsync($"b@two/{theirs.Id}"));
        Assert.NotNull(await _a.Sessions.FindAsync($"b@two/{unrelated.Id}"));
    }

    /// <summary>
    /// A new store is a new machine (the sync design §2), and the team's history is its to hold: its first fetch brings the
    /// forgotten quest whole, with every record of it, this machine's own that went up included.
    /// </summary>
    [Fact]
    public async Task A_new_store_fetches_a_forgotten_quest_whole_with_the_teams_records_of_it()
    {
        var (quest, mine, theirs) = await FinishedOnBothAsync();
        await _a.Desk.ClearAsync([Unit(quest)], Now.AddHours(1));
        var newcomer = await MachineAsync("c@three");

        await SyncAsync(newcomer);

        Assert.Equal(QuestStatus.Done, (await newcomer.Quests.FindAsync(quest.Id))!.Status);
        Assert.False(await newcomer.Quests.ForgottenAsync(quest.Id));
        Assert.NotNull(await newcomer.Sessions.FindAsync($"a@one/{mine.Id}"));
        Assert.NotNull(await newcomer.Sessions.FindAsync($"b@two/{theirs.Id}"));
    }

    /// <summary>
    /// H5: the same words make the same id. Where the quest was forgotten, the remote still holds it closed and would refuse
    /// a fresh copy on every pass, so the publish is refused in a closed quest's words and nothing is written. Where it
    /// simply went, as after a delete, the same words make it again.
    /// </summary>
    [Fact]
    public async Task The_same_words_asked_again_are_refused_where_the_quest_was_forgotten_and_ask_again_where_it_went()
    {
        var (quest, _, _) = await FinishedOnBothAsync("Asked once");
        await _a.Desk.ClearAsync([Unit(quest)], Now.AddHours(1));
        var local = await _a.Quests.PublishAsync("Asker", "Homebody", "Asked here alone", "why", Now);
        await _a.Quests.MoveAsync(local.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _a.Quests.MoveAsync(local.Id, QuestStatus.Declined, "Not now.", Now.AddMinutes(2));
        Assert.Empty(Assert.Single(await _a.Desk.ClearAsync([Unit(local)], Now.AddHours(1))).Unit.Forgotten);
        var exchange = new QuestExchange(_service, _a.Quests, new OneRemote(Remote(_a.Key)), files: null, _a.Sessions);

        var refused = await exchange.PublishAsync("Asker", "Federated", "Asked once", "Asked again.", Now.AddHours(2));
        var again = await exchange.PublishAsync("Asker", "Homebody", "Asked here alone", "Asked again.", Now.AddHours(2));

        Assert.Equal(QuestPublishRefusal.Cleared, refused.Refusal);
        Assert.Equal(
            $"Quest `#{quest.Id}` was cleared from this machine; the remote for `default` holds it closed. A new ask is a new title.",
            refused.Message);
        Assert.Null(await _a.Quests.FindAsync(quest.Id));
        Assert.DoesNotContain(await _a.Quests.PendingAsync(Workspaces.Default, _ => true), operation => operation.Quest == quest.Id);
        Assert.Equal(QuestPublishRefusal.None, again.Refusal);
        Assert.Equal((local.Id, QuestStatus.Open), (again.Quest!.Id, again.Quest.Status));
    }

    /// <summary>
    /// A record of this machine's written after the last push is the team's copy still to come (design §1.2): the clear
    /// waits for the pass that carries it, and then forgets.
    /// </summary>
    [Fact]
    public async Task A_record_the_remote_has_not_taken_keeps_the_quest_until_a_pass_carries_it()
    {
        var (quest, mine, _) = await FinishedOnBothAsync();
        await _a.Sessions.SetStateAsync(mine.Id, SessionState.Completed, "landed, and reviewed.", null, null, Now.AddHours(1));

        var unpushed = (await _a.Desk.PlanAsync(Unit(quest))).Refusal!;
        await SyncAsync(_a);
        var carried = await _a.Desk.PlanAsync(Unit(quest));

        Assert.Equal((HistoryRefusal.Unpushed, mine.Id, Workspaces.Default), (unpushed.Refusal, unpushed.Session, unpushed.Workspace));
        Assert.True(carried.Clearable);
        Assert.Equal([quest.Id], carried.Forgotten);
    }
}
