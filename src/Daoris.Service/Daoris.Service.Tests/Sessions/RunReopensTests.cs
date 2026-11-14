using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A record a run's work ended done under the older meaning, while its run goes on, reopens as waiting on the run's next step: once, in a
/// write the ledger's journal keeps, and never a record that has waited on a step before or ended any other way.
/// </summary>
public sealed class RunReopensTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-15T10:00:00Z");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionLedger _ledger = null!;
    private int _trees;

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

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task<Session> Moved(params string[] states)
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now, tree: $"/trees/s-{++_trees}")).Session!;
        foreach (var state in states)
        {
            var moved = await _ledger.AdvanceAsync(session.Id, state, state == "awaiting-step" ? "Its turn ended." : null, null, null, Now);
            Assert.Equal(SessionAdvanceRefusal.None, moved.Refusal);
            session = moved.Session!;
        }

        return session;
    }

    [Fact]
    public async Task A_record_ended_done_while_its_run_goes_on_waits_on_the_run_s_next_step_once()
    {
        var done = await Moved("starting", "working", "completed");

        var reopened = await _ledger.ReopenForRunAsync("q1", [done.Id], "It waits on its workflow's next step.", null, Now.AddMinutes(1));
        var again = await _ledger.ReopenForRunAsync("q1", [done.Id], "It waits on its workflow's next step.", null, Now.AddMinutes(2));

        var waiting = Assert.Single(reopened.Reopened);
        Assert.Equal((SessionState.AwaitingStep, "It waits on its workflow's next step."), (waiting.State, waiting.Note));
        Assert.Empty(again.Reopened);
    }

    [Fact]
    public async Task A_record_that_has_waited_on_a_step_before_or_ended_another_way_stays_as_it_is()
    {
        var waitedBefore = await Moved("starting", "working", "awaiting-step", "completed");
        var stopped = await Moved("starting", "working", "stopped");
        var working = await Moved("starting", "working");

        var reopened = await _ledger.ReopenForRunAsync("q1", [waitedBefore.Id, stopped.Id, working.Id, "s-none"], "It waits.", null, Now.AddMinutes(1));

        Assert.Empty(reopened.Reopened);
        Assert.Equal(SessionState.Completed, (await _ledger.FindAsync(waitedBefore.Id))!.State);
    }
}
