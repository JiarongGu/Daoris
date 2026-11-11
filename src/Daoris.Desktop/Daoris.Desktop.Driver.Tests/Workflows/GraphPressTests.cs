using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A door that holds on a run's graph for a judgement not made yet asks the loop for a whole pass, and
/// the person's press, its answer kept first, waits for that pass within a bound, reads the gate again and lands at the same press where the
/// pass made <c>handed</c> active; past the bound it is refused as read, its answer kept, and the next press or look lands it. A wait the next
/// pass may move asks one, and the press does not wait. The loop's asks (<see cref="PassAsks"/>): a pass takes the asks made before it began,
/// and one made while it runs waits for the next. Pure, over stand-ins: the fast half.
/// </summary>
public sealed class GraphPressTests
{
    private static LandingGate Gate(GraphGateState? graph, bool opinionHolds = false) =>
        new(new OpinionGateState(opinionHolds ? OpinionGateStates.NotAsked : OpinionGateStates.None, "engine"),
            new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "engine" }))
        {
            Graph = graph,
        };

    private static readonly LandingGate NotJudged = Gate(new GraphGateState(GraphGateStates.NotJudged, "q7") { Handed = "land", Commit = "abc" });

    private static readonly LandingGate Lands = Gate(new GraphGateState(GraphGateStates.Lands, "q7") { Handed = "land", Commit = "abc" }) with { Tip = "abc" };

    private static readonly LandingGate Waits = Gate(new GraphGateState(GraphGateStates.Waits, "q7") { Handed = "land", Node = "qa", Kind = NodeKinds.Human });

    /// <summary>What the press did, in its order: the answer kept, each pass asked with its bound, each gate read.</summary>
    private readonly List<string> _did = [];

    private Task<LandingGate> Answered(LandingGate gate)
    {
        _did.Add("answer kept");
        return Task.FromResult(gate);
    }

    private Func<CancellationToken, Task<LandingGate>> Reads(LandingGate gate) => _ =>
    {
        _did.Add("gate read");
        return Task.FromResult(gate);
    };

    private Func<string, TimeSpan, CancellationToken, Task<bool>> Passes(bool comes) => (unit, bound, _) =>
    {
        _did.Add($"pass asked for {unit}, waiting {bound.TotalSeconds:0}s");
        return Task.FromResult(comes && bound > TimeSpan.Zero);
    };

    private static readonly string Waited = $"pass asked for run:q7, waiting {GraphPasses.Bound.TotalSeconds:0}s";

    [Fact]
    public async Task A_press_held_for_a_judgement_not_made_lands_at_the_same_press_where_the_pass_comes_within_the_bound()
    {
        var gate = await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(Lands), Passes(comes: true), CancellationToken.None);

        Assert.Same(Lands, gate);
        Assert.True(gate.LetsGo);
        Assert.Equal(["answer kept", Waited, "gate read"], _did);
    }

    [Fact]
    public async Task Past_the_bound_the_press_is_refused_as_read_its_answer_kept_saying_the_pass_it_asked_judges_its_run_first()
    {
        var gate = await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(Lands), Passes(comes: false), CancellationToken.None);

        Assert.Equal(LandingCode.Refused, gate.Refusal!.Refusal);
        Assert.Equal(
            $"Daoris has not judged run q7's graph at `abc` yet: the pass this asked did not end within {GraphPasses.Bound.TotalSeconds:0} seconds, "
            + "and judges this run first when it does.", gate.Refusal.Message);
        Assert.Equal(["answer kept", Waited], _did);
    }

    [Fact]
    public async Task A_press_whose_pass_judged_its_run_and_left_it_unready_says_what_the_judgement_found()
    {
        var unready = Gate(new GraphGateState(GraphGateStates.NotJudged, "q7")
        {
            Handed = "land", Commit = "abc", HandedSaid = "Ready: nothing before it holds it.", HandedAt = "def",
        });

        var gate = await GraphPasses.PressAsync(_ => Answered(unready), Reads(unready), Passes(comes: true), CancellationToken.None);

        Assert.Equal(LandingCode.Refused, gate.Refusal!.Refusal);
        Assert.Equal("Daoris judged run q7's graph just now, and `land` does not land `abc` yet: Ready: nothing before it holds it. "
                     + "It is bound to `def`.", gate.Refusal.Message);
        Assert.Equal(["answer kept", Waited, "gate read"], _did);
    }

    [Fact]
    public async Task A_wait_the_next_pass_may_move_asks_one_and_the_press_does_not_wait_and_a_hold_before_the_graphs_asks_none()
    {
        Assert.Same(Waits, await GraphPasses.PressAsync(_ => Answered(Waits), Reads(Lands), Passes(comes: true), CancellationToken.None));
        Assert.Equal(["answer kept", "pass asked for run:q7, waiting 0s"], _did);

        _did.Clear();
        var opinion = Gate(NotJudged.GraphPart()!, opinionHolds: true);
        Assert.Same(opinion, await GraphPasses.PressAsync(_ => Answered(opinion), Reads(Lands), Passes(comes: true), CancellationToken.None));
        Assert.Equal(["answer kept"], _did);

        // No loop to ask (a terminal's press alone): refused as read.
        _did.Clear();
        Assert.Same(NotJudged, await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(Lands), pass: null, CancellationToken.None));
        Assert.Equal(["answer kept"], _did);
    }

    /// <summary>A press on a run still on its way to its review is kept as that review's Accept before the pass is asked.</summary>
    [Fact]
    public async Task A_press_before_the_graph_is_judged_or_the_review_waits_is_kept_as_the_reviews_accept()
    {
        var kept = new List<string>();
        Task<bool> Keep(GraphGateState graph, CancellationToken _)
        {
            kept.Add(graph.State);
            _did.Add("accept kept");
            return Task.FromResult(true);
        }

        await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(NotJudged), Passes(comes: true), CancellationToken.None, keep: Keep);
        Assert.Equal(["answer kept", "accept kept", Waited, "gate read"], _did);

        _did.Clear();
        Assert.Same(Waits, await GraphPasses.PressAsync(_ => Answered(Waits), Reads(Lands), Passes(comes: true), CancellationToken.None, keep: Keep));
        Assert.Equal(["answer kept", "accept kept", "pass asked for run:q7, waiting 0s"], _did);
        Assert.Equal([GraphGateStates.NotJudged, GraphGateStates.Waits], kept);

        // A hold before the graph's, and a press with no loop to ask, keep nothing.
        _did.Clear();
        var opinion = Gate(NotJudged.GraphPart()!, opinionHolds: true);
        await GraphPasses.PressAsync(_ => Answered(opinion), Reads(Lands), Passes(comes: true), CancellationToken.None, keep: Keep);
        await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(Lands), pass: null, CancellationToken.None, keep: Keep);
        Assert.Equal(["answer kept", "answer kept"], _did);
    }

    [Fact]
    public async Task A_pass_takes_the_asks_made_before_it_began_and_one_made_while_it_runs_waits_for_the_next()
    {
        var woken = 0;
        var asks = new PassAsks(() => woken++);

        var first = asks.WaitAsync(null, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Equal(1, woken);
        Assert.True(asks.Asked);
        var taken = asks.Take();
        Assert.False(asks.Asked);
        var during = asks.WaitAsync(null, TimeSpan.FromMinutes(1), CancellationToken.None);
        taken.Done(passed: true);

        Assert.True(await first);
        Assert.False(during.IsCompleted);
        asks.Take().Done(passed: true);
        Assert.True(await during);

        // Past the bound, false, and the ask is no longer waited on.
        Assert.False(await asks.WaitAsync(null, TimeSpan.FromMilliseconds(20), CancellationToken.None));
        // A bound of none asks and waits for nothing.
        Assert.False(await asks.WaitAsync(null, TimeSpan.Zero, CancellationToken.None));
        Assert.Equal(4, woken);
    }

    /// <summary>
    /// The pass beside the looks keeps what a press asked of it: a run asked for while a pass runs is the next pass's to judge first, its press
    /// answered once that run is judged, before the pass ends; a press past its bound leaves its run asked for the pass that takes it.
    /// </summary>
    [Fact]
    public async Task A_run_asked_for_while_a_pass_runs_is_the_next_passes_and_its_press_is_answered_once_that_run_is_judged()
    {
        var asks = new PassAsks(() => { });
        var running = asks.Take();
        var press = asks.WaitAsync("run:q7", TimeSpan.FromMinutes(1), CancellationToken.None);
        running.Done(passed: true);
        Assert.False(press.IsCompleted);

        var next = asks.Take();
        Assert.Equal(["run:q7"], next.Units);
        next.Judged("run:q1");
        Assert.False(press.IsCompleted);
        next.Judged("run:q7");
        Assert.True(await press);
        next.Done(passed: false);
        Assert.True(await press);

        Assert.False(await asks.WaitAsync("run:q9", TimeSpan.FromMilliseconds(20), CancellationToken.None));
        Assert.Equal(["run:q9"], asks.Take().Units);
        Assert.Empty(asks.Take().Units);
    }
}
