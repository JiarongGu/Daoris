using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// One run of a watch's loop (<see cref="DriverWatch.RunAsync"/>): the parts it starts beside the looks, the chain whose links 5 and 8 it
/// binds, and the loop that looks at its pace, at a nudge or as a session ends, until the loop's token closes it.
/// </summary>
/// <remarks>
/// <para><b>Who touches each field.</b> The loop's own flow writes and reads every field: the looks it awaits run inside it (link 5's
/// <see cref="LookAsync"/>, the look's wait <see cref="WantedAsync"/>, and a failed look's lines told into <see cref="_carried"/>). Two are
/// reached from elsewhere: <see cref="_effected"/>, which link 2's tidy says into on the ref watch's thread, under its own lock; and
/// <see cref="_config"/>, which link 8's pass beside the look reads (<see cref="WatchNodes"/>), a reference the loop swaps whole, so a pass
/// reads one look's choices, never a torn one.</para>
///
/// <para><b>The order.</b> The parts start in the order a later one reads an earlier: the pull-request moments before the ref watch, whose
/// first judgements read each line's baseline; the runtime before link 5 binds, so a look's withdrawal reads it; link 5 before link 8.
/// They close in reverse: link 8's wake and asks withdrawn before its executor and transport end, the change stream before the sessions
/// settle, and the work beside the looks waited for last, so nothing it writes outlives the loop. A look reads the nudges' count before
/// their plain flag (<see cref="WatchWakes"/>).</para>
///
/// <para><b>What a race would break.</b> A nudge during a look is seen by the count the loop compares before it waits, so it looks again
/// rather than sleep through it; one that swaps the source the wait links to is read as the nudge it is. The sessions run on a source the
/// loop cancels itself as a failure ends it, so no session outlives the loop that watches it.</para>
/// </remarks>
internal sealed class WatchRun(
    DriverWatch watch, WatchWakes wakes, WatchDoors doors,
    Func<TickReport, DriverConfig, Task> onReport, Func<Exception, Task>? onError, CancellationToken ct, Action<TickReport>? said)
{
    /// <summary>What link 5 hears as the <c>look</c> topic's deadline: <c>timer(look)</c>, the whole look.</summary>
    internal static readonly string Whole = ChainEvents.TimerOf(ChangeTopics.Look);

    // What is said and not yet in a report: the sweep's lines, and what a look that failed or is closed had said. The next report
    // opens with them, or `said` has them as the failure is heard.
    private readonly List<string> _carried = [];
    private readonly List<SessionEnded> _carriedEnded = [];

    // What link 2 removed or kept since the last look, said on the ref watch's thread.
    private readonly List<string> _effected = [];

    // The last choices that read, for the wait: a torn file keeps the pace it had.
    private DriverConfig _config = DriverConfig.Empty;

    private RoleStaffing? _staffing;

    // Once, before the first tick: what the last run left `working` with nothing behind it (a crash, a kill, a power cut) ended before this
    // tick counts it against the cap and its repository's lock. Retried each tick until the service has answered it once.
    private bool _swept;

    // Once, as the loop starts: every repository with a checkout here registered from its line, since a line the person
    // moved outside Daoris is read nowhere else. Beside the first looks rather than before them: a workspace's lines are read with git,
    // seconds of it, and no start should wait on that. A pass the service did not answer is tried again at the next look.
    private Task<IReadOnlyList<string>?>? _following;
    private bool _followed;

    // Set as the run starts.
    private WatchBeside _beside = null!;
    private BranchTidying _tidy = null!;
    private System.Threading.Timer _supervisorDue = null!;

    private Task<IReadOnlyList<string>>? _tidying;

    // The conversations that wait for an account, woken beside the looks, one pass at a time.
    private Task? _limiting;

    // What woke each look, for its line in the machine log: the loop's start, its pace, a session's end, or a nudge with what
    // it said moved; and the nudges counted when the last look took them up.
    private string _by = "start";
    private WatchWakes.Wake? _woke;
    private int _counted;

    // The `look` topic's deadline: `pollSeconds` after the last whole look ended, whatever woke the looks
    // between, so a stream of wakes never holds the reconciliation, or the remote sync in it, back.
    private long _whole;

    private WatchNodes? _nodes;
    private TickReport? _looked;

    private ServiceClient Service => watch.Service;

    private string Home => watch.Home;

    private string ConfigPath => watch.ConfigPath;

    private MachineLog? Log => watch.Log;

    private LookStages Stages => watch.Stages;

    private LookSaid Lines => watch.Lines;

    private RunningSessions Running => watch.Running;

    private QuestionSupervisorDecisions Supervisor => watch.Supervisor;

    private bool Share => watch.Share;

    private Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner => watch.Runner;

    private Func<string, Noted, bool>? Stops => watch.Stops;

    private Func<string, string, HandOverStep?, CancellationToken, Task<HandedOver>>? HandsOver => watch.HandsOver;

    private IChatSendBacks? SendsBack => watch.SendsBack;

    private Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? GoesOnLost => watch.GoesOnLost;

    private Action? Noticed => watch.Noticed;

    private ReviewDesk? Reviews => watch.Reviews;

    private Func<IRoleStarts?>? Roles => watch.Roles;

    private Func<bool>? Draining => watch.Draining;

    private Func<DriverConfig, CancellationToken, Task>? Limits => watch.Limits;

    private IChangeSource? Source => watch.Source;

    private Action<string, string, bool>? Heard => watch.Heard;

    /// <summary>The run, as <see cref="DriverWatch.RunAsync"/> says: it returns only once every session it started has written how it ended.</summary>
    internal async Task RunAsync()
    {
        // Once, as the loop starts: how its git is wired, a coded line.
        DriverWatch.Wired(Home);

        // The staffing imported once the start's confirmation is settled, and checked.
        _staffing = RoleStaffing.Of(ConfigPath);
        _staffing?.Check(StaffingCheckAt.Start);
        _beside = new WatchBeside(watch, Service, Home);

        // The session branches tidied beside the looks: a pass once each pace, its lines joining the report of the
        // look that finds it done, as the following's do.
        // The downloads' rule reads and locks the loop's own choices, as the Downloads row's press does.
        // Each of its lines is classed as it is made, the look's pass's and link 2's alike.
        // History's daily clear judges a live session by the loop's processes, and forgets a cleared one's numbering in its record.
        // The floor's third act clears the browser's caches only through the shell's browser host, where it carries one.
        _tidy = watch.Tidying ?? new BranchTidying(Home)
        {
            ConfigPath = ConfigPath, Sizes = SpaceSizes.Shared, Look = Lines, Processes = watch.Processes, Events = watch.Events,
            Caches = watch.Browser as IBrowserCaches, Volume = SpaceVolumes.Read,
        };

        _counted = wakes.Count;
        _whole = Stopwatch.GetTimestamp();

        // The look is link 5, on this loop's own chain: a wake is a `ledger` event, and runs the local
        // plan alone; the `look` topic's deadline is its `timer(look)`, and runs the whole look, the remote sync first. The loop stays the
        // topic's queue, as it is the look's: what wakes it during a look is one look after it, and its wait hears a session's end, which a
        // change watch's pump does not, so the watch under the chain is never started.
        await using var queue = new ChangeWatch();
        var chain = new Chain(queue) { Log = Log };

        // A plugin asked about an open pull request when the remote's facts may have moved (link 6): origin's line heard
        // by the ref watch below, Daoris's push and the window's focus, within the minute. Made first, so the ref watch's first judgements
        // read each line's baseline.
        await using var moments = PullRequestMoments.ForDriver(Home, Service.RegistryAsync, Log);
        moments.Start();
        doors.Focus.Ready(moments.Focused);

        // The home's record files heard, not found at the pace (link 7): the configuration, the plugins folder, the
        // moved lines and the held words each wake the local plan by their topic, and the pace stays their deadline. Both loops run this one
        // watch, so the headless host and the shell hear the same files.
        await using var files = HomeWatch.ForDriver(Home, ConfigPath, watch.Nudge, source: Source, log: Log);
        doors.Files = files;
        files.Start();

        // The refs heard on each repository's common git dir (link 1): a ref move judges that repository's ends on its own,
        // never the look and never the remote sync, and the tidy's pass stays the reconciliation at its pace. Link 2 on the same chain:
        // the tidy as an effect, tried where link 1 found a repository held, on the wakes that can unblock it, at
        // the `refs` deadline and at the start; what it says joins the next look's report.
        var effect = new TidyEffect(
            Home,
            async (name, token) => (await RefWatch.RepositoriesAsync(Home, Service.RegistryAsync, token).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase)),
            token => BranchTidying.InUseAsync(Home, Service, asked => ReviewServed.ReviewingAsync(Reviews, Service, asked), token))
        {
            Saying = _tidy,
            Log = Log,
            Said = lines =>
            {
                lock (_effected) _effected.AddRange(lines);
            },
        };
        await using var refs = RefWatch.ForDriver(Home, Service.RegistryAsync, source: Source, log: Log,
            // The facts that repository's refs move; past their rest where Daoris moved them itself.
            heard: (repository, mine) => Heard?.Invoke(ChangeTopics.Refs, repository, mine),
            // A tidy of a ref move removes a branch and its tree, which the drain does not wait on: refused while it drains.
            tidy: new DrainHeldEffect(effect, watch.DrainsNow));
        doors.Refs = refs;
        refs.Start();

        // The ledger heard, not found at the pace: one wait on the service's change stream for the loop's life, each
        // family that moved waking the local plan by the `ledger` topic, as a quest an agent published, and the pace its deadline.
        using var listening = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ledger = LedgerChanges.RunAsync(Service, watch.Nudge, Log, listening.Token);

        // The start's reconciliation: every pending tidy found from the persisted facts and tried once, beside the first looks.
        var recovering = Task.Run(async () =>
        {
            try
            {
                await refs.RecoverAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The loop closed before the start's reconciliation ended: the next start finds what is still pending.
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Log?.Failed("the start's reconciliation of the tidy", error);
            }
        }, CancellationToken.None);
        // The act journal's reconciliation at the start, beside the first looks so it never holds the come-up: one
        // checkout at a time, each under its own lock alone, one it cannot take left to the next look's writer, which reconciles it too.
        var reconciling = Task.Run(async () =>
        {
            try
            {
                foreach (var line in await new ActReconciler(new ActJournal(Home)).StartAsync(ct).ConfigureAwait(false)) Log?.Info("acts.held", ("said", line));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The loop closed first: every writer's lock reconciles what is still open.
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Log?.Failed("the start's reconciliation of the act journal", error);
            }
        }, CancellationToken.None);

        // Link 8: the evaluation runtime, only where this loop holds a hook set.
        _nodes = WatchNodes.Of(watch, () => _config);
        doors.Nodes = _nodes?.Runtime;
        // The runtime's own wakes run link 8 alone: its timer and a plugin's step, never the local plan.
        using var nodesDue = new System.Threading.Timer(_ => watch.NodesWoken("due"));
        // The look nudged at a held decision's goesOnAt, which releases it.
        using var supervisorDue = new System.Threading.Timer(_ => watch.Nudge(ChangeTopics.Words, SupervisorHanded.From));
        _supervisorDue = supervisorDue;

        chain.Bind(ChainRanks.Plan, async (heard, token) =>
        {
            // Choices that no longer arm the runtime withdraw its awaited set before the plan's supervision reads it, so the
            // look that reads the last graph's removal starts no plugin for it; link 8 then reads nothing.
            if (_nodes is { } nodes) await Stages.TimeAsync("withdraw", () => nodes.Runtime.WithdrawAsync(_config, token)).ConfigureAwait(false);
            _looked = await LookAsync(heard.Any(cause => cause.Key == Whole) ? LookPart.Look : LookPart.Plan, token).ConfigureAwait(false);
            return new ChainFired(_looked.Progressed ? 1 : 0, []);
        });
        _nodes?.Bind(chain, nodesDue, doors, ct);

        // What the sessions run on: the loop's own token, and cancelled by the loop itself when a failure ends it, so no session outlives the
        // loop that watches it.
        using var sessions = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            await LoopAsync(chain, refs, sessions.Token).ConfigureAwait(false);
        }
        catch
        {
            // A failure with nobody to hear it ends the loop: what its look had said is said first.
            SayCarried();
            await sessions.CancelAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            // No wake of the runtime's fires link 8 once the loop closes; nor does a door's ask, and one asked is
            // answered as not passed, so no press waits on a loop that closed.
            doors.Waking(null);
            if (doors.Unasked() is { } unasked) PassAsks.Done(unasked.Take(), passed: false);
            if (watch.Hooks is { } hooks) hooks.StepArrived = null;

            if (_nodes is not null) await _nodes.CloseAsync().ConfigureAwait(false);

            // The change stream's wait ends with the loop, and is waited for, so no ask outlives it.
            await listening.CancelAsync().ConfigureAwait(false);
            await ledger.ConfigureAwait(false);
            await Running.SettledAsync().ConfigureAwait(false);
            // The start's pass ends on the loop's own token; waited for, so nothing it writes outlives the loop.
            if (_following is not null) await _following.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            // The tidy's too: a removal half done when the loop closed is git's to finish, never left running unwatched.
            if (_tidying is not null) await _tidying.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            if (_limiting is not null) await _limiting.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            await recovering.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            await reconciling.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    /// <summary>The looks, until the loop's token closes it; a failure <see cref="onError"/> does not take ends the run.</summary>
    private async Task LoopAsync(Chain chain, RefWatch refs, CancellationToken sessions)
    {
        while (!ct.IsCancellationRequested)
        {
            var nudged = wakes.Count;
            var nudges = nudged - _counted;
            _counted = nudged;
            // Taken after the count, so a plain nudge the count missed still makes this look whole, and is looked at again.
            var everything = wakes.TakeEverything();
            var part = _by is "start" or "pace" || everything || LookDeadline.Due(Stopwatch.GetElapsedTime(_whole), _config) ? LookPart.Look : LookPart.Plan;
            var failed = false;
            var began = Stopwatch.GetTimestamp();
            try
            {
                await TickAsync(chain, part, nudges, began, sessions).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                SayCarried();
                return;
            }
            catch (Exception error) when (onError is not null)
            {
                failed = true;
                Looked(_by, _woke, part, nudges, began, failed: true);
                SayCarried();
                await onError(error).ConfigureAwait(false);
            }

            // A whole look that fails is still the deadline's: the next comes a pace later, not at once.
            if (part == LookPart.Look) _whole = Stopwatch.GetTimestamp();

            await WaitAsync(refs, nudged, failed).ConfigureAwait(false);
        }
    }

    /// <summary>One look of the loop and its report, with what is said beside it since the last.</summary>
    private async Task TickAsync(Chain chain, LookPart part, int nudges, long began, CancellationToken sessions)
    {
        // 🔴 Inside the catch: a hand edit that tears the file would throw from outside it, and the watch would die on its first tick,
        // silently, in the shell. Here it is said, nothing ticks on choices it cannot read, and the next look after the fix ticks again.
        _config = Load(ConfigPath);
        // A home whose staffing the store holds no import of yet is imported by a look, waiting for no lock.
        _staffing?.Check(StaffingCheckAt.Look);

        if (!_swept)
        {
            var orphans = await Stages.TimeAsync("sweep", () => Orphans.EndAsync(Service, watch.Processes, ct: ct)).ConfigureAwait(false);
            // An end nobody pressed and nobody must answer: news.
            foreach (var ended in orphans)
            {
                _carried.Add(Lines.News(
                    "sweep", new LookNews(LookSaid.Swept, ended.Id, ended.Id), $"stopped  session {ended.Id} ({ended.Repository}): {Orphans.Note}"));
            }
            _swept = true;
            // The decisions an earlier run left closed before this first look's carry-ons, held
            // and released ones stale and admitted ones interrupted; their waiting words the look takes back.
            Supervisor.SweepAtStart(DateTimeOffset.UtcNow);
            // A loop that died with a carry-on's move standing is taken up here, at the slot
            // where uncertain words are settled and before it, so the move says the `lost` note of a word it moved and never sent, and the
            // carry-ons the look plans are no longer held by it. Through a driver's holder, whose scope is the lease holder's.
            await Stages.TimeAsync(
                "moves",
                () => new Driver(Service, _config, watch.Adapters, Home, watch.Processes, watch.Sync, watch.Output, watch.Harnesses, watch.Usage, watch.Hooks,
                        watch.Events, watch.Browser, Running)
                    { Log = Log, Share = Share }.TakeUpMovesAsync(ct)).ConfigureAwait(false);
            // The words a shell before this one wrote to its agent and never took off leave
            // their records, said where their conversation never showed them taken, and are never sent again.
            _carried.AddRange(await Stages.TimeAsync("uncertain", () => UncertainWords.SettleAsync(Service, watch.Events, ct, Lines)).ConfigureAwait(false));
        }

        if (!_followed) _following ??= _beside.FollowEveryLineAsync(_config, ct);

        _looked = null;
        await chain.FireAsync([Cause(part, _woke)], sessions).ConfigureAwait(false);
        var report = _looked ?? throw new InvalidOperationException("link 5 of the loop's chain never ran the look.");
        if (_carried.Count > 0 || _carriedEnded.Count > 0)
        {
            report = report with { Events = [.. _carried, .. report.Events], Concluded = [.. _carriedEnded, .. report.Concluded] };
            _carried.Clear();
            _carriedEnded.Clear();
        }

        if (!_followed && _following is { IsCompleted: true } done)
        {
            var lines = done.IsCompletedSuccessfully ? done.Result : null;
            _following = null;
            if (lines is not null)
            {
                _followed = true;
                // Classed where the pass made them.
                if (lines.Count > 0) report = report with { Events = [.. report.Events, .. lines] };
            }
        }

        if (_tidying is { IsCompleted: true } tidied)
        {
            _tidying = null;
            // A pass that failed past its own catches is said, and so observed, rather than left to the finalizer. Classed where the tidy made
            // them.
            IReadOnlyList<string> lines = tidied.IsCompletedSuccessfully ? tidied.Result
                : tidied.Exception?.InnerException is { } failure
                    ? [Lines.Retry("tidy", $"tidy  the session branches were not looked at this time, and are looked at again later: {failure.Message}")]
                    : [];
            if (lines.Count > 0) report = report with { Events = [.. report.Events, .. lines] };
        }

        lock (_effected)
        {
            // Classed where the tidy made them.
            if (_effected.Count > 0) report = report with { Events = [.. report.Events, .. _effected] };
            _effected.Clear();
        }

        // The tidy (and the keeping passes in it) removes trees and branches, and the limits' wake goes conversations on in turns;
        // neither begins while an update drains, and both stay due for the first look that does not.
        if (_tidying is null && _tidy.Due && !watch.DrainsNow()) _tidying = _beside.TidyAsync(_tidy, ct);

        if (_limiting is { IsCompleted: true }) _limiting = null;
        if (_limiting is null && Limits is { } limits && !watch.DrainsNow()) _limiting = _beside.LimitsAsync(limits, _config, ct);

        // The declarations owed acceptance, read before the report so the wake it brings judges them.
        await watch.OwedLookAsync(_config, ct).ConfigureAwait(false);

        Looked(_by, _woke, part, nudges, began, failed: false);
        await onReport(report, _config).ConfigureAwait(false);
    }

    /// <summary>
    /// The wait after a look, interruptible four ways: cancellation ends the loop; the pace, a nudge and a session ending each end the wait,
    /// an ending at once, so a freed slot is used and the ending is said. Not after a failed look: the endings it never reported are still
    /// waiting, and waking on them would look again at once, every time, against a service that is not answering.
    /// </summary>
    private async Task WaitAsync(RefWatch refs, int nudged, bool failed)
    {
        CancellationTokenSource wait;
        try
        {
            wait = wakes.Wait(ct);
        }
        catch (ObjectDisposedException)
        {
            // A nudge swapped the source as the wait began: that is a nudge, so look again.
            (_by, _woke) = ("nudge", wakes.Woke);
            return;
        }

        using (wait)
        {
            // A nudge during the look came before this wait could hear it: look again now.
            if (wakes.Count != nudged)
            {
                (_by, _woke) = ("nudge", wakes.Woke);
                return;
            }

            // Up to the `look` topic's deadline, or the whole pace after a failed look.
            var pace = TimeSpan.FromSeconds(_config.PollSeconds);
            var left = failed ? pace : pace - Stopwatch.GetElapsedTime(_whole);
            await Running.NextAsync(left > TimeSpan.Zero ? left : TimeSpan.Zero, wait.Token, endings: !failed).ConfigureAwait(false);
            _woke = wakes.Count != nudged ? wakes.Woke : null;
            _by = wakes.Count != nudged ? "nudge"
                : failed || LookDeadline.Due(Stopwatch.GetElapsedTime(_whole), _config) ? "pace"
                : "ended";
            // A session ending may free the tree a refused tidy waits on.
            if (_by == "ended") refs.Unblocked(ChainEvents.Ledger);
        }
    }

    /// <summary>
    /// One look of the part link 5 heard, asked once whether an update drains, so the plan and what the report says of it agree.
    /// </summary>
    private async Task<TickReport> LookAsync(LookPart part, CancellationToken token)
    {
        var draining = Draining?.Invoke() == true;
        var driver = new Driver(
                Service, draining ? InstallUpdate.Drained(_config) : _config, watch.Adapters, Home, watch.Processes, watch.Sync, watch.Output,
                watch.Harnesses, watch.Usage, watch.Hooks, watch.Events, watch.Browser, Running)
            {
                Share = Share, Runner = Runner, Stops = Stops, HandsOver = HandsOver, SendsBack = SendsBack, GoesOnLost = GoesOnLost, Log = Log, Noticed = Noticed, Stages = Stages, Reviews = Reviews,
                Part = part, Wanted = token => WantedAsync(part, token),
                // An ended graph run whose chain came back here re-arms link 8, which a wake then judges on what is there now.
                RunReturned = run =>
                {
                    if (doors.Nodes?.Return(run) == true) watch.NodesWoken("returned");
                },
                // A session outlives this look's reading of the file, so its run reads the file again at its turn's end.
                ConfigPath = ConfigPath,
            };
        driver.Context.Supervisor = Supervisor;
        driver.Context.Roles = Roles?.Invoke();
        driver.Context.Supervising = watch.Supervising?.Invoke();
        try
        {
            var report = await driver.TickAsync(token, failed: told =>
            {
                _carried.AddRange(told.Events);
                _carriedEnded.AddRange(told.Concluded);
            }).ConfigureAwait(false);
            return draining ? report with { Considerations = InstallUpdate.HeldFor(report.Considerations, _config) } : report;
        }
        finally
        {
            WatchNodes.Due(_supervisorDue, Supervisor.NextGoesOn());
        }
    }

    /// <summary>
    /// What a look waits on for a start that has not yet opened its record, and leaves it beside on: a nudge since
    /// the loop counted its nudges, an ending waiting to be said, or the <c>look</c> topic's deadline (a whole look's own is a pace from now).
    /// Never throws: the look reads its own token to learn whether it is the close.
    /// </summary>
    private async Task WantedAsync(LookPart part, CancellationToken token)
    {
        CancellationToken paused;
        try
        {
            paused = wakes.Paused;
        }
        catch (ObjectDisposedException)
        {
            return; // A nudge swapped the source as this began: that is a nudge.
        }

        if (wakes.Count != _counted) return;
        var pace = TimeSpan.FromSeconds(_config.PollSeconds);
        var left = part == LookPart.Look ? pace : pace - Stopwatch.GetElapsedTime(_whole);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token, paused);
        await Task.WhenAny(Running.EndedAsync(waiting.Token), Task.Delay(left > TimeSpan.Zero ? left : TimeSpan.Zero, waiting.Token))
            .ConfigureAwait(false);
        await waiting.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>What a look that failed or is closed had said, handed to <c>said</c> where a door says a part of a look alone.</summary>
    private void SayCarried()
    {
        if (said is null || (_carried.Count == 0 && _carriedEnded.Count == 0)) return;
        said(new TickReport([], [.. _carried], Progressed: false, Concluded: [.. _carriedEnded]));
        _carried.Clear();
        _carriedEnded.Clear();
    }

    /// <summary>
    /// A look's cause on the loop's chain: the <c>look</c> topic's <c>timer</c> for a whole look; otherwise a <c>ledger</c>
    /// event naming what moved, its topic kept, since every wake the loop hears is a local fact the plan reads: a ledger family, a terminal's
    /// words kept on its record, a session's end.
    /// </summary>
    private static ChainEvent Cause(LookPart part, WatchWakes.Wake? woke) =>
        part == LookPart.Look
            ? ChainEvent.Timer(ChangeTopics.Look)
            : new ChainEvent(ChainEvents.Ledger, woke?.Subject ?? "sessions", woke?.Topic ?? ChangeTopics.Ledger);

    /// <summary>
    /// The look, timed: <c>watch.looked</c> with its ms, what woke it (<c>start</c>, <c>pace</c>, <c>ended</c>
    /// for a session's end, or <c>nudge</c> with the topic and subject a nudge carried, none for a plain one), the part it ran
    /// (<c>look</c> for the whole look, <c>plan</c> for a wake's local plan), how many nudges it took up, and whether it failed. Up to the
    /// report, not the door's handling of it. Its <c>stages</c>, each part of the look and of the work beside it that ended since
    /// the last line (<see cref="LookStages.Take"/>), so a slow look says which part is slow.
    /// </summary>
    private void Looked(string by, WatchWakes.Wake? woke, LookPart part, int nudges, long began, bool failed) =>
        Log?.Info("watch.looked",
            ("ms", (long)Stopwatch.GetElapsedTime(began).TotalMilliseconds),
            ("by", by),
            ("topic", woke?.Topic),
            ("subject", woke?.Subject),
            ("part", part == LookPart.Look ? "look" : "plan"),
            ("nudges", nudges),
            ("failed", failed),
            ("stages", Stages.Take()));

    /// <summary>The standing choices, or the reader's sentence about why they could not be read and what the loop does about it.</summary>
    private static DriverConfig Load(string path)
    {
        try
        {
            return DriverConfig.Load(path);
        }
        catch (DriverConfigUnreadableException error)
        {
            // The file and where in it are the reader's to say; what the loop does about it is the watch's.
            throw new DriverException(
                $"{error.Message} Nothing is driven until it reads; the loop keeps watching it, and takes the fix at its next look.");
        }
    }
}
