using System.Diagnostics;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The application's half of an install's update: one state (<see cref="UpdateFlow"/>) that a look moves, said once in the machine log
/// as <c>update.state</c> with its why, to the page as <c>UPDATE_STATE</c> and to the terminal in <c>update/state.json</c>. It watches what is
/// staged and what the person said of it, holds the loop's starts while it drains, for the machine's bound at most (<see cref="Bound"/>, then
/// deferred), and once no driven session runs a turn and no conversation's turn or plugin step is in flight — or at once on *Update now* —
/// it stops: checks the build, starts the launcher with <c>--update --after &lt;pid&gt;</c> and closes the application the way the person's
/// close does. At the next start it says once how the swap ended.
/// </summary>
/// <remarks>
/// <para><b>Every effect is handed in</b> — the work, the launcher, the close, the clock — so the tests drive it against a
/// scratch install and close nothing.</para>
///
/// <para><b>A look is quick and answers within <see cref="Look"/></b>. Looks are one at a time. The stop is its own task, started by the
/// look that decides it, so a look never waits on the check: a word, a newer build in the staged folder and the stop's bound
/// (its bound, <see cref="StopBounds"/>, by the clock) are each judged at every look and end it at once, its check cancelled and the presses let go.
/// While it stops the loop looks every <see cref="Tick"/>, so the page and the terminal hear each second what it waits on.</para>
///
/// <para><b>Said in the window and the machine log</b>: <c>update.state</c> for every transition (from, to, why, build and what the
/// moment held), <c>update.waiting</c> every ten seconds a stop waits, <c>update.requested</c> for a word, and at the next start
/// <c>update.installed</c> or <c>update.rolled-back</c>. A name or a count each, never a path: the launcher writes no log, so its journal is
/// turned into these lines here.</para>
/// </remarks>
public sealed class InstallUpdater(
    // The install's root, or null for a workspace build, which never updates this way.
    string? install,
    // The Daoris home, where the request is read and written.
    string home,
    MachineLog log,
    Func<UpdateWork> work,
    // Starts the launcher with `--update --after <pid>`, through the start gate: false when it could not be started.
    Func<CancellationToken, Task<bool>> relaunch,
    // Closes the application after ClosingForUpdate has handed the shell its reason and build.
    Action close,
    IEventBus? bus = null,
    Func<DateTimeOffset>? clock = null,
    // This process, which a confirmed swap records and the launcher waits on.
    int? pid = null) : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _look = new(1, 1);
    private readonly CancellationTokenSource _disposed = new();
    private readonly SemaphoreSlim _poke = new(0);
    private readonly UpdateFlow _flow = new();

    private UpdateState _state = new(UpdateStates.None, null, null, 0, 0, null, null, null);
    private UpdateOutcome? _outcome;
    private UpdateOutcome? _last;
    private readonly UpdateRefusals _refused = new();
    private string? _seen;
    private string? _drainedFor;
    private DateTimeOffset? _drainSince;
    private Stop? _stop;
    private DateTimeOffset? _closingSince;
    private UpdateRequest? _closeAsked;
    private DateTimeOffset? _acted;
    private Task? _loop;

    /// <summary>How long the application waits for its own close: the launcher waits three minutes, so one not taken by then is not coming.</summary>
    public static readonly TimeSpan CloseBound = TimeSpan.FromMinutes(5);

    /// <summary>The check's floor and place in the machine's admission (<see cref="StopPlace"/>).</summary>
    public CheckPlacing Placing { get; init; } = CheckPlacing.Machine;

    /// <summary>How long a look that has begun a stop waits for it before it answers that it stops: a word is answered within this.</summary>
    public TimeSpan Look { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How often the loop looks while a stop or a close waits: each second says what it waits on.</summary>
    public TimeSpan Tick { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How often the loop looks when nothing pokes it, while draining or deferred.</summary>
    public TimeSpan Pace { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long a drain may hold new work, read at each of its looks: the machine's <c>drain</c> (<see cref="DrainSetting"/>).</summary>
    public Func<TimeSpan> Bound { get; init; } = () => TimeSpan.FromMinutes(DrainSetting.Default);

    /// <summary>The version this application runs, which a staged build below goes back from (<see cref="InstallRollback"/>).</summary>
    public string? Running { get; init; } = UpdateRefusal.Running;

    /// <summary>The build's start on a copy of the home, asked last of the check, and ended when the stop is (a word, a newer build, its bound).</summary>
    public Func<string, string, string?, CancellationToken, (string Code, string Sentence)?>? Proof { get; init; }

    /// <summary>The refusal's code for a going back while a delivery is unresolved.</summary>
    public const string Deliveries = "deliveries";

    /// <summary>The refusal's code for a stop that outlasted its bound (<see cref="StopBounds"/>).</summary>
    public const string BoundCode = "bound";

    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();

    /// <summary>Hand the close its staged build before closing any window.</summary>
    public Action<string>? ClosingForUpdate { get; init; }

    /// <summary>
    /// Whether the loop's starts are held for the update: the driver watch asks at every look. True while it drains, stops and applies; once
    /// deferred, only while a stop holds the presses to check the build, which <see cref="Applying"/> tells apart.
    /// </summary>
    public bool Draining
    {
        get
        {
            lock (_gate) return _flow.State is UpdateStates.Draining or UpdateStates.Stopping or UpdateStates.Applying;
        }
    }

    /// <summary>
    /// Whether the application stops taking work for the update: from the moment a look finds the work allows it, or *Update now* is said, until
    /// the stop is ended (a refusal, a word, a newer build or its bound) or the application has closed. The pipe holds every press from here,
    /// as at a close; while it only drains, it lets through what starts nothing the drain waits on. The build is checked and the work read
    /// again under this hold, and work begun since the first read withdraws it, the drain going on.
    /// </summary>
    public bool Applying
    {
        get
        {
            lock (_gate) return _flow.State is UpdateStates.Stopping or UpdateStates.Applying;
        }
    }

    /// <summary>Where the update stands now.</summary>
    public UpdateState State
    {
        get
        {
            lock (_gate) return _state;
        }
    }

    /// <summary>
    /// At the application's start: a swap waiting for this start is confirmed, and said once as installed; a
    /// swap the launcher rolled back or refused, or one it finished without a word from the application, is said once and
    /// marked told. Then it looks, as <see cref="LookAsync"/>.
    /// </summary>
    public async Task StartedAsync()
    {
        if (install is null) return;
        UpdateOutcome? outcome = null;
        try
        {
            if (StagedBuild.Confirm(install, pid ?? Environment.ProcessId) && StagedBuild.ReadJournal(install) is { } confirmed)
            {
                outcome = Outcome(confirmed with { Phase = SwapPhase.Installed });
                log.Info("update.installed", Ending(confirmed, ("build", confirmed.Id), ("version", confirmed.Version), ("confirmed", true)));
            }
            else if (StagedBuild.ReadJournal(install) is { Told: false } last
                     && last.Phase is SwapPhase.Installed or SwapPhase.RolledBack or SwapPhase.Refused)
            {
                outcome = Outcome(last);
                if (last.Phase == SwapPhase.Installed)
                {
                    log.Info("update.installed",
                        Ending(last, ("build", last.Id), ("version", last.Version), ("confirmed", last.Confirmed ?? false)));
                }
                else
                {
                    log.Warn($"update.{last.Phase}", Ending(last, ("build", last.Id), ("reason", last.Reason)));
                }

                StagedBuild.Tell(install);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A journal that cannot be read or written is no outcome to say; the swap's own record stays unchanged.
        }

        try
        {
            // A start nothing can roll back is the home's newest build, which the launcher refuses an older one by.
            if (StartConfirmation.Read(install).Is) HomeLastBuild.Keep(home, Running);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Kept at the next start; until then the launcher refuses by the build kept before.
        }

        lock (_gate) _outcome = outcome;
        await LookAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// One look: what is staged, what holds for it, what runs — and a stop begun when its moment has come. It judges a stop under way (a
    /// word, a newer build, its bound) and answers within <see cref="Look"/>; it never waits on the check.
    /// </summary>
    public async Task<UpdateState> LookAsync()
    {
        await _look.WaitAsync(_disposed.Token).ConfigureAwait(false);
        try
        {
            return await LookOnceAsync().ConfigureAwait(false);
        }
        finally
        {
            _look.Release();
        }
    }

    private async Task<UpdateState> LookOnceAsync()
    {
        if (install is null) return Publish(Compose(null, null, null, null));

        var manifest = StagedBuild.Read(install, out var unread);
        var id = manifest is null ? unread is null ? null : $"unread:{unread.Code}" : manifest.Id;
        var staged = manifest is null ? null : new UpdateBuild(manifest.Id, manifest.Version, manifest.Commit, manifest.At);
        var request = InstallUpdate.Read(home);
        if (request is not null) lock (_gate) _acted = request.At;

        // A stop under way holds while it is for the build staged now, under the word that asked it, within its bound.
        Stop? stop;
        lock (_gate) stop = _stop;
        if (stop is not null)
        {
            if (stop.Run.IsCompleted)
            {
                lock (_gate) _stop = null;
            }
            else
            {
                var ended = StopEnded(stop, id, request);
                if (ended is null)
                {
                    SayWaiting(stop);
                    return Publish(Compose(staged, stop.Mode, null, null, drain: true));
                }

                await EndAsync(stop).ConfigureAwait(false);
                if (ended == UpdateWhy.StopBound)
                {
                    var on = stop.On;
                    return Refuse(stop.Build, staged, stop.Mode, stop.Asked, new UpdateProblem(BoundCode, stop.Place.Words(WaitWords(on), on == UpdateWaits.Place)), UpdateWhy.StopBound, on);
                }

                return await LookOnceAsync().ConfigureAwait(false);
            }
        }

        UpdateState? closing = Closing(staged, request);
        if (closing is not null) return closing;

        // The build a finished swap left behind: the launcher cannot delete its own running launcher inside it.
        StagedBuild.ClearPrevious(install);

        // Read at every look, as the terminal reads it at every `update`: a dismissal puts away the outcome only.
        var last = Ended(StagedBuild.ReadJournal(install));
        lock (_gate) _last = last;

        if (id is null) return Publish(NothingStaged());

        UpdateProblem? known;
        lock (_gate) known = _refused.For(id, request);
        if (known is not null)
        {
            Enter(UpdateStates.Refused, UpdateWhy.Refusal, id, staged, null, known.Code);
            return Publish(Compose(staged, null, null, known));
        }

        if (unread is not null) return Refuse(id, staged, null, request, new UpdateProblem(unread.Code, unread.Sentence), UpdateWhy.Refusal, unread.Code);

        bool restaged;
        lock (_gate)
        {
            restaged = _seen is not null && !string.Equals(_seen, id, StringComparison.Ordinal);
            _seen = id;
            if (restaged || _drainedFor is not null && !string.Equals(_drainedFor, id, StringComparison.Ordinal))
            {
                _drainedFor = null;
                _drainSince = null;
            }
        }

        var mode = InstallUpdate.ModeFor(request, manifest!.Id);
        if (mode == UpdateMode.NotNow)
        {
            lock (_gate)
            {
                _drainedFor = null;
                _drainSince = null;
            }

            Enter(UpdateStates.Staged, restaged ? UpdateWhy.Restaged : UpdateWhy.NotNow, id, staged, null);
            return Publish(Compose(staged, mode, null, null));
        }

        var now = work();
        if (mode == UpdateMode.Now) return await BeginStopAsync(id, staged, mode, request, now, "now", UpdateWhy.Now).ConfigureAwait(false);

        var entering = restaged ? UpdateWhy.Restaged : request is null ? UpdateWhy.Seen : UpdateWhy.WhenIdle;
        lock (_gate)
        {
            if (!string.Equals(_drainedFor, id, StringComparison.Ordinal))
            {
                _drainedFor = id;
                _drainSince = Now;
            }
        }

        return now.Idle
            ? await BeginStopAsync(id, staged, mode, request, now, "idle", UpdateWhy.Idle).ConfigureAwait(false)
            : Publish(Drains(id, staged, mode, now, entering));
    }

    /// <summary>The drain by the bound read now: holding new work until its <see cref="UpdateState.Until"/>, deferred from then on.</summary>
    private UpdateState Drains(string id, UpdateBuild? staged, string mode, UpdateWork now, string entering)
    {
        var bound = Bound();
        DateTimeOffset? since;
        lock (_gate) since = _drainSince;
        var deferred = since is { } began && Now - began >= bound;
        var to = deferred ? UpdateStates.Deferred : UpdateStates.Draining;
        string why;
        lock (_gate) why = deferred && _flow.State == UpdateStates.Draining && _flow.Build == id ? UpdateWhy.Bound : entering;
        Enter(to, why, id, staged, now, null, ("waitedSeconds", since is { } started ? (long)(Now - started).TotalSeconds : null));
        return Compose(staged, mode, now, null, since: since, until: since + bound);
    }

    /// <summary>
    /// The person's word, from the screen, written to the request the terminal writes, for the build staged now,
    /// and looked at at once — so *Update now* is acted on before this answers.
    /// </summary>
    /// <exception cref="Shenora.Core.Ipc.ShenoraException">
    /// <see cref="Refusals.UpdateModeUnknown"/> for a mode this build does not take; <see cref="Refusals.UpdateNothingStaged"/>
    /// when there is no install or nothing staged to say it of.
    /// </exception>
    public async Task<UpdateState> SayAsync(string? mode, string door)
    {
        if (install is null || !StagedBuild.IsStaged(install))
        {
            throw Refusals.Because(Refusals.UpdateNothingStaged, "nothing is staged beside this install, so there is nothing to install.");
        }

        if (mode is null || !UpdateMode.All.Contains(mode, StringComparer.Ordinal))
        {
            throw Refusals.Because(
                Refusals.UpdateModeUnknown, $"`{mode}` is not a word for an update: one of {string.Join(", ", UpdateMode.All)}.",
                ("mode", mode ?? ""));
        }

        var build = StagedBuild.Read(install, out _)?.Id;
        InstallUpdate.Write(home, new UpdateRequest(mode, build, Now));
        log.Info("update.requested", ("mode", mode), ("door", door), ("build", build));
        return await LookAsync().ConfigureAwait(false);
    }

    /// <summary>The outcome the banner shows, said and put away; the last swap stays, as the journal keeps it.</summary>
    public Task<UpdateState> DismissAsync()
    {
        lock (_gate) _outcome = null;
        return LookAsync();
    }

    /// <summary>
    /// How often the loop looks while nothing is staged or draining: the reconciliation behind the watch of the staged
    /// folder and the request, which wake it at once. A drain, deferred or not, looks every <see cref="Pace"/>: no file says a session ended.
    /// </summary>
    public TimeSpan Idle { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>What watches the staged folder and the request: the machine's watchers unless a test hands its fake.</summary>
    public IChangeSource? Source { get; init; }

    /// <summary>The deadline of the staged folder's topic: it finds a folder that did not exist when the watch opened, and what its watcher missed.</summary>
    public TimeSpan Reconcile { get; init; } = TimeSpan.FromSeconds(5);

    private HomeWatch? _files;

    /// <summary>
    /// Look at the loop's pace, and at once when poked or when the staged folder or the request moves, until stopped.
    /// A build staged or a word said by the terminal is heard within the watch's debounce, not at the next pace.
    /// </summary>
    public void Start()
    {
        if (install is not null && _files is null)
        {
            var folder = Path.Combine(install, StagedBuild.Folder);
            _files = new HomeWatch((_, _) => Poke(), Reconcile, Source);
            _files.Topic(
                ChangeTopics.Update, ChangeTopics.Update,
                HomePart.File(InstallUpdate.PathOf(home)),
                HomePart.Files(
                    folder, [StagedBuild.Journal, $"{StagedBuild.Staged}/{StagedBuild.Manifest}"], [StagedBuild.Staged]));
            _files.Start();
        }

        _loop ??= Task.Run(async () =>
        {
            var ct = _disposed.Token;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await LookAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    log.Warn("update.look", ("message", error.Message));
                }

                try
                {
                    var state = State.State;
                    await _poke.WaitAsync(
                        state is UpdateStates.Stopping or UpdateStates.Applying ? Tick
                        : state is UpdateStates.Draining or UpdateStates.Deferred || _files is null ? Pace : Idle, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>Look now, rather than at the next pace.</summary>
    public void Poke()
    {
        try
        {
            _poke.Release();
        }
        catch (ObjectDisposedException)
        {
            // The updater is gone: a stop that ends after it has nothing to wake.
        }
    }

    public void Dispose()
    {
        _disposed.Cancel();
        Stop? stop;
        lock (_gate) stop = _stop;
        try
        {
            stop?.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The stop has ended.
        }

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(5));
            stop?.Run.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Cancellation, as it should surface.
        }

        _files?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _disposed.Dispose();
        _poke.Dispose();
    }

    /// <summary>One stop: for a build, under the word that asked it, by what asked it, begun at a time; its task, and what it waits on now.</summary>
    private sealed class Stop(string build, string mode, UpdateRequest? asked, string by, DateTimeOffset began, CancellationTokenSource cts, StopPlace place)
    {
        public StopPlace Place { get; } = place;

        public string Build { get; } = build;

        public string Mode { get; } = mode;

        public UpdateRequest? Asked { get; } = asked;

        public string By { get; } = by;

        public DateTimeOffset Began { get; } = began;

        public CancellationTokenSource Cts { get; } = cts;

        public Task Run { get; set; } = Task.CompletedTask;

        public volatile string On = UpdateWaits.Check;

    }

    private string? StopEnded(Stop stop, string? id, UpdateRequest? request)
    {
        if (!string.Equals(id, stop.Build, StringComparison.Ordinal)) return UpdateWhy.Restaged;
        if (Withdrawn(stop, request, id)) return UpdateWhy.NotNow;
        var (since, limit) = stop.Place.Held();
        return Now - since >= limit ? UpdateWhy.StopBound : null;
    }

    /// <summary>
    /// Whether a word said since the stop began ends it: a stop by idle holds through no word and the default's, ends on *Now* or *Not now*; one
    /// by *Now* ends on any word other than the one that asked it.
    /// </summary>
    private static bool Withdrawn(Stop stop, UpdateRequest? request, string? id) =>
        stop.By == "now" ? !SameWord(request, stop.Asked) : InstallUpdate.ModeFor(request, id) != UpdateMode.WhenIdle;

    private static bool SameWord(UpdateRequest? a, UpdateRequest? b) => a?.Mode == b?.Mode && a?.Build == b?.Build;

    /// <summary>Cancel the stop and let go of it: the unwind (a copy under way, the scratch's removal) finishes on its own, and waits no longer than one look.</summary>
    private async Task EndAsync(Stop stop)
    {
        stop.Cts.Cancel();
        lock (_gate) if (_stop == stop) _stop = null;
        try
        {
            await Task.WhenAny(stop.Run, Task.Delay(Look, _disposed.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The application is stopping.
        }
    }

    /// <summary>Begin the stop: the presses are held from here, and the look answers `stopping` or, where it ends within <see cref="Look"/>, how it ended.</summary>
    private async Task<UpdateState> BeginStopAsync(string id, UpdateBuild? staged, string mode, UpdateRequest? asked, UpdateWork now, string by, string why)
    {
        Stop stop;
        double waited;
        lock (_gate)
        {
            if (_stop is { } under) return _state;
            waited = _drainSince is { } since ? Math.Max(0, (Now - since).TotalSeconds) : 0;
            stop = _stop = new Stop(id, mode, asked, by, Now, CancellationTokenSource.CreateLinkedTokenSource(_disposed.Token), new StopPlace(home, Now, Placing));
        }

        Enter(UpdateStates.Stopping, why, id, staged, now, null, ("by", by), ("waitedSeconds", (long)waited));
        stop.Run = Task.Run(() => StopAsync(stop, staged, now));
        try
        {
            await Task.WhenAny(stop.Run, Task.Delay(Look, _disposed.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return State;
        }

        if (!stop.Run.IsCompleted) return Publish(Compose(staged, mode, now, null, drain: true));

        // It ended within the look: how, is the state it left; one it left stopping a word withdrew, and is read again.
        lock (_gate) if (_stop == stop) _stop = null;
        bool stillStopping;
        lock (_gate) stillStopping = _flow.State == UpdateStates.Stopping;
        return stillStopping ? await LookOnceAsync().ConfigureAwait(false) : State;
    }

    /// <summary>
    /// The stop: the build checked (its files, a going back, its start on a copy of the home), the work read again where idle
    /// asked, the launcher started and the application closed. Each step says what it waits on; a refusal, a word read since, a newer build
    /// or the bound ends it, and work goes on.
    /// </summary>
    private async Task StopAsync(Stop stop, UpdateBuild? staged, UpdateWork now)
    {
        var ct = stop.Cts.Token;
        try
        {
            stop.On = UpdateWaits.Check;
            var check = Task.Run(() => stop.Place.RunAsync(
                () => Now, () => stop.On = UpdateWaits.Place, () => stop.On = UpdateWaits.Check,
                () => UpdateRefusal.Of(install!, home, staged?.Version, Running) ?? Proof?.Invoke(install!, home, staged?.Version, ct), ct), ct);
            await Task.WhenAny(check, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            var refused = await check.ConfigureAwait(false);
            if (refused is var (code, why))
            {
                stop.Place.Measure(Now);
                Fail(stop, staged, new UpdateProblem(code, why), UpdateWhy.Refusal, code);
                return;
            }

            // What began after the look read no work (a turn a press let through opened, a helper the look started, a conversation the
            // window began, a plugin step a pass sent) is read again now that no press passes, and the drain goes on for it. *Update now*
            // closes whatever runs, so it reads nothing.
            if (stop.By == "idle" && work() is { Idle: false } again)
            {
                if (!Holds(stop)) return;
                var bound = Bound();
                DateTimeOffset? since;
                lock (_gate) since = _drainSince;
                var deferred = since is { } began && Now - began >= bound;
                Enter(deferred ? UpdateStates.Deferred : UpdateStates.Draining, UpdateWhy.WorkBegan, stop.Build, staged, again);
                lock (_gate) _stop = null;
                Publish(Compose(staged, stop.Mode, again, null, since: since, until: since + bound));
                return;
            }

            // The check takes a while: a word said meanwhile is the person's latest, so this stop is withdrawn.
            if (Withdrawn(stop, InstallUpdate.Read(home), stop.Build)) return;

            stop.On = UpdateWaits.Launcher;
            bool started;
            try
            {
                // Awaited through the start gate. *Update now*'s SET started it in its synchronous part, on the window's thread.
                started = await relaunch(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                    started = false;
            }

            if (!started)
            {
                ct.ThrowIfCancellationRequested();
                Fail(stop, staged, new UpdateProblem(
                    "launcher", "the launcher at the install's root would not start, so the application stays open and nothing was replaced."),
                    UpdateWhy.Launcher, null);
                return;
            }

            // The launcher is running and waits for this process: the state is applying from this instant, in the block that ends the stop,
            // so no look finds a stopping state with no stop. A look that ended the stop first has moved the state on; the launcher, too late
            // to hold, waits three minutes for a close that does not come and puts the build aside, and the next start stages it again.
            bool closing;
            lock (_gate)
            {
                closing = Holds(stop);
                if (closing)
                {
                    var waited = _drainSince is { } since ? Math.Max(0, (Now - since).TotalSeconds) : 0;
                    _closingSince = Now;
                    _closeAsked = stop.Asked;
                    _stop = null;
                    Enter(UpdateStates.Applying, UpdateWhy.Checked, stop.Build, staged, now, null, ("by", stop.By), ("waitedSeconds", (long)waited));
                }
            }

            if (!closing) return;
            stop.Place.Measure(Now);
            // Said and done with: a word that outlived its build would hold for the next one (InstallUpdate.ModeFor).
            InstallUpdate.Clear(home);
            log.StopBecause(StopReasons.Update, ("build", stop.Build), ("by", stop.By));
            Publish(Compose(staged, stop.Mode, now, null, drain: true));
            try
            {
                ClosingForUpdate?.Invoke(stop.Build);
                close();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ObjectDisposedException)
            {
                // A close that threw closes nothing: the presses go on, said as a build refused for it.
                lock (_gate) _closingSince = null;
                Refuse(stop.Build, staged, null, null, new UpdateProblem(
                    "close", "the application could not close for the update, so it stays open and nothing was replaced."), UpdateWhy.Close, "threw");
            }
        }
        catch (OperationCanceledException)
        {
            // Ended by a look (a word, a newer build, the bound) or by the application's own close: that one says why.
        }
        catch (Exception error)
        {
            // A check or a read that failed closes nothing, the presses go on, and the build is refused until the person says another word.
            Fail(stop, staged, new UpdateProblem("check", $"the staged build could not be checked: {error.Message}"), UpdateWhy.Refusal, "check");
        }
        finally
        {
            stop.Cts.Dispose();
            Poke();
        }
    }

    /// <summary>Whether this stop is still the one the updater holds, and not one a look ended.</summary>
    private bool Holds(Stop stop)
    {
        lock (_gate) return _stop == stop && !stop.Cts.IsCancellationRequested;
    }

    /// <summary>The stop failed: refused for its build under the word that asked it, unless a look ended it first.</summary>
    private void Fail(Stop stop, UpdateBuild? staged, UpdateProblem problem, string why, string? detail)
    {
        if (!Holds(stop)) return;
        lock (_gate) _stop = null;
        Refuse(stop.Build, staged, stop.Mode, stop.Asked, problem, why, detail);
    }

    /// <summary>The application, which closed for an update, did not within <see cref="CloseBound"/>; or a word said since ends the wait. Null where it still waits or never closed.</summary>
    private UpdateState? Closing(UpdateBuild? staged, UpdateRequest? request)
    {
        DateTimeOffset? closing;
        UpdateRequest? asked;
        string? build;
        lock (_gate)
        {
            if (_flow.State != UpdateStates.Applying) return null;
            closing = _closingSince;
            asked = _closeAsked;
            build = _flow.Build;
        }

        // Still here after asking: the close did not take. Held no longer once the launcher has moved the build aside, the person
        // says another word, or the close has had its bound.
        if (staged is null) return null;
        if (request is { } word && !SameWord(word, asked))
        {
            lock (_gate) _closingSince = null;
            return null;
        }

        if (closing is { } since && Now - since >= CloseBound)
        {
            lock (_gate) _closingSince = null;
            var minutes = (int)Math.Round((Now - since).TotalMinutes);
            return Refuse(build ?? staged.Id, staged, null, null, new UpdateProblem(
                "close", $"the application did not close for the update within {minutes} minutes, so it stays open and nothing was replaced."),
                UpdateWhy.Close, "bound");
        }

        return Publish(Compose(staged, null, null, null, drain: true));
    }

    /// <param name="asked">The person's word it is refused under, which a new word replaces, trying again (<see cref="UpdateRefusals"/>).</param>
    private UpdateState Refuse(string id, UpdateBuild? staged, string? mode, UpdateRequest? asked, UpdateProblem problem, string why, string? detail)
    {
        lock (_gate)
        {
            _refused.Keep(id, asked, problem);
            _drainedFor = null;
            _drainSince = null;
        }

        Enter(UpdateStates.Refused, why, id, staged, null, detail ?? problem.Code, ("reason", problem.Code), ("sentence", UpdateRefusalWords.Logged(problem.Message, install, home)));
        return Publish(Compose(staged, mode, null, problem));
    }

    /// <summary>Nothing is staged: what the last swap left (installed, put back, refused) until it is put away, else none.</summary>
    private UpdateState NothingStaged()
    {
        lock (_gate)
        {
            _drainedFor = null;
            _drainSince = null;
            _seen = null;
        }

        UpdateOutcome? outcome;
        lock (_gate) outcome = _outcome;
        switch (outcome?.Phase)
        {
            case SwapPhase.Installed:
                Enter(UpdateStates.Installed, UpdateWhy.Started, outcome.Build, null, null);
                return Compose(null, null, null, null);
            case SwapPhase.RolledBack or SwapPhase.Refused:
                var problem = new UpdateProblem(outcome.Reason ?? outcome.Phase, outcome.Detail ?? "the swap did not take.");
                Enter(UpdateStates.Refused, UpdateWhy.Swap, outcome.Build, null, null, outcome.Reason);
                return Compose(null, null, null, problem);
            default:
                Enter(UpdateStates.None, _flow.State == UpdateStates.Installed ? UpdateWhy.Dismissed : UpdateWhy.Gone, null, null, null);
                return Compose(null, null, null, null);
        }
    }

    /// <summary>The state moved to <paramref name="to"/>: the transition, if it is one, said once in the log.</summary>
    private void Enter(string to, string why, string? build, UpdateBuild? staged, UpdateWork? now, string? detail = null, params (string Key, object? Value)[] more)
    {
        UpdateTransition? made;
        lock (_gate) made = _flow.Move(to, why, build, Now, detail);
        if (made is null) return;
        (string Key, object? Value)[] said =
        [
            ("from", made.From), ("to", made.To), ("why", made.Why), ("build", made.Build), ("version", staged?.Version), ("detail", made.Detail),
            ("driven", now?.Driven), ("turns", now?.Turns), ("steps", now?.Steps), .. more,
        ];
        if (to == UpdateStates.Refused) log.Warn("update.state", said);
        else log.Info("update.state", said);
    }

    /// <summary>What a stop waits on, said in the log every ten seconds it waits, and each second to the page and the terminal.</summary>
    private void SayWaiting(Stop stop)
    {
        if (stop.Place.ToSay(stop.On, Now) is not { } said) return;
        log.Info("update.waiting", ("build", stop.Build), ("on", said.On), ("seconds", said.Seconds), ("bound", said.Bound));
    }

    private UpdateState Compose(UpdateBuild? staged, string? mode, UpdateWork? now, UpdateProblem? problem, DateTimeOffset? since = null, DateTimeOffset? until = null, bool drain = false)
    {
        lock (_gate)
        {
            // While it stops or applies the drain's since and the work the drain counted stay what they were.
            if (drain && since is null) since = _drainSince;
            var counted = now ?? (drain ? new UpdateWork(_state.Driven, _state.Turns) { Steps = _state.Steps } : null);
            return new UpdateState(_flow.State, staged, mode, counted?.Driven ?? 0, counted?.Turns ?? 0, problem, _outcome, _last, since)
            {
                Steps = counted?.Steps ?? 0,
                Until = until ?? (drain ? _state.Until : null),
                Why = _flow.Why,
                Entered = _flow.Since,
                Transition = _flow.Last,
                Waiting = WaitingNow(),
            };
        }
    }

    /// <summary>What a stop or a close waits on now; called under the gate.</summary>
    private UpdateWaiting? WaitingNow() => _flow.State switch
    {
        UpdateStates.Stopping when _stop is { } stop => stop.Place.Waiting(stop.On, Now),
        UpdateStates.Applying when _closingSince is { } since => new UpdateWaiting(UpdateWaits.Close, Seconds(since), (int)CloseBound.TotalSeconds),
        _ => null,
    };

    private int Seconds(DateTimeOffset since) => (int)Math.Max(0, (Now - since).TotalSeconds);

    private static string WaitWords(string on) => on switch
    {
        UpdateWaits.Place => "a place in the machine's admission",
        UpdateWaits.Check => "the check of the staged build",
        UpdateWaits.Launcher => "the launcher to start",
        _ => "the close",
    };

    private static UpdateOutcome Outcome(SwapRecord record) =>
        new(record.Phase, record.Id, record.Version, record.Commit, record.Reason, record.Detail);

    /// <summary>
    /// A line about how a swap ended, with what it found held: the launcher writes no log, so its journal's holds are
    /// said here, a count and the longest in milliseconds, and nothing of them when it met none. The longest hold's entry, by
    /// its name alone, and the processes found holding it, where the swap found them.
    /// </summary>
    private static (string Key, object? Value)[] Ending(SwapRecord record, params (string Key, object? Value)[] data)
    {
        if (record.Holds is not { Count: > 0 } holds) return data;
        var longest = holds.MaxBy(hold => hold.Ms)!;
        (string Key, object? Value)[] said = [.. data, ("holds", holds.Count), ("heldMs", longest.Ms)];
        return longest is { Held: { } held, By: { } by } ? [.. said, ("held", Path.GetFileName(held)), ("heldBy", by)] : said;
    }

    /// <summary>
    /// How the journal's swap ended, told or not: installed, rolled back or refused as written; confirmed by this
    /// start, installed, as <see cref="StartedAsync"/> says it before the launcher finishes; and none while one is under way or
    /// there is no journal, since the page words only an ending.
    /// </summary>
    private static UpdateOutcome? Ended(SwapRecord? record) => record?.Phase switch
    {
        SwapPhase.Installed or SwapPhase.RolledBack or SwapPhase.Refused => Outcome(record),
        SwapPhase.Confirmed => Outcome(record with { Phase = SwapPhase.Installed }),
        _ => null,
    };

    /// <summary>The state, kept, told to the page when it changed and written for the terminal (<c>update/state.json</c>).</summary>
    private UpdateState Publish(UpdateState state)
    {
        bool changed;
        lock (_gate)
        {
            // The outcome and the last swap are read at the look, never composed from the past.
            state = state with { Last = _last };
            changed = state != _state;
            _state = state;
        }

        if (changed && bus is not null) _ = bus.EmitAsync("DAORIS", "UPDATE_STATE", state);
        // Written at every look, not only a change: a word's answer is the file's next write, which a terminal waits for.
        if (install is not null) UpdateStateFile.Write(install, Record(state));
        return state;
    }

    private static readonly long ThisStart = Started();

    private static long Started()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }

    private UpdateStateRecord Record(UpdateState state) => new(
        state.State, state.Why, state.Staged?.Id, state.Staged?.Version, state.Mode, state.Entered, state.Driven, state.Turns, state.Steps, state.Until,
        state.Waiting is { } waiting ? new UpdateStateWait(waiting.On, waiting.Seconds, waiting.Bound) : null,
        state.Problem?.Code, state.Problem?.Message,
        state.Transition is { } t ? new UpdateStateStep(t.From, t.To, t.Why, t.At, t.Detail) : null,
        pid ?? Environment.ProcessId, ThisStart, Now) { Word = Acted() };

    private DateTimeOffset? Acted()
    {
        lock (_gate) return _acted;
    }
}
