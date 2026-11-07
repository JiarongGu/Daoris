using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The protocol door hands each frame to the stages its modules register, phase by phase in their order, each built from the
/// session as its wiring; an order the marks cannot make, or a stage the session cannot build, is refused at its first frame and
/// never part-way through one. The open prompts' state its units share keeps its invariants on its own: a prompt is open from its
/// admission, the shut door turns away only what it had not admitted, and a word is taken once.
/// </summary>
public sealed class AcpPartsTests
{
    [Fact]
    public void The_session_runs_the_registered_stages_in_their_order_each_built_from_the_session()
    {
        var session = new AcpSession(new StringReader(""), TextWriter.Null, _ => { });

        var order = session.TurnStages.Order;
        Assert.Equal(["replay", "watch", "heard", "subagent", "measure", "render", "limit", "permission"], order.Select(part => part.Name));
        Assert.Equal(
            [.. Enumerable.Repeat(TurnPhase.Update, 7), TurnPhase.Request], order.Select(part => (TurnPhase)part.Phase));

        // What each anchor runs, in the order it runs them: a replay is passed over first, a subagent's update is routed away before
        // the session measures and renders its own.
        Assert.Collection(
            session.TurnStages.In(TurnPhase.Update),
            stage => Assert.IsType<AcpReplay>(stage),
            stage => Assert.IsType<AcpWatch>(stage),
            stage => Assert.IsType<AcpHeard>(stage),
            stage => Assert.IsType<AcpSubagent>(stage),
            stage => Assert.IsType<AcpMeasure>(stage),
            stage => Assert.IsType<AcpRender>(stage),
            stage => Assert.IsType<AcpLimitTurn>(stage));
        Assert.IsType<AcpPermission>(Assert.Single(session.TurnStages.In(TurnPhase.Request)));

        // Built once per session: the root reads a module's other doors from the same stage the frames pass.
        Assert.Same(session.TurnStages, session.TurnStages);
        Assert.Null(session.Usage);
    }

    [Fact]
    public void The_units_the_session_hands_its_work_to_are_built_on_first_use_and_stand_idle_before_a_run()
    {
        var session = new AcpSession(new StringReader(""), TextWriter.Null, _ => { });

        Assert.False(session.WaitsOnOwnWork);
        Assert.False(session.WaitsOnTransient);
        Assert.Empty(session.ConfigOptions);
        Assert.Empty(session.OpeningOptions);
        Assert.Null(session.NotOffered);
        Assert.Null(session.CarriesOn);
        Assert.Null(session.LastAcknowledgedOptions);
        Assert.Empty(session.HandedBack);
        Assert.Empty(session.Unreached);
        Assert.Empty(session.LeftRunning);
        Assert.False(session.NextStep);
        Assert.False(session.Steers);
    }

    [Fact]
    public void A_prompt_opens_as_it_is_admitted_and_the_shut_door_turns_away_only_what_it_had_not_admitted()
    {
        var open = new OpenPrompts();
        ChatMessage first = new("first", []), second = new("second", []), late = new("late", []);

        var written = open.Enter([first]);
        var prompt = open.Opened(written, marked: true, first);
        Assert.True(prompt.Opening);
        Assert.False(open.Prompted);
        Assert.True(open.Admit(written, prompt));
        Assert.True(open.Prompted);

        // Shut between two writes: the passage not yet admitted goes back, and so does one that comes to the door after.
        var waiting = open.Enter([second]);
        open.Shut();
        Assert.True(open.DoorShut);
        Assert.False(open.Admit(waiting, new OpenPrompt(second)));
        Assert.Throws<AcpTurnedAway>(() => open.Enter([late]));
        Assert.Equal<IAgentWord>([second, late], open.HandedBack);

        // The admitted one is the session's, never handed back: untaken at the end, it is unreached.
        Assert.Empty(open.Untaken(retry: null));
        Assert.Equal<IAgentWord>([first], open.Unreached);
        Assert.False(open.Prompted);
    }

    [Fact]
    public void An_answer_hands_the_turn_to_the_words_behind_it_and_an_error_hands_it_to_nothing_until_the_agent_goes_on()
    {
        var open = new OpenPrompts();
        ChatMessage first = new("first", []), second = new("second", []), third = new("third", []);
        var prompts = new[] { first, second, third }.Select(words =>
        {
            var passage = open.Enter([words]);
            var prompt = open.Opened(passage, marked: true, words);
            Assert.True(open.Admit(passage, prompt));
            return prompt;
        }).ToList();

        // Prompted to an idle session, the first words are taken by the first thing the agent does for them, never before the wire.
        Assert.Same(prompts[0], open.TakeLate());
        Assert.Same(prompts[1], open.Leaves(prompts[0], answered: true, retry: null, out var last));
        Assert.False(last);
        Assert.True(open.Took(prompts[1]));

        // An error answers the second: the third is not taken by it, only by the agent's next update, once.
        Assert.Null(open.Leaves(prompts[1], answered: false, retry: null, out last));
        Assert.False(open.Took(prompts[2]));
        Assert.Same(prompts[2], open.TakeLate());
        Assert.Null(open.TakeLate());
        Assert.Null(open.Leaves(prompts[2], answered: true, retry: null, out last));
        Assert.True(last);
    }

    [Fact]
    public void A_mark_that_comes_back_after_the_door_shut_or_unmarked_writes_nothing()
    {
        var open = new OpenPrompts();
        ChatMessage unmarked = new("unmarked", []), late = new("late", []);

        var refused = open.Enter([unmarked]);
        Assert.Throws<AcpWordsUnmarked>(() => open.Opened(refused, marked: false, unmarked));

        var cut = open.Enter([late]);
        open.Shut();
        Assert.Throws<AcpTurnedAway>(() => open.Marked(cut, marked: true));

        // The unmarked words wait on their record, never handed back; the cut ones go back with the run's.
        Assert.Equal<IAgentWord>([late], open.HandedBack);
    }

    [Fact]
    public void A_phase_runs_each_stage_after_the_ones_it_names_and_a_cycle_is_refused_by_its_members()
    {
        var order = PartOrder.Of(
        [
            Declared("permission", TurnPhase.Request),
            Declared("audit", TurnPhase.Update, "render"),
            Declared("render", TurnPhase.Update, "measure"),
            Declared("measure", TurnPhase.Update),
        ]);
        Assert.Equal(["measure", "render", "audit", "permission"], order.Select(part => part.Name));

        var cycle = Assert.Throws<PartOrderException>(() => PartOrder.Of(
            [Declared("measure", TurnPhase.Update, "render"), Declared("render", TurnPhase.Update, "measure"), Declared("permission", TurnPhase.Request)]));
        Assert.Equal("the parts measure → render → measure come after each other, so none of them can run first", cycle.Message);
    }

    [Fact]
    public void An_update_stage_after_a_request_stage_and_a_stage_its_session_cannot_build_are_refused()
    {
        Assert.Equal(
            "the part render runs in Update and comes after permission, which runs later, in Request",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("render", TurnPhase.Update, "permission"), Declared("permission", TurnPhase.Request)])).Message);

        // A constructor asking for wiring the session does not hand fails at the first frame, naming the stage.
        var unbuilt = Assert.Throws<PartOrderException>(() => Parts.Build<ITurnStage>(
            [Declared("render", TurnPhase.Update)], type => (ITurnStage)Activator.CreateInstance(type, new object())!));
        Assert.StartsWith("the part render (AcpRender) could not be built from its root's wiring: ", unbuilt.Message);
    }

    private static PartDeclaration Declared(string name, TurnPhase phase, params string[] after) =>
        new(name, phase, after, typeof(AcpRender));
}
