namespace Daoris.Driver;

/// <summary>
/// The files stage, after the record opens and before the hand: in one of Daoris's own trees, its dependencies kept, placed from the
/// store or installed, its hook run and its machine-local files copied (<see cref="TreeFilesStage"/>), each said on the record. Under
/// the record, so the tree is in every in-use set while an install runs. The checkout and any tree Daoris did not make are left.
/// </summary>
[RunStage("files", RunPhase.Opened, "open")]
internal sealed class TreeFilesRun(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var context = wiring.Context;
        var state = run.State;
        var tree = state.WorkTree;
        state.Files = TreeFilesOutcome.None;
        if (run.Start.Root is not { } root || !context.Trees.Holds(tree)) return null;
        var sessionId = state.Opened.SessionId;
        state.Files = await TreeFilesStep.RunAsync(
            context.Home, root, tree, run.Quest.To,
            line => context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null }, say: null),
            ct).ConfigureAwait(false);
        return null;
    }
}
