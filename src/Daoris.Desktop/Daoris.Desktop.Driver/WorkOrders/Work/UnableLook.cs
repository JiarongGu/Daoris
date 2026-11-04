namespace Daoris.Driver;

/// <summary>
/// The words a record could not go on with, read again at each look (MSG1b): the planner leaves them waiting. The session a reopen
/// names is one the planner goes on with too (FABRIC-CLOSE1c9).
/// </summary>
[LookPass("unable", LookPhase.Read, "alone")]
internal sealed class UnableLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var snapshot = round.State.Snapshot;
        round.State.Snapshot = snapshot with { Unable = wiring.Marks.For(snapshot.LastRun.Values.Concat(snapshot.NamedByReopen.Values)) };
        return Task.CompletedTask;
    }
}
