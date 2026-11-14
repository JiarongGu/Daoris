using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A quest worked in a workflow's run is bound to that run at its first start, so its agent's done is a submission that leaves it taken;
/// its Done is written when the run comes to its end, in the same write that ends the sessions waiting on the run.
/// </summary>
public sealed class RunEndsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-15T10:00:00Z");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);
        _ledger = new SessionLedger(_quests, _sessions, service);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>A quest bound to its run before its take, taken, submitted by its session, and that session waiting on the run.</summary>
    private async Task<(Quest Quest, Session Session)> Submitted()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        Assert.True((await _quests.BindAsync(quest.Id, quest.Id, Now)).Moved);
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now, tree: "/trees/s-1");
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(opened.Session!.Id, state, null, null, null, Now)).Refusal);
        }

        Assert.True((await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now)).Moved);
        var submitted = await _quests.SubmitAsync(quest.Id, "Built.", [], Now, session: opened.Session!.Id);
        Assert.True(submitted.Moved);
        var waits = await _ledger.AdvanceAsync(opened.Session!.Id, "awaiting-step", "It waits on its run.", null, null, Now.AddMinutes(1));
        Assert.Equal(SessionAdvanceRefusal.None, waits.Refusal);
        return (submitted.Quest!, waits.Session!);
    }

    private static QuestOperationRef Ref(Quest quest) => new(quest.Submission!.Machine, quest.Submission.Sequence);

    [Fact]
    public async Task A_run_binds_its_open_quest_once_and_the_bind_names_the_run()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);

        var bound = await _quests.BindAsync(quest.Id, "q-run", Now);

        Assert.True(bound.Moved);
        Assert.Equal(QuestBindOperation.RunClosure("q-run"), bound.Quest!.Closure);
        Assert.False((await _quests.BindAsync(quest.Id, "q-run", Now)).Moved);
        Assert.Equal(QuestStatus.Open, (await _quests.FindAsync(quest.Id))!.Status);
    }

    [Fact]
    public async Task The_runs_end_writes_the_quests_done_and_ends_its_waiting_session_in_one_write()
    {
        var (quest, session) = await Submitted();
        Assert.Equal(QuestStatus.Taken, quest.Status);

        var ended = await _ledger.EndRunAsync(
            quest.Id, [(quest.Id, Ref(quest))], [session.Id], "completed", "Its workflow's run came to its end.", null, Now.AddMinutes(9));

        Assert.Equal(QuestCloseRefusal.None, ended.Refusal);
        Assert.Equal(QuestStatus.Done, (await _quests.FindAsync(quest.Id))!.Status);
        Assert.Equal("Built.", (await _quests.FindAsync(quest.Id))!.Note);
        Assert.Equal(SessionState.Completed, (await _sessions.FindAsync(session.Id))!.State);
        Assert.Equal([session.Id], ended.Ended.Select(each => each.Id));
    }

    [Fact]
    public async Task A_close_refused_moves_no_session()
    {
        var (quest, session) = await Submitted();

        var ended = await _ledger.EndRunAsync(
            quest.Id, [(quest.Id, new QuestOperationRef(Ref(quest).Machine, Ref(quest).Sequence + 7))], [session.Id], "completed", null, null, Now);

        Assert.Equal(QuestCloseRefusal.Stale, ended.Refusal);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
        Assert.Equal(SessionState.AwaitingStep, (await _sessions.FindAsync(session.Id))!.State);
    }

    [Fact]
    public async Task A_run_that_ended_any_other_way_ends_its_sessions_and_leaves_its_quest_taken()
    {
        var (quest, session) = await Submitted();

        var ended = await _ledger.EndRunAsync(quest.Id, [], [session.Id], "stopped", "Its run ended failed at `check`.", null, Now);

        Assert.Equal(QuestCloseRefusal.None, ended.Refusal);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
        Assert.Equal(SessionState.Stopped, (await _sessions.FindAsync(session.Id))!.State);
    }
}
