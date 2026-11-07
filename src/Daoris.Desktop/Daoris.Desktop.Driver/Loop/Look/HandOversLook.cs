namespace Daoris.Driver;

/// <summary>
/// The quests handed to another agent: the boxes taken, the quests that left dropped, and what stands
/// read for the planner, which reads the stop that handed one over as released.
/// </summary>
[LookPass("handovers", LookPhase.Read, "pieces")]
internal sealed class HandOversLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var read = round.State.Snapshot;
        round.State.Snapshot = await wiring.Stages.TimeAsync("handovers", () => wiring.HandOvers.HandOversAsync(read, round.Events, ct))
            .ConfigureAwait(false);
    }
}
