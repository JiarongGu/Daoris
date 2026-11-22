namespace Daoris.Driver;

/// <summary>
/// The facts that end a run's step, teed into the bus's log where each happens (their types the registry's, <see cref="BusTypes"/>): a working
/// session's settle taken from its box, the session that performs a plugin step ending its turn, the risk policy passing a second opinion at its
/// gate (a landing's try ending is teed by <see cref="LandOutcomes"/>), and a start binding its run to a graph, which begins the run's first step. Each is of the commit class and its subject is the unit it moves, so the run's progression
/// (<see cref="RunStepConsumer"/>) hears them in order with the check facts. Ids, codes and flags only.
/// </summary>
public static class StepBusFacts
{
    /// <summary>A working session's step settle taken from its box: <paramref name="run"/> as the settle names it (<c>#</c> allowed).</summary>
    public static BusDraft StepSettled(string session, string run, string node) => new(BusTypes.StepSettled, RunUnits.IdOf(RunOf(run)))
    {
        Commit = true,
        Payload = [("session", session), ("run", RunOf(run)), ("node", node)],
    };

    /// <summary>The session of <paramref name="unit"/>'s step <paramref name="node"/> ended its turn: its answer is kept, or the step is judged unanswered.</summary>
    public static BusDraft StepTurnEnded(string unit, string node, string invocation) => new(BusTypes.StepTurnEnded, unit)
    {
        Commit = true,
        Payload = [("invocation", invocation), ("node", node)],
    };

    /// <summary>The policy passed <paramref name="unit"/>'s second opinion at its gate, its opinion step <paramref name="step"/>, at <paramref name="tip"/>.</summary>
    public static BusDraft PolicyPassed(string run, string step, string? tip) => new(BusTypes.PolicyPassed, RunUnits.IdOf(RunOf(run)))
    {
        Commit = true,
        Payload = [("step", step), ("tip", tip)],
    };

    /// <summary>A start bound <paramref name="run"/> to <paramref name="version"/> of the graph <paramref name="workflow"/>: its first step begins from it.</summary>
    public static BusDraft RunBound(string run, string workflow, int? version) => new(BusTypes.RunBound, RunUnits.IdOf(RunOf(run)))
    {
        Commit = true,
        Payload = [("run", RunOf(run)), ("workflow", workflow), ("version", version)],
    };

    /// <summary>The run's id without the <c>#</c> a person's or a session's word puts before it.</summary>
    public static string RunOf(string run) => run.Trim().TrimStart('#');

    /// <summary>
    /// Whether the log held for <paramref name="home"/> took the fact; where none is held here, the caller asks the pass itself, as it did before
    /// the queue, so a writer with no queue behind it never leaves a step waiting.
    /// </summary>
    public static bool Teed(string home, Func<BusDraft?> draft) => BusLog.Of(home) is { } bus && bus.Tee(draft);
}
