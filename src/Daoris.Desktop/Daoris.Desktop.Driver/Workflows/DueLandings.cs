using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Done work lands itself: the sessions due to land, chosen at the end of the look and landed beside it, one landing pass at a
/// time, since a plugin's two minutes may outlast a look; what a pass says joins a later look's report. It runs after the endings
/// and their sync, so a done that just closed is read as the service now holds it. A due list or a service that does not answer is
/// said, and its sessions are tried again at the next look: landing is never a dead look.
/// </summary>
/// <remarks>
/// Only the process holding the home's lease selects or launches one, as only it keeps an end: a loop beside it (<c>drive --share</c>)
/// has its own guard and its own looks, so both could land one entry and each call its plugin. The person's press is unchanged.
/// </remarks>
[LookPass("landing", LookPhase.After)]
internal sealed class DueLandings(ILandingWiring wiring) : ILookPass
{
    private bool _deliveriesSettled;

    /// <summary>
    /// Whether this look's landing judged with <see cref="ILandingWiring.Woke"/>: set once its selection read the due list, or found it
    /// is not this process's to land. False where the look failed before it, the selection failed, or a landing still ran: the loop
    /// puts the wakes back.
    /// </summary>
    internal bool WokeSpent { get; private set; }

    /// <summary>Whether the selection was left because a landing still ran: that landing's end offers the wakes again.</summary>
    internal bool LandingBusy { get; private set; }

    public Task RunAsync(LookRound round, CancellationToken ct) => wiring.Stages.TimeAsync("landing", () => LandDueAsync(round.Events, ct));

    private async Task LandDueAsync(List<string> events, CancellationToken ct)
    {
        if (!HomeLease.HeldHere(wiring.Home))
        {
            // Not this process's to land: the lease holder's looks land it, and its wakes are this loop's to drop.
            WokeSpent = true;
            return;
        }

        // A landing changes a tree and the repository's line, and its plugin may take minutes: none is chosen or begun while an update
        // drains. The wakes are left unspent for the first look that does not.
        if (wiring.Config.Draining)
        {
            if (new AutoLandings(wiring.Home).Open().Count > 0) wiring.DrainHolds(events, "landing", "due", "the sessions due to land");
            return;
        }

        if (wiring.Runs.Landing)
        {
            LandingBusy = true;
            return;
        }

        try
        {
            // The rule's plugin says its lines on the console under its name, as at a press, and what the landing did with it is
            // logged where the loop's hooks are.
            var plugins = new LandingPlugins(
                wiring.Home, say: (plugin, line) => wiring.Output?.Append($"plugin:{plugin}", line), pluginLog: wiring.Hooks?.Log);
            await SettleDeliveriesAsync(plugins, events, ct).ConfigureAwait(false);
            var lander = new AutoLander(
                wiring.Home, new ServiceAutoLandingWorld(wiring.Service), new SessionTrees(wiring.Home, plugins), wiring.Events, wiring.Service.LandingSaid)
            {
                Look = wiring.Lines, PassAsked = wiring.PassAsked,
            };
            if (wiring.Beside)
            {
                SelectBeside(lander, ct);
                return;
            }

            var (chosen, said) = await lander.ChooseAsync(wiring.Config, ct, wiring.Woke).ConfigureAwait(false);
            WokeSpent = true;
            events.AddRange(said);
            if (chosen.Count > 0 && !wiring.Runs.Beside(token => lander.LandAsync(chosen, token), ct, wiring.LandingEnded))
            {
                // A pass began meanwhile (looks run one at a time, so none should): what was chosen is chosen again after it.
                WokeSpent = false;
                LandingBusy = true;
            }
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            events.Add(wiring.Lines.Retry(
                "landing", $"landing  the sessions due to land could not be looked at this look, and are tried again at the next: {error.Message}"));
        }
    }

    /// <summary>
    /// The selection and its landings, under the watch, as one landing pass beside the look: a due list, a workflow, a quest or a
    /// tree slow to read holds that pass, never a start. The wakes this look took are the pass's to judge with, so they are spent
    /// here, once; a selection that fails hands them back before it says so, and its line wakes the look that judges them again. A
    /// look while the pass runs leaves its own wakes for the pass's end.
    /// </summary>
    private void SelectBeside(AutoLander lander, CancellationToken ct)
    {
        if (lander.Due.Open().Count == 0)
        {
            WokeSpent = true;
            return;
        }

        var woke = wiring.Woke;
        var back = wiring.WokeBack;
        if (!wiring.Runs.Beside(async token =>
            {
                AutoChosen chosen;
                try
                {
                    chosen = await wiring.Stages.TimeAsync("landing", () => lander.ChooseAsync(wiring.Config, token, woke), beside: true)
                        .ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                                  or UnauthorizedAccessException
                                                  || (error is OperationCanceledException && !token.IsCancellationRequested))
                {
                    back?.Invoke();
                    // Said once until a selection reads again, so one that keeps failing wakes no look of its own.
                    return wiring.Runs.Facts.Failed("landing")
                        ? [wiring.Lines.Retry("landing", $"landing  the sessions due to land could not be looked at this time, and are tried again at a later look: {error.Message}")]
                        : [];
                }

                wiring.Runs.Facts.Judged("landing");
                if (chosen.Chosen.Count == 0) return chosen.Said;
                IReadOnlyList<string> landed = await lander.LandAsync(chosen.Chosen, token).ConfigureAwait(false);
                return [.. chosen.Said, .. landed];
            }, ct, wiring.LandingEnded))
        {
            // A pass began meanwhile (looks run one at a time, so none should): its end offers the wakes again.
            LandingBusy = true;
            return;
        }

        WokeSpent = true;
    }

    /// <summary>
    /// A restart settles by phase, before any new start: every delivery left unresolved, once per driver, by the lease's holder, its
    /// plugin phases through the landing's plugins. What is held for the person, or could not be read, is said; a delivery settled
    /// or going on says nothing, since its unit's next landing goes on from it.
    /// </summary>
    private async Task SettleDeliveriesAsync(IWorkplacePluginWire plugins, List<string> events, CancellationToken ct)
    {
        if (_deliveriesSettled) return;
        _deliveriesSettled = true;
        var store = new DeliveryStore(wiring.Home);
        foreach (var settled in await DeliveryReconciler.Of(store, wiring.Home, plugins, DeliveryReconciler.Checkouts(wiring.Home)).StartAsync(ct).ConfigureAwait(false))
        {
            if (settled.Standing is DeliveryStandings.Unknown or DeliveryStandings.Held) events.Add(wiring.Lines.Retry("landing", $"landing  {settled.Said}"));
        }
    }
}
