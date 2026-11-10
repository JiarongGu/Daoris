namespace Daoris.Driver;

/// <summary>
/// A start in a repository's own place, held where another Daoris home on this machine works that checkout in place
/// (<see cref="PlaceHomeMarker"/>): asked before its record opens, so the hold costs nothing. A repository that opens a tree for each
/// session is not asked.
/// </summary>
[RunStage("home", RunPhase.Choose, "account")]
internal sealed class PlaceHome(IRunWiring wiring) : IRunStage
{
    public ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct) =>
        ValueTask.FromResult(
            wiring.Context.Config.OpensOwnTree(run.Quest.To) || run.Start.Root is not { } root
                || PlaceHomeMarker.Claim(wiring.Context.Home, root, run.Quest.To) is not { } refused
                ? null
                : run.Hold(refused));
}
