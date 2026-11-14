using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Each agent conversation is a numbered turn inside its session: an open writes the first, a step's or the person's word that wakes it
/// adds one, and a limit or a curve move is the line's next turn while the session's id stays its first record's.
/// </summary>
public sealed class SessionTurnsTests : IAsyncLifetime
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

    private async Task<Session> Move(string id, params string[] states)
    {
        Session? moved = null;
        foreach (var state in states)
        {
            var outcome = await _ledger.AdvanceAsync(id, state, null, null, null, Now.AddMinutes(1), limit: state == "failed");
            Assert.Equal(SessionAdvanceRefusal.None, outcome.Refusal);
            moved = outcome.Session;
        }

        return moved!;
    }

    [Fact]
    public async Task An_open_writes_the_first_turn_as_a_start()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "claude-code", Now, profile: "work", tree: "/trees/s-1")).Session!;

        var turn = Assert.Single(SessionTurns.OfLine([(await _sessions.FindAsync(opened.Id))!]));
        Assert.Equal((1, SessionTurns.Start, opened.Id, "claude-code", "work"), (turn.Number, turn.Cause, turn.Part, turn.Agent, turn.Account));
    }

    [Fact]
    public async Task A_limit_move_appends_a_turn_and_the_session_s_id_stays()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var first = (await _ledger.OpenAsync(quest.Id, "claude-code", Now, profile: "work", tree: "/trees/s-2")).Session!;
        await Move(first.Id, "starting", "working", "failed");

        var moved = (await _ledger.OpenAsync(quest.Id, "claude-code", Now.AddMinutes(2), profile: "home", tree: "/trees/s-2",
            from: first.Id, fromWhy: "limit", fromBy: "daoris")).Session!;

        var line = (await SessionTurnLines.ReadAsync(_sessions, [moved]))[moved.Id];
        Assert.Equal([(1, SessionTurns.Start, first.Id, "work"), (2, SessionTurns.Limit, moved.Id, "home")],
            line.Select(turn => (turn.Number, turn.Cause, turn.Part!, turn.Account!)));
        Assert.Equal(first.Id, line[0].Part);
        Assert.Equal(line[1].At, line[0].Ended);
    }

    [Fact]
    public async Task A_step_s_word_that_wakes_it_adds_a_turn_naming_the_step_and_its_attempt()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "codex", Now, tree: "/trees/s-3")).Session!;
        await Move(opened.Id, "starting", "working", "awaiting-step");

        await _ledger.SayAsync(opened.Id, "Daoris: `check` failed.", null, Now.AddMinutes(3), from: SaidFrom.Daoris, key: "changes/r/check/2");
        var woken = (await _ledger.AdvanceAsync(opened.Id, "working", null, null, null, Now.AddMinutes(4))).Session!;

        Assert.Equal([(1, SessionTurns.Start, null, null), (2, SessionTurns.Step, "check", 2)],
            SessionTurns.OfLine([woken]).Select(turn => (turn.Number, turn.Cause, turn.Node, turn.Attempt)));
    }

    [Fact]
    public async Task The_person_s_word_that_wakes_it_adds_the_person_s_turn()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "codex", Now, tree: "/trees/s-4")).Session!;
        await Move(opened.Id, "starting", "working", "completed");

        await _ledger.SayAsync(opened.Id, "One more thing.", null, Now.AddMinutes(3));
        var woken = (await _ledger.AdvanceAsync(opened.Id, "working", null, null, null, Now.AddMinutes(4))).Session!;

        Assert.Equal([SessionTurns.Start, SessionTurns.Person], SessionTurns.Own(woken).Select(turn => turn.Cause));
    }

    [Fact]
    public async Task A_record_from_before_turns_reads_its_from_link_as_its_turn()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var first = (await _ledger.OpenAsync(quest.Id, "claude-code", Now, tree: "/trees/s-5")).Session!;
        await Move(first.Id, "starting", "working", "failed");
        var moved = (await _ledger.OpenAsync(quest.Id, "codex", Now.AddMinutes(2), tree: "/trees/s-5",
            from: first.Id, fromWhy: "curve", fromBy: "daoris")).Session!;
        await using (var forget = _connection.CreateCommand())
        {
            forget.CommandText = "UPDATE sessions SET turns = NULL";
            await forget.ExecuteNonQueryAsync();
        }

        var line = SessionTurns.OfLine([(await _sessions.FindAsync(first.Id))!, (await _sessions.FindAsync(moved.Id))!]);

        Assert.Equal([(1, SessionTurns.Start, "claude-code"), (2, SessionTurns.Curve, "codex")],
            line.Select(turn => (turn.Number, turn.Cause, turn.Agent!)));
    }

    [Fact]
    public async Task A_turn_s_end_keeps_what_its_door_reported_since_the_turns_before_it()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "codex", Now, tree: "/trees/s-6")).Session!;
        await Move(opened.Id, "starting", "working");

        var first = (await _ledger.AdvanceAsync(opened.Id, "awaiting-step", null, null, null, Now.AddMinutes(1), spent: new TurnSpent(1000, 200))).Session!;
        await _ledger.SayAsync(opened.Id, "Daoris: `check` failed.", null, Now.AddMinutes(3), from: SaidFrom.Daoris, key: "changes/r/check/1");
        await _ledger.AdvanceAsync(opened.Id, "working", null, null, null, Now.AddMinutes(4));
        var second = (await _ledger.AdvanceAsync(opened.Id, "awaiting-step", null, null, null, Now.AddMinutes(5), spent: new TurnSpent(1700, 260))).Session!;

        Assert.Equal([(1000L, 200L)], SessionTurns.Own(first).Select(turn => (turn.Input!.Value, turn.Output!.Value)));
        Assert.Equal([(1000L, 200L), (700L, 60L)], SessionTurns.Own(second).Select(turn => (turn.Input!.Value, turn.Output!.Value)));
        var kept = SessionTurns.Own((await _sessions.FindAsync(opened.Id))!);
        Assert.Equal((700L, 60L), (kept[1].Input!.Value, kept[1].Output!.Value));
    }

    [Fact]
    public async Task A_turn_whose_door_reported_nothing_keeps_no_figure()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "codex", Now, tree: "/trees/s-7")).Session!;
        await Move(opened.Id, "starting", "working");

        var ended = (await _ledger.AdvanceAsync(opened.Id, "completed", null, null, null, Now.AddMinutes(1), spent: new TurnSpent(null, null))).Session!;

        Assert.All(SessionTurns.Own(ended), turn => Assert.Equal((null, null), (turn.Input, turn.Output)));
    }

    [Theory]
    [InlineData("check/a1b2", "check_run", null)]
    [InlineData("changes/r/opinion/3", "opinion", 3)]
    [InlineData("loose", null, null)]
    public void A_step_s_key_names_its_node_and_attempt(string key, string? node, int? attempt) =>
        Assert.Equal((node, attempt), SessionTurns.StepOf(key));
}
