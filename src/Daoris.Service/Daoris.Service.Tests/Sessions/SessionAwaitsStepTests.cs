using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A session whose agent's turn ended waits on its workflow's next step: open, holding its tree, woken by the step's word through the same
/// go-on an ended record takes, and ended only by its workflow's end.
/// </summary>
public sealed class SessionAwaitsStepTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-14T10:00:00Z");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private int _trees;

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

    private async Task<Session> Waiting()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now, tree: $"/trees/s-{++_trees}");
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(opened.Session!.Id, state, null, null, null, Now)).Refusal);
        }

        var waits = await _ledger.AdvanceAsync(opened.Session!.Id, "awaiting-step", "its turn ended; it waits on `check`.", null, null, Now.AddMinutes(5));
        Assert.Equal(SessionAdvanceRefusal.None, waits.Refusal);
        return waits.Session!;
    }

    [Fact]
    public async Task A_waiting_session_is_open_and_spelled_awaiting_step()
    {
        var waiting = await Waiting();

        Assert.Equal((SessionState.AwaitingStep, "awaiting-step", true), (waiting.State, waiting.StateName, waiting.Active));
        Assert.True(Session.TryParse("awaiting-step", out var parsed) && parsed == SessionState.AwaitingStep);
        Assert.Equal(waiting.Id, (await _sessions.ActiveForAsync("Owner", waiting.Tree))?.Id);
    }

    [Fact]
    public async Task The_step_s_word_wakes_it_through_the_go_on_and_no_word_does_not()
    {
        var waiting = await Waiting();

        var bare = await _ledger.AdvanceAsync(waiting.Id, "working", null, null, null, Now.AddMinutes(6));
        Assert.Equal(SessionAdvanceRefusal.Terminal, bare.Refusal);

        await _ledger.SayAsync(waiting.Id, "Daoris: `check` failed.", null, Now.AddMinutes(7), from: SaidFrom.Daoris, key: "changes/r/check/1");
        var woken = await _ledger.AdvanceAsync(waiting.Id, "working", null, null, null, Now.AddMinutes(8));

        Assert.Equal(SessionAdvanceRefusal.None, woken.Refusal);
        Assert.Equal($"Session `{waiting.Id}` is working again: it goes on from awaiting-step with Daoris's words.", woken.Message);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("declined")]
    [InlineData("stopped")]
    [InlineData("awaiting-person")]
    public async Task Only_its_workflow_s_end_or_the_person_moves_it_on(string to)
    {
        var waiting = await Waiting();

        Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(waiting.Id, to, null, null, null, Now.AddMinutes(9))).Refusal);
        Assert.Equal(SessionAdvanceRefusal.InvalidMove, (await _ledger.AdvanceAsync((await Waiting()).Id, "stood-down", null, null, null, Now.AddMinutes(9))).Refusal);
    }
}
