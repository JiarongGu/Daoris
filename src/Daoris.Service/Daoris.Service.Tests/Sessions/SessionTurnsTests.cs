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
    public async Task An_open_names_the_first_turn_s_step_and_attempt()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "claude-code", Now, tree: "/trees/s-8", node: "work", attempt: 1)).Session!;

        var kept = SessionTurns.Own((await _sessions.FindAsync(opened.Id))!);

        Assert.Equal([("work", 1)], kept.Select(turn => (turn.Node!, turn.Attempt!.Value)));
    }

    [Fact]
    public async Task A_turn_keeps_its_own_agent_account_and_transcript_which_its_record_no_longer_names()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "claude-code", Now, doorVersion: "2.1", profile: "work", tree: "/trees/s-9")).Session!;
        await Move(opened.Id, "starting", "working");
        await _ledger.AdvanceAsync(opened.Id, "awaiting-step", null, null, "/logs/conv-one.jsonl", Now.AddMinutes(1));
        await _ledger.SayAsync(opened.Id, "Daoris: `check` failed.", null, Now.AddMinutes(3), from: SaidFrom.Daoris, key: "changes/r/check/1");
        await _ledger.AdvanceAsync(opened.Id, "working", null, null, null, Now.AddMinutes(4));

        var record = (await _sessions.FindAsync(opened.Id))!;
        var elsewhere = record with { Profile = "home", Door = "codex", DoorVersion = "9", Transcript = "/logs/conv-two.jsonl" };
        var line = SessionTurns.OfLine([elsewhere]);

        Assert.Equal(
            [("claude-code", "work", "2.1", "conv-one"), ("claude-code", "work", "2.1", "conv-one")],
            line.Select(turn => (turn.Agent!, turn.Account!, turn.Adapter!, turn.Conversation!)));
        Assert.All(line, turn => Assert.Equal("/logs/conv-one.jsonl", turn.Transcript));
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

    [Fact]
    public async Task A_cut_record_that_goes_on_with_Daoris_s_word_keeps_its_first_turn_s_end_and_wakes_a_daoris_turn()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "codex", Now, tree: "/trees/s-10")).Session!;
        await Move(opened.Id, "starting", "working");
        await _ledger.AdvanceAsync(
            opened.Id, "stopped", "the driver was stopped while this ran.", null, null, Now.AddMinutes(1), interrupted: true,
            noteParts: """[{"code":"ended.driver-closed","text":"the driver was stopped while this ran."}]""");

        await _ledger.SayAsync(opened.Id, "Daoris restarted while you were working. Go on where you stopped.", null, Now.AddMinutes(2),
            from: SaidFrom.Daoris, key: $"restart/{opened.Id}/2026-11-15T10-01-00.0000000Z");
        var woken = (await _ledger.AdvanceAsync(opened.Id, "working", null, null, null, Now.AddMinutes(3))).Session!;

        var kept = SessionTurns.Own((await _sessions.FindAsync(opened.Id))!);
        Assert.Equal([SessionTurns.Start, SessionTurns.Daoris], kept.Select(turn => turn.Cause));
        Assert.Equal(new SessionTurnEnd("stopped", Interrupted: true, Codes: ["ended.driver-closed"]), kept[0].End);
        Assert.Null(kept[1].End);
        Assert.False(woken.Interrupted);
    }

    [Fact]
    public async Task A_carry_on_reads_the_same_turns_appended_to_its_record_as_chained_in_a_new_one()
    {
        async Task<Session> CutAsync(string tree)
        {
            var quest = await _quests.PublishAsync("Asker", "Owner", $"Do the thing in {tree}", "Here is why.", Now);
            var opened = (await _ledger.OpenAsync(quest.Id, "codex", Now, profile: "work", tree: tree)).Session!;
            await Move(opened.Id, "starting", "working");
            await _ledger.AdvanceAsync(opened.Id, "stopped", null, null, null, Now.AddMinutes(1), interrupted: true);
            return opened;
        }

        var first = await CutAsync("/trees/s-12");
        var chained = (await _ledger.OpenAsync(first.Quest!, "codex", Now.AddMinutes(2), profile: "work", tree: "/trees/s-12",
            from: first.Id, fromWhy: "carry-on", fromBy: "daoris")).Session!;
        var cut = await CutAsync("/trees/s-13");
        await _ledger.SayAsync(cut.Id, "Daoris restarted while you were working. Go on where you stopped.", null, Now.AddMinutes(2),
            from: SaidFrom.Daoris, key: $"restart/{cut.Id}/2026-11-15T10-01-00.0000000Z");
        await _ledger.AdvanceAsync(cut.Id, "working", null, null, null, Now.AddMinutes(2));
        var appended = (await _sessions.FindAsync(cut.Id))!;

        static IEnumerable<(int, string, string?, string?)> Rows(IReadOnlyList<SessionTurn> turns) =>
            turns.Select(turn => (turn.Number, turn.Cause, turn.Agent, turn.Account));
        var asChain = (await SessionTurnLines.ReadAsync(_sessions, [chained]))[chained.Id];
        var asTurns = (await SessionTurnLines.ReadAsync(_sessions, [appended]))[appended.Id];

        Assert.Equal(Rows(asChain), Rows(asTurns));
        Assert.Equal([first.Id, chained.Id], asChain.Select(turn => turn.Part!));
        Assert.Equal([cut.Id, cut.Id], asTurns.Select(turn => turn.Part!));
    }

    [Fact]
    public async Task A_limited_end_a_go_on_forgives_stays_on_its_turn()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now);
        var opened = (await _ledger.OpenAsync(quest.Id, "codex", Now, tree: "/trees/s-11")).Session!;
        await Move(opened.Id, "starting", "working", "failed");
        await _ledger.SayAsync(opened.Id, "One more thing.", null, Now.AddMinutes(3));
        await _ledger.AdvanceAsync(opened.Id, "working", null, null, null, Now.AddMinutes(4));

        var kept = SessionTurns.Own((await _sessions.FindAsync(opened.Id))!);
        Assert.Equal([new SessionTurnEnd("failed", Limit: true, Codes: []), null], kept.Select(turn => turn.End));
        Assert.Equal(SessionTurns.Person, kept[1].Cause);
    }

    [Theory]
    [InlineData("check/a1b2", "check_run", null)]
    [InlineData("changes/r/opinion/3", "opinion", 3)]
    [InlineData("loose", null, null)]
    public void A_step_s_key_names_its_node_and_attempt(string key, string? node, int? attempt) =>
        Assert.Equal((node, attempt), SessionTurns.StepOf(key));
}
