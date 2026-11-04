namespace Daoris.Driver;

/// <summary>
/// The ask's channel (SESSONE2b, D167 point 7; the co-working design §2.3): what is new for each line of work on an ask reaches its
/// newest part here where it can take it now, and otherwise waits for the entry's next part. One read per such line of work.
/// </summary>
[LookPass("channel", LookPhase.Read, "roster")]
internal sealed class ChannelLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var looked = round.State.Looked;
        round.Events.AddRange(await wiring.Stages.TimeAsync("channel", () => wiring.Context.Channel
            .LookAsync(new ServiceChannelDoors(wiring.Service), looked, wiring.Processes, ct, wiring.Lines))
            .ConfigureAwait(false));
    }
}
