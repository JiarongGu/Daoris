using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Which part of the look a look runs (FABRIC-EVENT3a, the fabric events design §A3, §C5, §C6 link 5). The whole look's only
/// further parts are the remote sync at its head and the lost-claim stop after it, so a wake defers those to the <c>look</c>
/// topic's deadline and skips no judgement the whole look would have made.
/// </summary>
public enum LookPart
{
    /// <summary>
    /// The <c>look</c> topic's: the remote sync and the lost-claim stop, then the local plan. At the topic's deadline
    /// (<c>pollSeconds</c> after the last whole look), at the loop's start, after a failed look's pace, and on a plain nudge.
    /// </summary>
    Look,

    /// <summary>
    /// A wake's (a <c>ledger</c> family moved, a terminal's words kept, a session's end): the local plan alone, every part of the
    /// look but the remote sync and the lost-claim stop, the sync after an ending it reports included.
    /// </summary>
    Plan,
}

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

    /// <summary>Handed to every look's driver (<see cref="Driver.Log"/>): where a failure nothing else awaits is written (ANSWER2).</summary>
    public MachineLog? Log { get; init; }

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.Reviews"/>): the review's showing in Daoris's browser (REVIEWENV1d), one
    /// for the shell's life, since a served tab outlives the look and the session that started it. Null in the headless host.
    /// </summary>
    public ReviewDesk? Reviews { get; init; }

    /// <summary>
    /// Whether an update is draining this loop (UPDATE1, D139 §2), asked at every look: while it answers true, each look
    /// plans with <see cref="InstallUpdate.Drained"/>, so nothing new starts, and says the hold as the update's
    /// (<see cref="InstallUpdate.HeldFor"/>). The look itself goes on — the endings, the plugins' word on them, the sync —
    /// and what runs is never touched. Null, as in the headless host, is never.
    /// </summary>
    public Func<bool>? Draining { get; init; }

    /// <summary>
    /// The look's own tidy of session branches (AUTOTIDY1, AUTOTIDY2a, D88's notes), run beside the looks once each pace, as the start's
    /// line-following runs beside them; what it says joins the report of the look that finds it done. Null keeps the watch's
    /// own, at <see cref="BranchTidying.DefaultPace"/>.
    /// </summary>
    public BranchTidying? Tidying { get; init; }

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown. A plain nudge carries no topic, and means everything: the whole look, the remote
    /// sync first (FABRIC-EVENT3a).</summary>
    public void Nudge() => Woken(null);

    /// <summary>
    /// Look now, because <paramref name="topic"/>'s <paramref name="subject"/> moved (FABRIC-EVENT2, the design §A3): a wake from a
    /// change source, never a value. It runs the look's local plan alone (FABRIC-EVENT3a, §C6 link 5), and leaves the remote sync to
    /// the <c>look</c> topic's deadline, unless that deadline came with it. The topic and subject are what its <c>watch.looked</c>
    /// line says woke it. A subject is a name, never a path.
    /// </summary>
    public void Nudge(string topic, string subject) => Woken(new Wake(topic, subject));

    /// <summary>What watches the home's record files (<see cref="HomeWatch"/>): the machine's watchers unless a test hands its fake.</summary>
    internal IChangeSource? Source { get; init; }

    private HomeWatch? _files;

    private RefWatch? _refs;

    /// <summary>
    /// The machine resumed from sleep (FABRIC-EVENT4, FABRIC-END3): the record files' and the repositories' watchers are reopened and each
    /// judged now.
    /// </summary>
    public void Resumed()
    {
        _files?.Resumed();
        _refs?.Resumed();
    }

    /// <summary>The ref watch this loop runs (FABRIC-END3), which a test steps and counts; null until the loop starts.</summary>
    internal RefWatch? Refs => _refs;

    /// <summary>
    /// Look now, because a terminal's words for <paramref name="session"/> were kept (FABRIC-EVENT2b): the requests topic's wake,
    /// so <c>watch.looked</c> says the words woke it, not that something nudged.
    /// </summary>
    public void Said(string session) => Nudge(ChangeTopics.Requests, session);

    // What the last nudge said woke the loop: null for a plain nudge, which means everything.
    private Wake? _woke;

    private sealed record Wake(string Topic, string Subject);

    // Whether a plain nudge came since the loop last took its nudges up (FABRIC-EVENT3a): set before the nudge is counted, so the look
    // that counts it is whole, whatever topic's wake came after it and replaced `_woke`.
    private int _everything;

    // The wakes since the last look, which a plan's landing looks at alone (FABRIC-END7); the whole look takes them and lands on all.
    private readonly LandingWakes _landing = new();

    private void Woken(Wake? wake)
    {
        if (wake is null) Interlocked.Exchange(ref _everything, 1);
        else _landing.Hear(wake.Topic, wake.Subject);
        Volatile.Write(ref _woke, wake);
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

        // The session branches tidied beside the looks (AUTOTIDY1, AUTOTIDY2a): a pass once each pace, its lines joining the report
        // of the look that finds it done, as the following's do.
        var tidy = Tidying ?? new BranchTidying(home);
        Task<IReadOnlyList<string>>? tidying = null;

        // What woke each look, for its line in the machine log (FABRIC-EVENT2): the loop's start, its pace, a session's end, or a
        // nudge with what it said moved; and how many nudges the look took up at once.
        var by = "start";
        Wake? woke = null;
        var counted = Volatile.Read(ref _nudges);

        // The `look` topic's deadline (FABRIC-EVENT3a, the design §C5): `pollSeconds` after the last whole look ended, whatever woke
        // the looks between, so a stream of wakes never holds the reconciliation, or the remote sync in it, back.
        var whole = Stopwatch.GetTimestamp();

        // The look is link 5 of the design §C6 (FABRIC-EVENT3a), on this loop's own chain: a wake is a `ledger` event, and runs the
        // local plan alone; the `look` topic's deadline is its `timer(look)`, and runs the whole look, the remote sync first. The loop
        // stays the topic's queue, as it was the look's: what wakes it during a look is one look after it, and its wait hears a
        // session's end, which a change watch's pump does not, so the watch under the chain is never started.
        await using var queue = new ChangeWatch();
        var chain = new Chain(queue) { Log = Log };

        // The home's record files heard, not found at the pace (FABRIC-EVENT4, §A4.1, link 7): the configuration, the plugins folder,
        // the moved lines and the held words each wake the local plan by their topic, and the pace stays their deadline. Both
        // loops run this one watch, so the headless host and the shell hear the same files.
        await using var files = HomeWatch.ForDriver(home, configPath, Nudge, source: Source, log: Log);
        _files = files;
        files.Start();

        // The refs heard on each repository's common git dir (FABRIC-END3, §B4, link 1): a ref move judges that repository's ends on its
        // own, never the look and never the remote sync, and the tidy's pass below stays the reconciliation at its pace.
        await using var refs = RefWatch.ForDriver(home, service.RegistryAsync, source: Source, log: Log,
            heard: repository => LandingWakes.Moved(home, repository, Nudge)); // FABRIC-END7: a due tree that moved wakes its landing.
        _refs = refs;
        refs.Start();

        // The ledger heard, not found at the pace (FABRIC-EVENT3b, §A4.2): one wait on the service's change stream for the loop's life,
        // each family that moved waking the local plan by the `ledger` topic, as a quest an agent published, and the pace its deadline.
        using var listening = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ledger = LedgerChanges.RunAsync(service, Nudge, Log, listening.Token);
        TickReport? looked = null;
        chain.Bind(ChainRanks.Plan, async (heard, token) =>
        {
            looked = await LookAsync(heard.Any(cause => cause.Key == Whole) ? LookPart.Look : LookPart.Plan, token).ConfigureAwait(false);
            return new ChainFired(looked.Progressed ? 1 : 0, []);
        });

        // One look of the part link 5 heard, asked once whether an update drains, so the plan and what the report says of it
        // agree (UPDATE1).
        async Task<TickReport> LookAsync(LookPart part, CancellationToken token)
        {
            var draining = Draining?.Invoke() == true;
            var report = await new Driver(
                    service, draining ? InstallUpdate.Drained(config) : config, AdapterSet.Built(), home, processes, sync, output,
                    _harnesses, usage, hooks, events, browser, Running)
                { Runner = Runner, Stops = Stops, Log = Log, Reviews = Reviews, Part = part, Woke = _landing.For(whole: part == LookPart.Look) }
                .TickAsync(token, failed: said =>
                {
                    carried.AddRange(said.Events);
                    carriedEnded.AddRange(said.Concluded);
                }).ConfigureAwait(false);
            return draining ? report with { Considerations = InstallUpdate.HeldFor(report.Considerations, config) } : report;
        }

        // What the sessions run on: the loop's own token, and cancelled by the loop itself when a failure
        // ends it, so no session outlives the loop that watches it (DEV3).
        using var sessions = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var nudged = Volatile.Read(ref _nudges);
                var nudges = nudged - counted;
                counted = nudged;
                // Taken after the count, so a plain nudge the count missed still makes this look whole, and is looked at again.
                var everything = Interlocked.Exchange(ref _everything, 0) == 1;
                var part = by is "start" or "pace" || everything || Due(whole, config) ? LookPart.Look : LookPart.Plan;
                var failed = false;
                var began = Stopwatch.GetTimestamp();
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

                    looked = null;
                    await chain.FireAsync([Cause(part, woke)], sessions.Token).ConfigureAwait(false);
                    var report = looked ?? throw new InvalidOperationException("link 5 of the loop's chain never ran the look.");
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

                    if (tidying is { IsCompleted: true } tidied)
                    {
                        tidying = null;
                        // A pass that failed past its own catches is said, and so observed, rather than left to the finalizer.
                        IReadOnlyList<string> lines = tidied.IsCompletedSuccessfully ? tidied.Result
                            : tidied.Exception?.InnerException is { } failure
                                ? [$"tidy  the session branches were not looked at this time, and are looked at again later: {failure.Message}"]
                                : [];
                        if (lines.Count > 0) report = report with { Events = [.. report.Events, .. lines] };
                    }

                    if (tidying is null && tidy.Due) tidying = TidyAsync(tidy, ct);

                    Looked(by, woke, part, nudges, began, failed: false);
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
                    Looked(by, woke, part, nudges, began, failed: true);
                    SayCarried();
                    await onError(error).ConfigureAwait(false);
                }

                // A whole look that failed was still the deadline's: the next comes a pace later, as it always did, not at once.
                if (part == LookPart.Look) whole = Stopwatch.GetTimestamp();

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
                    // A nudge swapped the source as the wait began: that is a nudge, so look again.
                    (by, woke) = ("nudge", Volatile.Read(ref _woke));
                    continue;
                }

                using (wait)
                {
                    // A nudge during the look came before this wait could hear it: look again now.
                    if (Volatile.Read(ref _nudges) != nudged)
                    {
                        (by, woke) = ("nudge", Volatile.Read(ref _woke));
                        continue;
                    }

                    // Up to the `look` topic's deadline (FABRIC-EVENT3a), or the whole pace after a failed look, as it always was.
                    var pace = TimeSpan.FromSeconds(config.PollSeconds);
                    var left = failed ? pace : pace - Stopwatch.GetElapsedTime(whole);
                    await Running.NextAsync(left > TimeSpan.Zero ? left : TimeSpan.Zero, wait.Token, endings: !failed).ConfigureAwait(false);
                    woke = Volatile.Read(ref _nudges) != nudged ? Volatile.Read(ref _woke) : null;
                    by = Volatile.Read(ref _nudges) != nudged ? "nudge"
                        : failed || Due(whole, config) ? "pace"
                        : "ended";
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
            // The change stream's wait ends with the loop, and is waited for, so no ask outlives it.
            await listening.CancelAsync().ConfigureAwait(false);
            await ledger.ConfigureAwait(false);
            await Running.SettledAsync().ConfigureAwait(false);
            // The start's pass ends on the loop's own token; waited for, so nothing it writes outlives the loop.
            if (following is not null) await following.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            // The tidy's too: a removal half done when the loop closed is git's to finish, never left running unwatched.
            if (tidying is not null) await tidying.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    /// <summary>How much sooner than its pace a wait may end and still be the pace's: a timer's own granularity.</summary>
    private static readonly TimeSpan Early = TimeSpan.FromMilliseconds(50);

    /// <summary>What link 5 hears as the <c>look</c> topic's deadline: <c>timer(look)</c>, the whole look.</summary>
    private static readonly string Whole = ChainEvents.TimerOf(ChangeTopics.Look);

    /// <summary>Whether the <c>look</c> topic's deadline has come: <c>pollSeconds</c> since the last whole look ended, a timer's granularity allowed.</summary>
    private static bool Due(long whole, DriverConfig config) =>
        Stopwatch.GetElapsedTime(whole) >= TimeSpan.FromSeconds(config.PollSeconds) - Early;

    /// <summary>
    /// A look's cause on the loop's chain (FABRIC-EVENT3a): the <c>look</c> topic's <c>timer</c> for a whole look; otherwise a
    /// <c>ledger</c> event naming what moved, its topic kept, since every wake the loop hears is a local fact the plan reads: a
    /// ledger family, a terminal's words kept on its record, a session's end.
    /// </summary>
    private static ChainEvent Cause(LookPart part, Wake? woke) =>
        part == LookPart.Look
            ? ChainEvent.Timer(ChangeTopics.Look)
            : new ChainEvent(ChainEvents.Ledger, woke?.Subject ?? "sessions", woke?.Topic ?? ChangeTopics.Ledger);

    /// <summary>
    /// The look, timed (FABRIC-EVENT2, the design §A2): <c>watch.looked</c> with its ms, what woke it (<c>start</c>, <c>pace</c>,
    /// <c>ended</c> for a session's end, or <c>nudge</c> with the topic and subject a nudge carried, none for a plain one), the part
    /// it ran (FABRIC-EVENT3a: <c>look</c> for the whole look, <c>plan</c> for a wake's local plan), how many nudges it took up, and
    /// whether it failed. Up to the report, not the door's handling of it.
    /// </summary>
    private void Looked(string by, Wake? woke, LookPart part, int nudges, long began, bool failed) =>
        Log?.Info("watch.looked",
            ("ms", (long)Stopwatch.GetElapsedTime(began).TotalMilliseconds),
            ("by", by),
            ("topic", woke?.Topic),
            ("subject", woke?.Subject),
            ("part", part == LookPart.Look ? "look" : "plan"),
            ("nudges", nudges),
            ("failed", failed));

    /// <summary>
    /// One tidy pass (AUTOTIDY1), started on the pool so the look beside it does not wait on its git. A closing loop ends it
    /// with nothing said; what else stops it is said once, in a later look's report.
    /// </summary>
    private Task<IReadOnlyList<string>> TidyAsync(BranchTidying tidy, CancellationToken ct)
    {
        tidy.Began();
        return Task.Run<IReadOnlyList<string>>(async () =>
        {
            try
            {
                // A review step in progress keeps the tree it shows from (REVIEWENV1d), as a session in use keeps its own.
                return await tidy.LookAsync(service, Log, ct, token => BranchTidying.ReviewingAsync(Reviews, service, token))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return [];
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or IOException or UnauthorizedAccessException)
            {
                return [$"tidy  the session branches were not looked at this time, and are looked at again later: {error.Message}"];
            }
        }, CancellationToken.None);
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
            // WORKFLOW1f: its run's named workflow, where it has one, says whether it lands with no press.
            var process = await AutoLander.ProcessAsync(home, config, new ServiceReviewWorld(service), quest, ended.Tree!, quest.Workspace, ct)
                .ConfigureAwait(false);
            AutoLander.Concluded(home, config, events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)), ended.Id, quest, quest.Status,
                "stopped", ended.Tree!, quest.Workspace, process: process);
        }
    }

    /// <summary>The standing choices, or the reader's sentence about why they could not be read and what the loop does about it.</summary>
    private static DriverConfig Load(string path)
    {
        try
        {
            return DriverConfig.Load(path);
        }
        catch (DriverConfigUnreadableException error)
        {
            // The file and where in it are the reader's to say (CONFIGREAD1); what the loop does about it is the watch's.
            throw new DriverException(
                $"{error.Message} Nothing is driven until it reads; the loop keeps watching it, and takes the fix at its next look.");
        }
    }
}
