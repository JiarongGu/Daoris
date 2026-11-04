namespace Daoris.Driver;

/// <summary>
/// What ended since the last look, into the report (DEV3): each run's line and what it ended as, then everything that ended told to
/// the plugins that listen, then one more sync. Taken last, so an ending is never lost with a look that failed before its report.
/// The sync carries what those sessions did (a take, a close, a chain's next step) in the look that reports them, rather than a look
/// later (sync design §8). Nothing concluded, nothing new to carry.
/// </summary>
[LookPass("endings", LookPhase.Begun, "bind")]
internal sealed class EndingsLook(ILookStagesWiring wiring) : ILookPass
{
    /// <summary>
    /// How many endings the plugins hear at once beside the looks (PARALLEL1b; a topic bounded, the events design §A3): one slow answer
    /// holds its own ending, and a burst of endings reaches each plugin's one process two requests at a time, never all at once.
    /// </summary>
    internal const int HooksAtOnce = 2;

    public Task RunAsync(LookRound round, CancellationToken ct) =>
        wiring.Stages.TimeAsync("endings", () => EndAsync(round.Events, round.State.Concluded, ct));

    /// <summary>The endings reported into <paramref name="events"/>, and what concluded told to the plugins and synced.</summary>
    public async Task EndAsync(List<string> events, List<SessionEnded> concluded, CancellationToken ct)
    {
        var lines = wiring.Lines;
        foreach (var ended in wiring.Runs.Drain())
        {
            // As the pass that made it classed it (NOTIFY2h2); else an end the watch announces, or the person's own (NOTIFY2h, the
            // design §5.1: `:1246` is `progress`).
            events.Add(lines.Ran(ended.Look, ended.Ended is { ByPerson: true }, ended.Line));
            if (ended.Ended is not null) concluded.Add(ended.Ended);
        }

        // Contained: nothing a plugin says here changes the record, which moved on the exit code and the
        // quest before this line ran (D46 §4). PARALLEL1b: under the watch, as the `hooks` topic's judge beside the look, a session's
        // ending told once and HooksAtOnce endings at a time, so a plugin slow to hear holds no later look; what it says joins a later
        // report. A one-look run and a gate's run-until-idle tell them in the look, as before.
        if (wiring.Context.Hooks is { } hooks)
        {
            foreach (var ended in concluded)
            {
                if (wiring.Beside)
                {
                    wiring.Runs.Judge("hooks", ended.Session, HooksAtOnce,
                        token => wiring.Stages.TimeAsync("hooks", () => hooks.EndedAsync(ended, token, lines), ended.Session, beside: true), ct);
                    continue;
                }

                events.AddRange(await wiring.Stages.TimeAsync("hooks", () => hooks.EndedAsync(ended, ct, lines), ended.Session).ConfigureAwait(false));
            }
        }

        // Under the watch, beside the look: the report after it carries what the sync said.
        if (concluded.Count == 0) return;
        await wiring.SyncPass.SyncAsync(events, ct).ConfigureAwait(false);
    }
}
