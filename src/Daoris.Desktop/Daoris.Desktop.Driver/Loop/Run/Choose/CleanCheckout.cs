namespace Daoris.Driver;

/// <summary>
/// A clean checkout, or nothing: uncommitted changes are somebody's work in flight. Asked before any record exists, so a hold costs
/// nothing and destroys nothing. A repository that opens a tree for each session is not asked: its fresh tree is clean by
/// construction, and the person's work in the checkout no longer holds the driver.
/// </summary>
[RunStage("clean", RunPhase.Choose, "agent")]
internal sealed class CleanCheckout(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        if (wiring.Context.Config.OpensOwnTree(run.Quest.To)) return null;
        var (clean, detail) = await WorkingTree.CleanAsync(run.Start.Root!, ct).ConfigureAwait(false);
        return clean ? null : run.Hold(detail);
    }
}
