namespace Daoris.Driver;

/// <summary>
/// The meetings on file: each round's packet frozen for the person to read, and each round they sent run beside
/// the look, its turns one at a time through a brainstorm's pass, each taking a slot under the cap.
/// </summary>
[LookPass("meetings", LookPhase.Read, "brainstorms")]
internal sealed class MeetingsLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var judged = round.State.Snapshot;
        var taken = round.State.Passes;
        round.State.Passes += await wiring.Stages.TimeAsync("meetings", () => wiring.Meetings.MeetingsAsync(judged, taken, round.Events, ct))
            .ConfigureAwait(false);
    }
}
