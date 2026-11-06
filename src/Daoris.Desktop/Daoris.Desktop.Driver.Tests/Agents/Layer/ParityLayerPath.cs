using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The layer path over a registration (the agent-lyntai design §6.3): a <see cref="LayerRun"/> held by its executor as a driven start holds
/// it (<see cref="LayerHold"/> over a driver's own host), handed the same session as the Daoris path, so each process runs under a lineage
/// token of its own and every reading is stamped as production stamps it; each thing it did observed for the oracle.
/// </summary>
internal sealed class ParityLayerPath(string home) : IDisposable
{
    private readonly ServiceClient _service = new("http://127.0.0.1:9", null);

    public void Dispose() => _service.Dispose();

    /// <summary>One turn of <paramref name="registration"/>'s door with <paramref name="start"/>'s prompt, observed.</summary>
    public async Task<ParityObservation> RunAsync(LayerRegistration registration, LayerStart start, DateTimeOffset at, CancellationToken ct)
    {
        var (driver, target) = ParityDaorisPath.Over(_service, home);
        var held = new TransportStart(new ProcessStartInfo(registration.Door), StartClass.Background)
        {
            Target = target, Selection = new HarnessSelection(null), Prompt = start.Prompt,
        };
        var seen = new ParityObservation("layer", at);

        await using (var run = new LayerRun(
            registration, start, new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null), exists: _ => true,
            new LayerHold(new DriverTransportHost(driver), held)))
        {
            run.HandStart(run.Start with { Session = ParityDaorisPath.Session, Transcript = start.Transcript, Own = new HarnessWords() });
            run.HandSink(ParityObserver.Sink(seen));
            await run.OpenAsync(ct);
            var end = await run.EndAsync(ct);
            ParityObserver.Ended(seen, end, run.Stopped);
            if (run.Failure is { } failure) ParityObserver.Failed(seen, AgentErrors.OfNormalized(failure), failure.Delivery);
        }

        ParityObserver.Transcript(seen, start.Transcript);
        return seen;
    }
}
