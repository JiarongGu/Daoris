namespace Daoris.Driver;

/// <summary>
/// The watch loop, owned once by the library — the headless host and the desktop shell both ran the
/// same algorithm (re-read the config, tick, wait) as two copies, four six-argument constructions
/// between them. Each host keeps only its reporting half: what a tick report BECOMES (console lines,
/// event-bus notifications) differs by door; when the loop looks does not.
/// </summary>
/// <remarks>
/// <para>The person's standing choices are re-read every tick, so a hold, an opt-in, or a new adapter
/// command takes effect without a restart — the shell's controls are edits to the config file, and a
/// control that needs a bounce is a control nobody trusts.</para>
///
/// <para><b>The watch keeps the running sessions</b> (DEV3, D115 §3.1). A look starts what may start and
/// returns, so the loop looks again at its pace, at a nudge, or the moment a session ends, while others
/// still work. A quest published while a long session runs starts at the next look, not after it.</para>
/// </remarks>
public sealed class DriverWatch(
    ServiceClient service, string configPath, string home, SessionProcesses processes, RemoteSyncSet? sync,
    // The live console, where something is watching (D49 §2). Null in the headless host: a buffer
    // nobody reads is memory spent on an audience that does not exist.
    SessionOutput? output = null,
    // The harness cache (D49 §4), shared across ticks for the same reason the process registry is:
    // a probe spawns a process, and re-detecting every harness every tick would be absurd.
    HarnessRoster? harnesses = null,
    // What sessions consumed (TOOL3). Null in the headless host for the same reason the console
    // buffer is: a record nobody reads is a file written for an audience that does not exist.
    SessionUsage? usage = null,
    // The plugins that speak (D64), owned by whoever owns this loop and stopped with it; every tick
    // reconciles them against the catalogue, so an edit between ticks takes effect at the next.
    HookSet? hooks = null,
    // Where a session's structure is kept (D76 §2), shared so the shell hears each event live. Null
    // in the headless host: each tick's driver keeps the record under the home with nobody watching.
    SessionEvents? events = null,
    // Daoris's own browser (D78), where a shell carries one. Null in the headless host, where a server
    // that drives it is not handed.
    IInAppBrowser? browser = null)
{
    private CancellationTokenSource _pause = new();

    // Every nudge counted, so one that lands while a look runs — before the wait it would have ended —
    // still ends that wait (DEV3): looks are short now, and a person's control nudges the loop often.
    private int _nudges;

    /// <summary>This machine's harnesses, as the loop sees them — also what a roster surface reads.</summary>
    private readonly HarnessRoster _harnesses = harnesses ?? new HarnessRoster(AdapterSet.Built());

    /// <inheritdoc cref="_harnesses"/>
    public HarnessRoster Harnesses => _harnesses;

    /// <summary>
    /// The sessions this loop started and still runs (DEV3): kept here, since each look's driver is built
    /// fresh and a session outlives the look that started it.
    /// </summary>
    public RunningSessions Running { get; } = new();

    /// <summary>Handed to every look's driver: <see cref="Driver.Runner"/>, a test's in-process stand-in for a start's run (DEV3).</summary>
    internal Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner { get; init; }

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown.</summary>
    public void Nudge()
    {
        Interlocked.Increment(ref _nudges);

        // Swap first, atomically, so two racing nudges each cancel a source that is no longer current
        // — the swapped-out source is cancelled, disposed, and never waited on again.
        var paused = Interlocked.Exchange(ref _pause, new CancellationTokenSource());
        paused.Cancel();
        paused.Dispose();
    }

    /// <summary>
    /// Tick until cancelled. <paramref name="onReport"/> receives every tick's report with the config
    /// that produced it; <paramref name="onError"/> decides what a failed tick means — the shell says
    /// so and keeps watching (an unattended loop outlives its service's restarts), while a null lets
    /// the failure propagate, which is the headless host's exit-2 contract.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>It returns only once every session it started has written how it ended</b> (REV3, D104): a
    /// close cancels them on the loop's own token, and each records itself stopped by the driver's
    /// shutdown, to be carried on at the next start. A failure that ends the loop ends them the same way
    /// first, rather than leave them working with nothing watching.
    /// </remarks>
    public async Task RunAsync(
        Func<TickReport, DriverConfig, Task> onReport,
        Func<Exception, Task>? onError,
        CancellationToken ct)
    {
        // Once, before the first tick: what the last run left `working` with nothing behind it — a
        // crash, a kill, a power cut — ended before this tick counts it against the cap and its
        // repository's lock (2026-09-25). Retried each tick until the service has answered it once.
        var swept = false;
        IReadOnlyList<string> sweep = [];
        // The last choices that read, for the wait: a torn file keeps the pace it had.
        var config = DriverConfig.Empty;

        // What the sessions run on: the loop's own token, and cancelled by the loop itself when a failure
        // ends it, so no session outlives the loop that watches it (DEV3).
        using var sessions = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var nudged = Volatile.Read(ref _nudges);
                var failed = false;
                try
                {
                    // 🔴 Inside the catch (REV3): a hand edit that tore the file threw from here, outside it,
                    // and the watch died on its first tick — silently, in the shell. Now it is said, nothing
                    // ticks on choices it cannot read, and the next look after the fix ticks again.
                    config = Load(configPath);

                    if (!swept)
                    {
                        sweep = [.. (await Orphans.EndAsync(service, processes, ct: ct).ConfigureAwait(false))
                            .Select(ended => $"stopped  session {ended.Id} ({ended.Repository}): {Orphans.Note}")];
                        swept = true;
                    }

                    var report = await new Driver(
                            service, config, AdapterSet.Built(), home, processes, sync, output, _harnesses, usage, hooks, events, browser,
                            Running)
                        { Runner = Runner }
                        .TickAsync(sessions.Token).ConfigureAwait(false);
                    if (sweep.Count > 0)
                    {
                        report = report with { Events = [.. sweep, .. report.Events] };
                        sweep = [];
                    }

                    await onReport(report, config).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception error) when (onError is not null)
                {
                    failed = true;
                    await onError(error).ConfigureAwait(false);
                }

                // Interruptible four ways: cancellation ends the loop; the pace, a nudge and a session ending
                // each end the wait — an ending at once, so a freed slot is used and the ending is said. Not
                // after a failed look: the endings it never reported are still waiting, and waking on them
                // would look again at once, every time, against a service that is not answering.
                CancellationTokenSource wait;
                try
                {
                    wait = CancellationTokenSource.CreateLinkedTokenSource(ct, _pause.Token);
                }
                catch (ObjectDisposedException)
                {
                    continue; // A nudge swapped the source as the wait began: that is a nudge, so look again.
                }

                using (wait)
                {
                    // A nudge during the look came before this wait could hear it: look again now.
                    if (Volatile.Read(ref _nudges) != nudged) continue;
                    await Running.NextAsync(TimeSpan.FromSeconds(config.PollSeconds), wait.Token, endings: !failed)
                        .ConfigureAwait(false);
                }
            }
        }
        catch
        {
            await sessions.CancelAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            await Running.SettledAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The standing choices, or the driver's own sentence about why they could not be read.</summary>
    private static DriverConfig Load(string path)
    {
        try
        {
            return DriverConfig.Load(path);
        }
        catch (Exception error) when (
            error is System.Text.Json.JsonException or FormatException or InvalidOperationException or IOException
                or UnauthorizedAccessException)
        {
            throw new DriverException(
                $"{Path.GetFileName(path)} could not be read ({error.Message}) — nothing is driven until it reads; "
                + "the loop keeps watching it, and takes the fix at its next look.");
        }
    }
}
