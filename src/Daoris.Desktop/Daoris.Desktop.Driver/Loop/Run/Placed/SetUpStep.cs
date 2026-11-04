namespace Daoris.Driver;

/// <summary>
/// A set-up step follows its repository's own procedure, which its tree must hold: checked once the tree exists, and held before any
/// record, a fresh tree going as a refused open's does. A local set-up step's tab is opened before its session, so its instruction
/// names a tab the person sees: a tab an agent opens over CDP has no window, and a browser that will not open one holds the start.
/// </summary>
[RunStage("setup", RunPhase.Placed, "bind")]
internal sealed class SetUpStep(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var quest = run.Quest;
        var setUp = ReviewSetUps.Environment(wiring.Context.Config, quest, run.Start.Workspace);
        if (setUp is not null && ReviewSetUps.ProcedureMissing(setUp, quest, run.State.WorkTree) is { } unfollowable)
        {
            await run.Fresh.LetGoAsync().ConfigureAwait(false);
            return run.Hold(unfollowable);
        }

        if (setUp is not null && wiring.Reviews is { } desk && await desk.OpenForStepAsync(quest, setUp, ct).ConfigureAwait(false) is { } unshowable)
        {
            await run.Fresh.LetGoAsync().ConfigureAwait(false);
            return run.Hold(unshowable);
        }

        run.State.SetUp = setUp;
        return null;
    }
}
