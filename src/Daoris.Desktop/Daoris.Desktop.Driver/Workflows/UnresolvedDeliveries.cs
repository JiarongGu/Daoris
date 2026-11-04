using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The look's landing: a run's <c>land</c> is the runtime's, so the look only settles the deliveries a stopped run left unresolved,
/// by the process holding the home's lease, as only it lands, and never while an update drains. A delivery that could not be read
/// is said, and settled at the next look.
/// </summary>
[LookPass("landing", LookPhase.After)]
internal sealed class UnresolvedDeliveries(ILandingWiring wiring) : ILookPass
{
    private bool _settled;

    public Task RunAsync(LookRound round, CancellationToken ct) => wiring.Stages.TimeAsync("landing", () => SettleAsync(round.Events, ct));

    private async Task SettleAsync(List<string> events, CancellationToken ct)
    {
        if (!HomeLease.HeldHere(wiring.Home) || wiring.Config.Draining) return;
        try
        {
            // A plugin says its lines on the console under its name, as at a press, and what it did is logged where the loop's hooks are.
            var plugins = new LandingPlugins(
                wiring.Home, say: (plugin, line) => wiring.Output?.Append($"plugin:{plugin}", line), pluginLog: wiring.Hooks?.Log);
            await SettleDeliveriesAsync(plugins, events, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            events.Add(wiring.Lines.Retry(
                "landing", $"landing  the deliveries left unresolved could not be settled this look, and are settled at the next: {error.Message}"));
        }
    }

    /// <summary>
    /// A restart settles by phase, before any new start: every delivery left unresolved, once per driver, its plugin phases through
    /// the landing's plugins. What is held for the person, or could not be read, is said; a delivery settled or going on says nothing,
    /// since its unit's next landing goes on from it.
    /// </summary>
    private async Task SettleDeliveriesAsync(IWorkplacePluginWire plugins, List<string> events, CancellationToken ct)
    {
        if (_settled) return;
        _settled = true;
        var store = new DeliveryStore(wiring.Home);
        foreach (var settled in await DeliveryReconciler.Of(store, wiring.Home, plugins, DeliveryReconciler.Checkouts(wiring.Home)).StartAsync(ct).ConfigureAwait(false))
        {
            if (settled.Standing is DeliveryStandings.Unknown or DeliveryStandings.Held) events.Add(wiring.Lines.Retry("landing", $"landing  {settled.Said}"));
        }
    }
}
