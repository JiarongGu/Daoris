using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A stand-down against this machine's own dead taker is read past at the open: the quest's take is this machine's, and an earlier
/// session here took it and ended failed or stopped, so a session of the quest goes on rather than being refused as taken.
/// </summary>
public sealed class SessionOpenBehindStandDownTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-13T09:00:00Z");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);
        _ledger = new SessionLedger(_quests, sessions, service);
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    private async Task<Session> EndedAsync(Quest quest, string ending, DateTimeOffset at, bool took)
    {
        var opened = await _ledger.OpenAsync(quest.Id, "stub", at);
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        foreach (var state in new[] { "starting", "working" }) await _ledger.AdvanceAsync(opened.Session!.Id, state, null, null, null, at);
        // The take is made by the session's own connector while it works, once, by the first.
        if ((await _quests.FindAsync(quest.Id))!.Status == QuestStatus.Open) await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, at);
        if (took) Assert.True(await _ledger.MarkTookAsync(opened.Session!.Id, quest.Id));
        await _ledger.AdvanceAsync(opened.Session!.Id, ending, "ended.", null, null, at.AddMinutes(5), interrupted: ending == "stopped");
        return opened.Session!;
    }

    /// <summary>🔴 The failed taker's carry-on stood down against its own take; the next open reads past that stand-down.</summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("stopped")]
    public async Task A_stand_down_behind_a_taker_that_ended_is_read_past_where_the_claim_is_this_machines(string ending)
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        await EndedAsync(quest, ending, Now, took: true);
        await EndedAsync(quest, "stood-down", Now.AddMinutes(10), took: false);

        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(20));

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
    }

    /// <summary>A stand-down behind a record that never took the quest, or one that still runs, stays refused.</summary>
    [Fact]
    public async Task A_stand_down_behind_a_record_that_never_took_the_quest_is_refused()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        await EndedAsync(quest, "failed", Now, took: false);
        await EndedAsync(quest, "stood-down", Now.AddMinutes(10), took: false);

        Assert.Equal(SessionOpenRefusal.QuestNotOpen, (await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(20))).Refusal);
    }
}
