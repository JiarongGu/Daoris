using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (D163 point 3; the mixed agents design §2, §5; D191's settling): the driver's judgement of an agent's ask to hand its
/// session over, in process. The person's `handOver` rule alone settles it (`off` refused, `ask` held `waiting` until the person's
/// yes, `on` taken), never Daoris's own offer's conditions; again what the connector judged, since a file may be old; and no second
/// hand-over without a turn of the part's own between.
/// </summary>
public sealed class HandOverAskJudgeTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-20T10:00:00Z");

    private static HandOverAsk Ask(string id = "a1b2c3d4") => new(id, "s1", "The parser is done; ParserTests are left.", At);

    private static PriorSession Record(string kind = "chat") =>
        new("s1", "trees/default/reports/s-1", "working", Repository: "reports") { Workspace = "default", Kind = kind, Adapter = "claude-code" };

    private static HandOverRule Rule(string mode) => new(mode);

    private static HandOverAskVerdict Judge(
        PriorSession? record, string mode, HandOverAskState? state = null, bool turnSince = true, string? next = "codex-acp") =>
        HandOverAskJudge.Judge(Ask(), state, record, Rule(mode), turnSince, next);

    [Fact]
    public void An_ask_with_the_rule_on_goes_to_the_next_agent_in_the_persons_order()
    {
        Assert.Equal(HandOverAskVerdict.Go("codex-acp"), Judge(Record(), HandOverRules.On));
        Assert.Equal(HandOverAskVerdict.Go("codex-acp"), Judge(Record("driven"), HandOverRules.On));
    }

    [Fact]
    public void An_ask_with_the_rule_off_is_refused_with_its_code()
    {
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.Off), Judge(Record(), HandOverRules.Off));
    }

    /// <summary>`ask` holds it `waiting` for the person's word, naming the agent it would go on with; their yes lets it go to that agent.</summary>
    [Fact]
    public void An_ask_with_the_rule_ask_waits_for_the_persons_yes_and_goes_once_approved()
    {
        Assert.Equal(HandOverAskVerdict.Wait("codex-acp"), Judge(Record(), HandOverRules.Ask));
        var waiting = new HandOverAskState("a1b2c3d4", HandOverAskStates.Waiting, null, "codex-acp", At);
        Assert.Equal(HandOverAskVerdict.Wait("codex-acp"), Judge(Record(), HandOverRules.Ask, waiting));

        var approved = new HandOverAskState("a1b2c3d4", HandOverAskStates.Approved, null, "dsh", At);
        Assert.Equal(HandOverAskVerdict.Go("dsh"), Judge(Record(), HandOverRules.Ask, approved));
    }

    /// <summary>An ask taken or refused is past judging; a state naming an earlier ask is that ask's, not this one's.</summary>
    [Fact]
    public void A_settled_ask_is_done_and_an_earlier_asks_state_is_not_this_ones()
    {
        Assert.Equal(HandOverAskVerdict.Done, Judge(Record(), HandOverRules.On, new("a1b2c3d4", HandOverAskStates.Taken, null, "codex-acp", At)));
        Assert.Equal(HandOverAskVerdict.Done, Judge(Record(), HandOverRules.On, new("a1b2c3d4", HandOverAskStates.Refused, HandOverAskWhy.Declined, null, At)));
        Assert.Equal(HandOverAskVerdict.Wait("codex-acp"), Judge(Record(), HandOverRules.Ask, new("e0e0e0e0", HandOverAskStates.Refused, HandOverAskWhy.Off, null, At)));
    }

    /// <summary>What the files cannot vouch for is judged again: a session not here, a teammate's, a helper, a reviewer, an intake, Ask Daoris's.</summary>
    [Fact]
    public void An_asker_gone_or_unfit_is_refused_whatever_the_rule()
    {
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.AskerGone), Judge(null, HandOverRules.On));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.AskerGone), Judge(new PriorSession("mac/s1", null), HandOverRules.On));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.AskerUnfit), Judge(Record() with { Serves = "s0" }, HandOverRules.On));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.AskerUnfit), Judge(Record() with { Opinion = "op1" }, HandOverRules.On));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.AskerUnfit), Judge(Record() with { Ask = "a1" }, HandOverRules.On));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.AskerUnfit), Judge(Record() with { HelpMode = "restricted" }, HandOverRules.On));
    }

    /// <summary>
    /// 🔴 No second hand-over without a turn between (design §5; D191): the record of a part a hand-over started says so (its `from`
    /// with a hand-over's why), and an ask before that part's first turn ended is refused with that reason, whatever the rule. A part
    /// begun another way (a carry-on, a resume) asks as any does.
    /// </summary>
    [Fact]
    public void A_part_a_hand_over_started_cannot_ask_again_before_its_first_turn_ends()
    {
        var handed = Record() with { From = "s0", FromWhy = HandOverWhy.Agent, FromBy = HandOverBy.Agent };

        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.JustHanded), Judge(handed, HandOverRules.On, turnSince: false));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.JustHanded), Judge(handed with { FromWhy = HandOverWhy.Press }, HandOverRules.Ask, turnSince: false));
        Assert.Equal(HandOverAskVerdict.Go("codex-acp"), Judge(handed, HandOverRules.On, turnSince: true));
        Assert.Equal(HandOverAskVerdict.Go("codex-acp"), Judge(Record() with { From = "s0", FromWhy = "carry-on" }, HandOverRules.On, turnSince: false));
    }

    /// <summary>No other agent in the person's order: refused with its code, never waiting on a yes that could start nothing.</summary>
    [Fact]
    public void No_other_agent_is_refused_and_off_is_said_before_it()
    {
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.NoAgent), Judge(Record(), HandOverRules.Ask, next: null));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.NoAgent), Judge(Record(), HandOverRules.On, next: null));
        Assert.Equal(HandOverAskVerdict.Refuse(HandOverAskWhy.Off), Judge(Record(), HandOverRules.Off, next: null));
    }

    /// <summary>Each refusal's reason is worded once in the terminal's English, and each has its page twin (`note.handover.why.*`).</summary>
    [Fact]
    public void Every_reason_is_said_and_declared_for_the_page()
    {
        foreach (var why in HandOverAskWhy.All)
        {
            Assert.NotEqual(why, HandOverAskWhy.Said(why));
            Assert.Contains(NoteCodes.HandOver.All, reason => reason.Code == why);
        }
    }

    /// <summary>
    /// 🔴 HANDOVERSAID1: each asker is named by the path that made the ask: the agent's own (its tool), the person at a terminal, the
    /// person by the window's press, each on a code of its own the page words.
    /// </summary>
    [Fact]
    public void The_ask_note_names_its_asker()
    {
        var agent = HandOverAskNotes.Asked("codex-acp");
        var terminal = HandOverAskNotes.AskedBy(HandOverAsker.Terminal, "codex-acp");
        var window = HandOverAskNotes.AskedBy(HandOverAsker.Window, "codex-acp");

        Assert.Equal(["handover.asked", "handover.asked-terminal", "handover.asked-window"],
            [agent.Parts[0].Code, terminal.Parts[0].Code, window.Parts[0].Code]);
        Assert.StartsWith("Its agent asked to hand this session over", agent.Note, StringComparison.Ordinal);
        Assert.Equal("You asked at a terminal to hand this session over to codex-acp: Daoris takes it at its next look.", terminal.Note);
        Assert.Equal("You asked in the window to hand this session over to codex-acp: Daoris takes it at its next look.", window.Note);
    }

    /// <summary>
    /// 🔴 HANDOVERSAID1: a hand-over let go that Daoris could not take is said as not taken, never refused, with the take's own reason in
    /// words and what the session does next; a reason the take names that is no code here is `cannot`.
    /// </summary>
    [Fact]
    public void A_hand_over_not_taken_says_its_reason_and_what_the_session_does_next()
    {
        var closed = HandOverAskNotes.NotTaken("codex-acp", HandOver.QuestClosed);
        var unknown = HandOverAskNotes.NotTaken("codex-acp", HandOver.UnknownAgent);
        var odd = HandOverAskNotes.NotTaken("codex-acp", "not-found");

        Assert.Equal(("handover.not-taken-ends", "quest-closed"), (closed.Parts[0].Code, Why(closed)));
        Assert.Equal(
            "The hand-over to codex-acp was not taken: its quest is closed, so no work is left to hand over. Nothing goes on with this session.",
            closed.Note);
        Assert.Equal(("handover.not-taken", "unknown-agent"), (unknown.Parts[0].Code, Why(unknown)));
        Assert.EndsWith("This session goes on with its own agent.", unknown.Note, StringComparison.Ordinal);
        Assert.Equal("cannot", Why(odd));
        Assert.DoesNotContain("refused", closed.Note + unknown.Note + odd.Note, StringComparison.Ordinal);
    }

    private static object Why(Noted note) => note.Parts[0].Values.Single(value => value.Key == "why").Value;
}
