namespace Daoris.Driver;

/// <summary>
/// The set-ups waiting for the person here, and each set-up step's tab let go once they answer it: kept
/// served from look to look until then. A miss is said and tried again at the next look.
/// </summary>
[LookPass("reviews", LookPhase.Read, "reopens")]
internal sealed class ReviewsLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        if (wiring.Reviews is not { } showing) return;
        try
        {
            round.Events.AddRange(await wiring.Stages
                .TimeAsync("reviews", () => showing.LookAsync(round.State.Looked.Quests, wiring.Service.FindQuestAsync, ct, wiring.Lines))
                .ConfigureAwait(false));
        }
        catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or DriverException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            round.Events.Add(wiring.Lines.Retry("review", $"review  the set-ups' tabs were not looked at this look: {error.Message}"));
        }
    }
}
