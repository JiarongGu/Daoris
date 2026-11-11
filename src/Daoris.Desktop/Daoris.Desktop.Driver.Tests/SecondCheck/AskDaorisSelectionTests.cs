using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which of a turn's calls the second check reads, by the design's table over fixture events logs and lineage lines: a known read never,
/// an act, an unknown call and a press of Daoris's connector always, delegated work where its stream is kept and said where not, and what
/// the person said yes to on a card or a hold never, said as theirs. Cut evidence and an interrupted turn are limits of the reading.
/// </summary>
public sealed class AskDaorisSelectionTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private sealed class Log
    {
        private readonly List<SessionEvent> _events = [];

        public IReadOnlyList<SessionEvent> Events => _events;

        public DateTimeOffset Now => Start.AddSeconds(_events.Count);

        public Log Add(SessionEvent e)
        {
            _events.Add(e with { Seq = _events.Count + 1, At = Start.AddSeconds(_events.Count + 1) });
            return this;
        }

        public Log User(string id, string words = "do the thing", string? reaches = null) =>
            Add(new SessionEvent { Kind = SessionEventKind.User, Id = id, Text = words, Origin = "person", Reaches = reaches });

        public Log Tool(string id, string kind, string? title = null, string? tool = null, string? stream = null, string? input = null, string? output = null) =>
            Add(new SessionEvent { Kind = SessionEventKind.Tool, Id = id, ToolKind = kind, Title = title ?? kind, ToolName = tool, Stream = stream, Status = "completed", Input = input, Output = output });

        public Log Turn() => Add(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
    }

    private static ConnectorCall Line(Log log, string tool, string? act, string? toolUse = null) => new(log.Now, tool, act, toolUse);

    private static ReadTurn One(Log log, IReadOnlyList<ConnectorCall>? calls = null, IReadOnlyList<TurnPress>? presses = null,
        IReadOnlySet<string>? held = null, Func<string, IReadOnlyList<SessionEvent>?>? streams = null) =>
        Assert.Single(AskDaorisSelection.Turns(new TurnEvidence(log.Events, calls ?? [], presses ?? []) { Held = held ?? new HashSet<string>(), Streams = streams }));

    [Fact]
    public void A_turn_of_known_reads_alone_is_not_selected_and_its_reads_are_counted_by_kind()
    {
        var turn = One(new Log().User("w1").Tool("c1", "read").Tool("c2", "search").Tool("c3", "fetch").Tool("c4", "think").Tool("c4", "read").Turn());

        Assert.False(turn.Selected);
        Assert.Empty(turn.Calls);
        Assert.Equal(2, turn.Reads["read"]);
        Assert.Equal((1, 1), (turn.Reads["search"], turn.Reads["fetch"]));
        Assert.Equal(("w1", "do the thing", false), (turn.Id, turn.Words, turn.Interrupted));
    }

    [Fact]
    public void An_edit_an_execute_a_delete_and_a_move_are_selected_as_acts()
    {
        var turn = One(new Log().User("w1").Tool("c1", "edit").Tool("c2", "execute", input: "git push").Tool("c3", "delete").Tool("c4", "move").Turn());

        Assert.True(turn.Selected);
        Assert.Equal(["c1", "c2", "c3", "c4"], turn.Calls.Select(call => call.Id));
        Assert.All(turn.Calls, call => Assert.Equal(SelectedWhy.Act, call.Why));
        Assert.Equal("git push", turn.Calls[1].Input);
    }

    [Fact]
    public void On_claude_code_s_door_a_connector_call_is_classed_by_its_line_matched_by_the_call_s_id()
    {
        var log = new Log().User("w1").Tool("toolu_1", "other", tool: "knowledge_search").Tool("toolu_2", "other", tool: "settings_cap").Tool("toolu_3", "other").Turn();
        ConnectorCall[] lines = [Line(log, "knowledge_search", "search", "toolu_1"), Line(log, "settings_cap", "press", "toolu_2"), Line(log, "housekeeping_clean", "press", "toolu_9")];

        var turn = One(log, lines);

        Assert.Equal(1, turn.Reads["connector"]);
        Assert.Equal(
            [("toolu_2", SelectedWhy.Press, "settings_cap"), ("toolu_3", SelectedWhy.Unknown, null), ("line-3", SelectedWhy.Press, "housekeeping_clean")],
            turn.Calls.Select(call => (call.Id, call.Why, call.Tool)));
        Assert.DoesNotContain(turn.Limits, limit => limit.Code == ReadLimit.ByCount);
    }

    [Fact]
    public void On_codex_s_door_connector_lines_account_for_as_many_other_cards_as_they_number_and_the_rest_are_unknown()
    {
        var log = new Log().User("w1").Tool("call_a", "other").Tool("call_b", "other").Tool("call_c", "other").Turn();
        ConnectorCall[] lines = [Line(log, "knowledge_search", "search"), Line(log, "branch_discard", "press")];

        var turn = One(log, lines);

        Assert.Equal(1, turn.Reads["connector"]);
        Assert.Equal([("call_b", SelectedWhy.Press), ("call_c", SelectedWhy.Unknown)], turn.Calls.Select(call => (call.Id, call.Why)));
        Assert.Contains(turn.Limits, limit => limit.Code == ReadLimit.ByCount);
    }

    [Fact]
    public void A_proposal_through_the_connector_is_a_card_the_person_applies_and_is_not_selected()
    {
        var log = new Log().User("w1").Tool("toolu_1", "other").Turn();

        var turn = One(log, [Line(log, "workflow_propose", "propose", "toolu_1")]);

        Assert.False(turn.Selected);
        Assert.Equal(1, turn.Reads["proposed"]);
    }

    [Fact]
    public void A_delegation_is_selected_its_kept_child_read_by_the_same_table_and_one_not_kept_said_delegated_not_read()
    {
        var log = new Log().User("w1").Tool("c1", "think", title: "Agent", stream: "child-1").Tool("c2", "think", tool: "Task").Turn();
        var child = new Log().Tool("k1", "read").Tool("k2", "edit");

        var turn = One(log, streams: stream => stream == "child-1" ? child.Events : null);

        Assert.Equal([("c1", SelectedWhy.Delegated), ("c1/k2", SelectedWhy.Act), ("c2", SelectedWhy.Delegated)], turn.Calls.Select(call => (call.Id, call.Why)));
        Assert.Equal([(ReadLimit.Delegated, "c2")], turn.Limits.Select(limit => (limit.Code, limit.Call)));
    }

    [Fact]
    public void A_press_asked_on_the_person_s_card_and_an_answered_hold_are_the_person_s_yes_never_selected()
    {
        var log = new Log().User("w1").Tool("c1", "execute").Turn();
        TurnPress[] presses = [new(Start.AddSeconds(2), "QUEST_DELETE", "repository:atlas", "allowed", Asked: true)];

        var turn = One(log, presses: presses, held: new HashSet<string> { "c1" });

        Assert.False(turn.Selected);
        Assert.Equal(["c1"], turn.Yours);
        Assert.Equal([("QUEST_DELETE", true)], turn.Presses.Select(press => (press.Route, press.Yours)));
    }

    [Fact]
    public void A_granted_family_s_press_and_an_act_the_allow_list_let_run_are_selected()
    {
        var log = new Log().User("w1").Tool("c1", "execute").Turn();
        TurnPress[] presses = [new(Start.AddSeconds(2), "WORK_PAUSE", "repository:atlas", "done", Asked: false), new(Start.AddSeconds(2), "ASK_DELETE", null, "refused", Asked: true)];

        var turn = One(log, presses: presses);

        Assert.True(turn.Selected);
        Assert.Equal(["c1"], turn.Calls.Select(call => call.Id));
        Assert.Equal([("WORK_PAUSE", false)], turn.Presses.Select(press => (press.Route, press.Yours)));
    }

    [Fact]
    public void A_turn_with_no_turn_event_ends_at_the_session_s_last_event_interrupted_and_a_word_shown_waiting_is_no_turn()
    {
        var log = new Log().User("w0", reaches: "turn-end").User("w1").Tool("c1", "edit").Tool("c1", "edit", output: "ok");

        var turn = One(log);

        Assert.Equal(("w1", 2L, 4L, true), (turn.Id, turn.From, turn.To, turn.Interrupted));
        Assert.Contains(turn.Limits, limit => limit.Code == ReadLimit.Interrupted);
        Assert.Equal("ok", Assert.Single(turn.Calls).Output);
    }

    [Fact]
    public void A_call_whose_record_cut_its_input_or_output_is_a_limit_of_the_reading()
    {
        var cut = TextBounds.ClipCounted(new string('x', 3000), SessionEventText.RawLimit);
        var turn = One(new Log().User("w1").Tool("c1", "execute", input: "make", output: cut).Turn());

        Assert.Equal([(ReadLimit.Cut, "c1")], turn.Limits.Select(limit => (limit.Code, limit.Call)));
        Assert.Equal(["output"], turn.Calls[0].Cut);
    }

    [Fact]
    public void Each_turn_of_a_log_is_its_own_from_its_taken_words_to_its_end()
    {
        var log = new Log().User("w1").Tool("c1", "read").Turn().User("w2").Tool("c2", "edit").Turn();

        var turns = AskDaorisSelection.Turns(new TurnEvidence(log.Events, [], []));

        Assert.Equal([("w1", 1L, 3L, false), ("w2", 4L, 6L, true)], turns.Select(turn => (turn.Id, turn.From, turn.To, turn.Selected)));
    }
}
