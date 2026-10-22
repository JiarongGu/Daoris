using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1d (the cutover design §4): a door that holds on a run's graph for a judgement not made yet asks the loop for a whole pass, and
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

    private Func<TimeSpan, CancellationToken, Task<bool>> Passes(bool comes) => (bound, _) =>
    {
        _did.Add($"pass asked, waiting {bound.TotalSeconds:0}s");
        return Task.FromResult(comes && bound > TimeSpan.Zero);
    };

    [Fact]
    public async Task A_press_held_for_a_judgement_not_made_lands_at_the_same_press_where_the_pass_comes_within_the_bound()
    {
        var gate = await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(Lands), Passes(comes: true), CancellationToken.None);

        Assert.Same(Lands, gate);
        Assert.True(gate.LetsGo);
        Assert.Equal(["answer kept", $"pass asked, waiting {GraphPasses.Bound.TotalSeconds:0}s", "gate read"], _did);
    }

    [Fact]
    public async Task Past_the_bound_the_press_is_refused_as_read_its_answer_kept()
    {
        var gate = await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(Lands), Passes(comes: false), CancellationToken.None);

        Assert.Same(NotJudged, gate);
        Assert.Equal(AutoLandingCode.Refused, gate.Refusal!.Refusal);
        Assert.Equal(["answer kept", $"pass asked, waiting {GraphPasses.Bound.TotalSeconds:0}s"], _did);
    }

    [Fact]
    public async Task A_wait_the_next_pass_may_move_asks_one_and_the_press_does_not_wait_and_a_hold_before_the_graphs_asks_none()
    {
        Assert.Same(Waits, await GraphPasses.PressAsync(_ => Answered(Waits), Reads(Lands), Passes(comes: true), CancellationToken.None));
        Assert.Equal(["answer kept", "pass asked, waiting 0s"], _did);

        _did.Clear();
        var opinion = Gate(NotJudged.Graph, opinionHolds: true);
        Assert.Same(opinion, await GraphPasses.PressAsync(_ => Answered(opinion), Reads(Lands), Passes(comes: true), CancellationToken.None));
        Assert.Equal(["answer kept"], _did);

        // No loop to ask (a terminal's press alone): refused as read.
        _did.Clear();
        Assert.Same(NotJudged, await GraphPasses.PressAsync(_ => Answered(NotJudged), Reads(Lands), pass: null, CancellationToken.None));
        Assert.Equal(["answer kept"], _did);
    }

    [Fact]
    public async Task A_pass_takes_the_asks_made_before_it_began_and_one_made_while_it_runs_waits_for_the_next()
    {
        var woken = 0;
        var asks = new PassAsks(() => woken++);

        var first = asks.WaitAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Equal(1, woken);
        Assert.True(asks.Asked);
        var taken = asks.Take();
        Assert.False(asks.Asked);
        var during = asks.WaitAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        PassAsks.Done(taken, passed: true);

        Assert.True(await first);
        Assert.False(during.IsCompleted);
        PassAsks.Done(asks.Take(), passed: true);
        Assert.True(await during);

        // Past the bound, false, and the ask is no longer waited on.
        Assert.False(await asks.WaitAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));
        // A bound of none asks and waits for nothing.
        Assert.False(await asks.WaitAsync(TimeSpan.Zero, CancellationToken.None));
        Assert.Equal(4, woken);
    }
}
