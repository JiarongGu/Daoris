namespace Daoris.Driver;

/// <summary>
/// A source's reopen (FABRIC-CLOSE1c8, §E5): the session that said the reopened ready is told once, with the person's words, and the
/// planner's go-on resumes it at the next look; a running one this loop runs at its next step, through its inbox (COWORK1b). Read
/// from the quests this look read. The words it took off a record because what they said no longer holds are off the snapshot the
/// plan reads too (LIMITSWITCH3).
/// </summary>
[LookPass("reopens", LookPhase.Read, "channel")]
internal sealed class ReopensLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var looked = round.State.Looked;
        var takenOff = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var context = wiring.Context;
        round.Events.AddRange(await wiring.Stages.TimeAsync("reopens", () => context.Reopens
                .LookAsync(
                    new ServiceReopenDoors(wiring.Service, context.Processes, context.Events, context.Trees), looked, ct, wiring.Lines,
                    taken: (session, ids) => takenOff[session] = takenOff.TryGetValue(session, out var before) ? [.. before, .. ids] : ids))
            .ConfigureAwait(false));
        round.State.Snapshot = ReopenTelling.Without(round.State.Snapshot, takenOff);
    }
}
