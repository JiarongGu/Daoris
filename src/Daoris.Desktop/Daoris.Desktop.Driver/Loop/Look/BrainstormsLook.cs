namespace Daoris.Driver;

/// <summary>
/// The brainstorms on file: each request frozen into its packet for the person to read, and each one the person
/// sent run beside the look, taking a slot under the cap as a pass does.
/// </summary>
[LookPass("brainstorms", LookPhase.Read, "partners")]
internal sealed class BrainstormsLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var judged = round.State.Snapshot;
        var taken = round.State.Passes;
        round.State.Passes += await wiring.Stages.TimeAsync("brainstorms", () => wiring.Brainstorms.BrainstormsAsync(judged, taken, round.Events, ct))
            .ConfigureAwait(false);
    }
}
