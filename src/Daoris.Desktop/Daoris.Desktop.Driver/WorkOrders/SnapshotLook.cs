namespace Daoris.Driver;

/// <summary>
/// The quests read: the snapshot the plan reads, taken into the round's state, with what has to be taken before it so nothing chosen
/// or opening while it is read is lost.
/// </summary>
[LookPass("snapshot", LookPhase.Read)]
internal sealed class SnapshotLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        // The goal's walk counts Daoris's sessions from this look's records and every start chosen while they were read: marked
        // first, so a conversation chosen meanwhile is not lost.
        round.State.Mark = wiring.Harnesses.Mark();

        // The starts still opening, read before the snapshot as well as at the plan, so one whose record opened while the snapshot
        // is read is held by one of the two, whichever the ledger's answer missed.
        round.State.ReservedBefore = wiring.Runs.Reserved;
        var snapshot = await wiring.Stages.TimeAsync("snapshot", () => wiring.Service.SnapshotAsync(ct)).ConfigureAwait(false);
        round.State.Snapshot = snapshot with { Isolations = WorkOrdersWorkflows.Current.Isolations(wiring.Home, wiring.Config, snapshot.Repositories) };
    }
}
