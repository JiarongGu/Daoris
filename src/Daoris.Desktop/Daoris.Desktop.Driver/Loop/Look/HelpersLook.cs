namespace Daoris.Driver;

/// <summary>
/// The helpers asked for: read, judged and started beside the look, each taking a slot under the cap as
/// a pass does, and each answer handed to the session that asked. Under the watch an asker's record, an answer's handing
/// and a restart's settling are the <c>helper</c> topic's judge's; the verdict and its start stay here.
/// </summary>
[LookPass("helpers", LookPhase.Read, "opinions")]
internal sealed class HelpersLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var judged = round.State.Snapshot;
        var taken = round.State.Passes;
        round.State.Passes += await wiring.Stages.TimeAsync("helpers", () => wiring.Helpers.HelpersAsync(judged, taken, round.Events, ct))
            .ConfigureAwait(false);
    }
}
