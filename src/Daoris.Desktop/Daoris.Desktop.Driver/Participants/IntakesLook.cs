namespace Daoris.Driver;

/// <summary>
/// The asks this machine answers with a session (D65 §1b): only where a harness is named for it, and only in the slots the quests
/// left, since work somebody already asked for goes first. PARALLEL1b: a start still opening holds its slot, and an ask whose intake
/// is still opening is not begun again. A start a plugin will hold still counts here, since each start asks its plugins on its own:
/// the intakes may wait a look for a slot a hold left, never overrun the cap.
/// </summary>
[LookPass("intakes", LookPhase.Planned, "answered")]
internal sealed class IntakesLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var snapshot = round.State.Snapshot;
        var slotsLeft = wiring.Config.Cap - snapshot.SlotsHeld - Planner.Opening(snapshot).Count - round.State.Starts.Count - round.State.Passes;
        round.State.Intakes = (await wiring.Stages.TimeAsync("intakes", () => wiring.Intakes.IntakesDueAsync(slotsLeft, round.Events, ct)).ConfigureAwait(false))
            .Where(ask => !snapshot.Starting.Any(start => string.Equals(start.Ask, ask.Id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
