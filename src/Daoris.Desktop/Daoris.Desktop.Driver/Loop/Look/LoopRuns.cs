using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// The looks a one-look run and a gate's run-until-idle make, and the wait for the sessions they started. A look no longer waits for
/// its sessions, so between looks this waits for the next ending or the watch's pace, whichever is first; the sync beside the sessions
/// and the lost-claim stop ride every look, as they ride the watch's.
/// </summary>
/// <param name="look">One look, handed what it had said when it fails or is closed.</param>
/// <param name="beside">The watch's pace, which the sync beside working sessions keeps.</param>
/// <param name="passes">The look's passes, of which the sync, the lost-claim stop and the endings are asked directly.</param>
internal sealed class LoopRuns(
    ILookStagesWiring wiring, Func<CancellationToken, Action<TickReport>?, Task<TickReport>> look, TimeSpan beside, Func<PartSet<ILookPass>> passes)
{
    /// <summary>
    /// Look until nothing runs and a look starts nothing: a queue that is empty, held, or waiting on the person, with no session of this
    /// driver's still working and no ending left unreported. A failure or a close lets go only once every session has written how it
    /// ended, and has said what ended.
    /// </summary>
    /// <param name="said">
    /// Handed each look's report as the look ends, a look that failed or was closed what it had said before, and before a failure or a
    /// close lets go, what ended while it waited: what the headless host prints, so a look's lines reach the person whatever ends the
    /// run after it.
    /// </param>
    public async Task<IReadOnlyList<TickReport>> UntilIdleAsync(CancellationToken ct, Action<TickReport>? said)
    {
        var reports = new List<TickReport>();
        try
        {
            while (true)
            {
                var report = await look(ct, said).ConfigureAwait(false);
                reports.Add(report);
                said?.Invoke(report);
                if (!report.Progressed && wiring.Runs.Idle) return reports;

                await wiring.Runs.NextAsync(beside, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }
        }
        catch
        {
            await LetGoAsync(said).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// One look, then every session it started to its end: the headless host's <c>--once</c>, and a test that reads one start whole.
    /// The endings join the look's own report.
    /// </summary>
    /// <remarks>
    /// 🔴 It never lets go before what its look started has ended: each session's ending written to the service and to the machine
    /// log, and said. A pass beside the sessions that fails is a line, and the next pass tries again; a run that left there let the
    /// process end with its sessions still working, and a lost-claim stop already made went unsaid.
    /// </remarks>
    /// <param name="said">
    /// Handed the look's report as the look ends, each pass beside its sessions that said anything as that pass ends, then what ended,
    /// which together are the report returned. A look that fails hands it what it had said before.
    /// </param>
    public async Task<TickReport> OnceAsync(CancellationToken ct, Action<TickReport>? said)
    {
        TickReport first;
        try
        {
            first = await look(ct, said).ConfigureAwait(false);
        }
        catch
        {
            // A look that failed after opening a session still waits for it, as a look always did: its record is written before
            // the failure is said.
            await LetGoAsync(said).ConfigureAwait(false);
            throw;
        }

        said?.Invoke(first);
        var settled = await SettleAsync(ct, said).ConfigureAwait(false);
        return first with
        {
            Events = [.. first.Events, .. settled.Events],
            Concluded = [.. first.Concluded, .. settled.Concluded],
        };
    }

    /// <summary>
    /// What a run that failed or was closed does before it lets go: it waits until every session it started has written how it
    /// ended, then hands <paramref name="said"/> what ended, so the endings are said rather than lost with the process. Nothing is
    /// asked of the service: it may be what failed.
    /// </summary>
    private async Task LetGoAsync(Action<TickReport>? said)
    {
        await wiring.Runs.SettledAsync().ConfigureAwait(false);
        if (said is null) return;

        var ended = wiring.Runs.Drain();
        if (ended.Count == 0) return;
        said(new TickReport(
            [], [.. ended.Select(run => wiring.Lines.Ran(run.Look, run.Ended is { ByPerson: true }, run.Line))], Progressed: false,
            Concluded: [.. ended.Select(run => run.Ended).OfType<SessionEnded>()]));
    }

    /// <summary>
    /// Wait until nothing this driver started runs, then report what ended. While they work, the sync runs beside them at the watch's
    /// pace, each pass followed by the lost-claim stop.
    /// </summary>
    /// <remarks>
    /// A pass that fails is said and the next tries again, as a look's sync is, since leaving here would leave the sessions with
    /// nothing watching. Only the close leaves, and what was gathered and what ended is handed to <paramref name="said"/> before it does.
    /// </remarks>
    /// <param name="said">Handed each pass that said anything as the pass ends, then what ended: together, the report returned.</param>
    private async Task<TickReport> SettleAsync(CancellationToken ct, Action<TickReport>? said)
    {
        var events = new List<string>();
        var concluded = new List<SessionEnded>();
        // What is gathered and not yet said. 🔴 Each pass is said as it ends: kept for the run's end, a lost-claim stop the family
        // rehearsal's run had made went with the process, ended from outside before its sessions had all ended.
        var pending = new List<string>();
        void Say(IReadOnlyList<SessionEnded> ended)
        {
            List<string> lines;
            lock (pending)
            {
                lines = [.. pending];
                pending.Clear();
            }

            if (lines.Count == 0 && ended.Count == 0) return;
            events.AddRange(lines);
            said?.Invoke(new TickReport([], lines, Progressed: false, Concluded: ended));
        }

        var settled = wiring.Runs.SettledAsync();
        try
        {
            // Woken at the pace whether or not a remote syncs, so the watcher judges the sessions it waits for, and a --quiet-end ends
            // one nobody watches; with no remote, the watcher's look alone.
            while (!settled.IsCompleted && !ct.IsCancellationRequested)
            {
                using var pace = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var first = await Task.WhenAny(settled, Task.Delay(beside, pace.Token)).ConfigureAwait(false);
                await pace.CancelAsync().ConfigureAwait(false);
                if (first == settled || ct.IsCancellationRequested) break;

                try
                {
                    lock (pending) pending.AddRange(wiring.LookIn(wiring.Lines));
                    if (wiring.Sync is not null)
                    {
                        await passes().Of<LookSync>().OnceAsync(pending, ct).ConfigureAwait(false);
                        await passes().Of<LostClaimStop>().StopAsync(pending, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // 🔴 Closing: the sessions are ending on this same token, and each writes how it ended. Leaving before them left
                    // them unrecorded when the process exited.
                    break;
                }
                catch (Exception error)
                {
                    lock (pending) pending.Add(wiring.Lines.Retry("sync", $"sync  a pass beside the running sessions failed, and the next tries again: {error.Message}"));
                }

                Say([]);
            }

            await settled.ConfigureAwait(false);

            try
            {
                await passes().Of<EndingsLook>().EndAsync(pending, concluded, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                // What ended is drained into the report before the pass after it runs, so a failed pass loses none of it.
                lock (pending) pending.Add(wiring.Lines.Retry("sync", $"sync  the pass after the sessions ended failed, and the next look tries again: {error.Message}"));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Say(concluded);
            throw;
        }

        Say(concluded);
        return new TickReport([], events, Progressed: false, Concluded: concluded);
    }
}
