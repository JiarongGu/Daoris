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

    /// <summary>Handed to every look's driver beside <see cref="Runner"/>: <see cref="Driver.Stops"/>, that stand-in's stop (DEV3c).</summary>
    internal Func<string, Noted, bool>? Stops { get; init; }

    /// <summary>
    /// Whether an update is draining this loop (UPDATE1, D139 §2), asked at every look: while it answers true, each look
    /// plans with <see cref="InstallUpdate.Drained"/>, so nothing new starts, and says the hold as the update's
    /// (<see cref="InstallUpdate.HeldFor"/>). The look itself goes on — the endings, the plugins' word on them, the sync —
    /// and what runs is never touched. Null, as in the headless host, is never.
    /// </summary>
    public Func<bool>? Draining { get; init; }

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
    /// <param name="said">
    /// Where a door can say a part of a look alone (the headless host's console): handed what a look that failed or was closed
    /// had said before (DEV3c), its lines and what ended, before the failure is heard or ends the loop. Null carries them into
    /// the next look's report instead, as the orphan sweep's lines are: a part handed to <paramref name="onReport"/> would
    /// replace a screen's standing answers with nothing, and a look that failed returns no report.
    /// </param>
    public async Task RunAsync(
        Func<TickReport, DriverConfig, Task> onReport,
        Func<Exception, Task>? onError,
        CancellationToken ct,
        Action<TickReport>? said = null)
    {
        // Once, before the first tick: what the last run left `working` with nothing behind it — a
        // crash, a kill, a power cut — ended before this tick counts it against the cap and its
        // repository's lock (2026-09-25). Retried each tick until the service has answered it once.
        var swept = false;

        // What is said and not yet in a report: the sweep's lines, and what a look that failed or was closed had said (DEV3c).
        // The next report opens with them, or `said` has them as the failure is heard.
        var carried = new List<string>();
        var carriedEnded = new List<SessionEnded>();
        void SayCarried()
        {
            if (said is null || (carried.Count == 0 && carriedEnded.Count == 0)) return;
            said(new TickReport([], [.. carried], Progressed: false, Concluded: [.. carriedEnded]));
            carried.Clear();
            carriedEnded.Clear();
        }

        // The last choices that read, for the wait: a torn file keeps the pace it had.
        var config = DriverConfig.Empty;

        // Once, as the loop starts (WSSETUP5, D124 §3.1): every repository with a checkout here registered from its line,
        // since a line the person moved outside Daoris is read nowhere else. Beside the first looks rather than before
        // them: a workspace's lines are read with git, seconds of it, and no start should wait on that. What it says
        // joins the next look's report; a pass the service did not answer is tried again at the next look.
        Task<IReadOnlyList<string>?>? following = null;
        var followed = false;

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
                        var orphans = await Orphans.EndAsync(service, processes, ct: ct).ConfigureAwait(false);
                        carried.AddRange(orphans.Select(ended => $"stopped  session {ended.Id} ({ended.Repository}): {Orphans.Note}"));
                        swept = true;
                        // A record the sweep ended concludes too (LAND2b, design §2): one whose quest its session closed done
                        // before the crash is due, and this first look lands it.
                        await OrphansDueAsync(orphans, config, ct).ConfigureAwait(false);
                    }

                    if (!followed) following ??= FollowEveryLineAsync(config, ct);

                    // Asked once per look, so the plan and what the report says of it agree (UPDATE1).
                    var draining = Draining?.Invoke() == true;
                    var report = await new Driver(
                            service, draining ? InstallUpdate.Drained(config) : config, AdapterSet.Built(), home, processes, sync, output,
                            _harnesses, usage, hooks, events, browser, Running)
                        { Runner = Runner, Stops = Stops }
                        .TickAsync(sessions.Token, failed: part =>
                        {
                            carried.AddRange(part.Events);
                            carriedEnded.AddRange(part.Concluded);
                        }).ConfigureAwait(false);
                    if (draining) report = report with { Considerations = InstallUpdate.HeldFor(report.Considerations, config) };
                    if (carried.Count > 0 || carriedEnded.Count > 0)
                    {
                        report = report with { Events = [.. carried, .. report.Events], Concluded = [.. carriedEnded, .. report.Concluded] };
                        carried.Clear();
                        carriedEnded.Clear();
                    }

                    if (!followed && following is { IsCompleted: true } done)
                    {
                        var lines = done.IsCompletedSuccessfully ? done.Result : null;
                        following = null;
                        if (lines is not null)
                        {
                            followed = true;
                            if (lines.Count > 0) report = report with { Events = [.. report.Events, .. lines] };
                        }
                    }

                    await onReport(report, config).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    SayCarried();
                    break;
                }
                catch (Exception error) when (onError is not null)
                {
                    failed = true;
                    SayCarried();
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
            // A failure with nobody to hear it ends the loop: what its look had said is said first (DEV3c).
            SayCarried();
            await sessions.CancelAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            await Running.SettledAsync().ConfigureAwait(false);
            // The start's pass ends on the loop's own token; waited for, so nothing it writes outlives the loop.
            if (following is not null) await following.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Every repository with a checkout here, followed from its line (WSSETUP5): what the pass says, for a look's report,
    /// or null where the service did not answer, so the next look tries again. Started on the pool, so the look beside it
    /// does not wait on its git.
    /// </summary>
    private Task<IReadOnlyList<string>?> FollowEveryLineAsync(DriverConfig config, CancellationToken ct) =>
        Task.Run<IReadOnlyList<string>?>(async () =>
        {
            try
            {
                var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, home, config), only: null, ct)
                    .ConfigureAwait(false);
                return
                [
                    .. report.Followed.Select(RegistrationFollow.EventLine).OfType<string>(),
                    .. report.Refresh is { } refresh ? [$"registry  the index was not read again after registering: {refresh}"] : Array.Empty<string>(),
                ];
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                return null;
            }
        }, CancellationToken.None);

    /// <summary>
    /// Each record the sweep ended that served a quest, read against its repository's rule (LAND2b): its done under the switch
    /// is due. Only a done is acted on: the sweep's stop is an interruption, carried on at the next start (D104), so an end that
    /// is not a done says nothing here.
    /// </summary>
    private async Task OrphansDueAsync(IReadOnlyList<OrphanEnded> orphans, DriverConfig config, CancellationToken ct)
    {
        foreach (var ended in orphans.Where(ended => ended.Quest is not null && ended.Tree is { Length: > 0 }))
        {
            QuestView? quest;
            try
            {
                quest = await service.FindQuestAsync(ended.Quest!, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                // Not due, then: its work waits for the person's Accept, as it would have before the switch.
                continue;
            }

            if (quest is not { Status: "Done" }) continue;
            AutoLander.Concluded(home, config, events ?? new SessionEvents(Path.Combine(home, "sessions")), ended.Id, quest, quest.Status,
                "stopped", ended.Tree!, quest.Workspace);
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
