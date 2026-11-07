namespace Daoris.Driver;

/// <summary>
/// The session watch: the watcher looks in on every running driven session, from
/// memory, reading a job only for one unheard for half its bound; each step it takes is said, and nothing quiet is ended but under
/// <c>--quiet-end</c>.
/// </summary>
[LookPass("watch", LookPhase.Local, "rules")]
internal sealed class WatchLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        using (wiring.Stages.Begin("watch")) round.Events.AddRange(wiring.LookIn(wiring.Lines));
        return Task.CompletedTask;
    }
}
