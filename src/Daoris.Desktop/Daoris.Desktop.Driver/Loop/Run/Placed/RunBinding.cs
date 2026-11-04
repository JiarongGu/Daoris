namespace Daoris.Driver;

/// <summary>
/// The run bound to its workflow once its tree is open and before its take names a closure graph, so the take names the graph its run
/// is bound to and no look binds it later to something else. A start held before this binds nothing; one held or refused after it
/// keeps its run bound to what it chose, so a choice changed since applies to the next run. A bind not made, or a binding that does
/// not read, holds the start, said, and the next look tries again, its fresh tree let go as a refused open's is.
/// </summary>
[RunStage("bind", RunPhase.Placed, "steward")]
internal sealed class RunBinding(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var runBound = await wiring.BindAtStartAsync(run.Quest, run.Start.Workspace, ct).ConfigureAwait(false);
        if (runBound.Holds)
        {
            await run.Fresh.LetGoAsync().ConfigureAwait(false);
            return run.Hold(runBound.Why!);
        }

        // The walk took the role its run's bind would name; a bind that named another since holds the start, its count given back, and
        // the next look walks the role the run is bound to.
        if (runBound.Binding?.RoleOf(NodeKinds.Claimed)?.Role is var boundRole && !string.Equals(boundRole, run.State.StepRole, StringComparison.Ordinal))
        {
            await run.Fresh.LetGoAsync().ConfigureAwait(false);
            wiring.Context.Harnesses.Release(run.State.Selection.Number);
            var taker = boundRole is null ? "its work's own role" : $"the role `{boundRole}`";
            return run.Hold($"its run was bound as it started, its work step taken by {taker}, which it did not walk, so it starts on that role at the next look.");
        }

        // A run bound already to a named version may be a graph run that ended and whose chain has come back here: the runtime is
        // asked, and re-arms for it until a pass judges it where its record holds its end.
        if (runBound is { Came: WorkflowBindCame.Already, Run: { } returned, Binding.IsCurrent: false }) wiring.RunReturned?.Invoke(returned);
        run.State.Bound = runBound;
        return null;
    }
}
