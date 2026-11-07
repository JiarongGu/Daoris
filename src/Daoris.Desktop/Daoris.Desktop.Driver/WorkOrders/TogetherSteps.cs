using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The operations bringing sessions together, stepped before the look's starts take a head's word: looked at by
/// the process holding the home's lease, each stepped by the records, a landing settled, one whose sessions are deleted dropped.
/// Nothing is asked of the service while none is kept.
/// </summary>
[LookPass("together", LookPhase.Local, "lines")]
internal sealed class TogetherSteps(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) => wiring.Stages.TimeAsync("together", () => StepAsync(round.Events, ct));

    private async Task StepAsync(List<string> events, CancellationToken ct)
    {
        if (!HomeLease.HeldHere(wiring.Home)) return;
        try
        {
            if (new TogetherOperations(wiring.Home).All().Count == 0) return;
            var records = await wiring.Service.SessionRecordsAsync(ct).ConfigureAwait(false);
            var trees = new SessionTrees(wiring.Home);
            await new TogetherWatch(wiring.Home, () => DateTimeOffset.UtcNow, () => DriverConfig.Load(DriverConfig.ResolvePath(wiring.Home)),
                    (session, note) => wiring.Events.Keep(session, note, say: null))
                .LookAsync(records, (root, repository, token) => trees.LineOfAsync(root, repository, null, token), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            events.Add(wiring.Lines.Retry(
                "together", $"together  the sessions being brought together could not be looked at this look, and are tried again at the next: {error.Message}"));
        }
    }
}
