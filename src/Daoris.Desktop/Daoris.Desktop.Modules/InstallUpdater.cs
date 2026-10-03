using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>What the drain waits on (D139 §2): driven sessions this loop runs, and conversations with a turn in flight.</summary>
public sealed record UpdateWork(int Driven, int Turns);

/// <summary>The staged build, as the banner names it: no path of this machine's.</summary>
public sealed record UpdateBuild(string Id, string Version, string? Commit, DateTimeOffset? At);

/// <summary>Why the staged build is not being installed: the check's code, which the page words, and the driver's sentence.</summary>
public sealed record UpdateProblem(string Code, string Message);

/// <summary>How a swap ended (D139 §6): its phase — installed, rolled back or refused — the build, and the reason.</summary>
public sealed record UpdateOutcome(string Phase, string? Build, string? Version, string? Commit, string? Reason, string? Detail);

/// <summary>Where an install's update stands, whole — what the banner renders and <c>UPDATE_STATE</c> carries.</summary>
/// <param name="State"><see cref="UpdateStates"/>'s words.</param>
/// <param name="Mode">The mode that holds for the staged build (<see cref="UpdateMode"/>), or null when none is staged.</param>
/// <param name="Outcome">The last swap, said once at the start after it and kept until the banner's *Dismiss*.</param>
/// <param name="Last">
/// The last swap as the journal records it, told or not, on every state (UPDATE1d): what Settings → Driver's row says
/// and the terminal's plain <c>daoris-driver update</c> says (D50). Null with no journal, and while a swap is under way.
/// </param>
public sealed record UpdateState(
    string State, UpdateBuild? Staged, string? Mode, int Driven, int Turns, UpdateProblem? Problem, UpdateOutcome? Outcome,
    UpdateOutcome? Last);

/// <summary>The states an update is in, as the page reads them.</summary>
public static class UpdateStates
{
    /// <summary>Nothing staged, or no install at all.</summary>
    public const string None = "none";

    /// <summary>Staged, and the person said *Not now*: work goes on.</summary>
    public const string Waiting = "waiting";

    /// <summary>Staged and installed when idle: nothing new starts, and what runs is counted down.</summary>
    public const string Draining = "draining";

    /// <summary>Checked, the launcher started, and the application closing for it.</summary>
    public const string Applying = "applying";

    /// <summary>The staged build failed its check, or the launcher would not start: nothing was replaced, and work goes on.</summary>
    public const string Refused = "refused";
}

/// <summary>
/// The application's half of an install's update (UPDATE1, D139 §2–§4, §6): it watches what is staged and what the person
/// said of it, holds the loop's starts while it drains, and once no driven session runs and no turn is in flight — or at
/// once on *Update now* — checks the build, starts the launcher with <c>--update --after &lt;pid&gt;</c> and closes the
/// application the way the person's close does. At the next start it says once how the swap ended.
/// </summary>
/// <remarks>
/// <para><b>Every effect is handed in</b> — the work, the launcher, the close, the clock — so the tests drive it against a
/// scratch install and close nothing. The loop looks every <see cref="Pace"/> and at every word, and the page hears each
/// change once as <c>UPDATE_STATE</c>.</para>
///
/// <para><b>Said in the window and the machine log</b> (D139 §2): <c>update.staged</c>, <c>update.draining</c>,
/// <c>update.requested</c>, <c>update.applying</c>, <c>update.refused</c>, and at the next start <c>update.installed</c> or
/// <c>update.rolled-back</c>. A name or a count each, never a path: the launcher writes no log, so its journal is turned into
/// these lines here.</para>
/// </remarks>
public sealed class InstallUpdater(
    // The install's root, or null for a workspace build, which never updates this way.
    string? install,
    // The Daoris home, where the request is read and written.
    string home,
    MachineLog log,
    Func<UpdateWork> work,
    // Starts the launcher with `--update --after <pid>`: false when it could not be started.
    Func<bool> relaunch,
    // Closes the application as the person's close does: each driven session recorded stopped and interrupted (D104).
    Action close,
    IEventBus? bus = null,
    Func<DateTimeOffset>? clock = null,
    // This process, which a confirmed swap records and the launcher waits on.
    int? pid = null) : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _poke = new(0);

    private UpdateState _state = new(UpdateStates.None, null, null, 0, 0, null, null, null);
    private UpdateOutcome? _outcome;
    private UpdateOutcome? _last;
    private readonly Dictionary<string, UpdateProblem> _refused = new(StringComparer.Ordinal);
    private string? _seen;
    private string? _drainedFor;
    private DateTimeOffset? _drainSince;
    private bool _applying;
    private Task? _loop;

    /// <summary>How often the loop looks when nothing pokes it.</summary>
    public TimeSpan Pace { get; init; } = TimeSpan.FromSeconds(2);

    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();

    /// <summary>Whether the loop's starts are held for the update: the driver watch asks at every look (D139 §2).</summary>
    public bool Draining
    {
        get
        {
            lock (_gate) return _state.State is UpdateStates.Draining or UpdateStates.Applying;
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
    /// At the application's start (D139 §5, §6): a swap waiting for this start is confirmed, and said once as installed; a
    /// swap the launcher rolled back or refused, or one it finished without a word from the application, is said once and
    /// marked told.
    /// </summary>
    public void Started()
    {
        if (install is null) return;
        UpdateOutcome? outcome = null;
        try
        {
            if (StagedBuild.Confirm(install, pid ?? Environment.ProcessId) && StagedBuild.ReadJournal(install) is { } confirmed)
            {
                outcome = Outcome(confirmed with { Phase = SwapPhase.Installed });
                log.Info("update.installed", ("build", confirmed.Id), ("version", confirmed.Version), ("confirmed", true));
            }
            else if (StagedBuild.ReadJournal(install) is { Told: false } last
                     && last.Phase is SwapPhase.Installed or SwapPhase.RolledBack or SwapPhase.Refused)
            {
                outcome = Outcome(last);
                if (last.Phase == SwapPhase.Installed)
                {
                    log.Info("update.installed", ("build", last.Id), ("version", last.Version), ("confirmed", last.Confirmed ?? false));
                }
                else
                {
                    log.Warn($"update.{last.Phase}", ("build", last.Id), ("reason", last.Reason));
                }

                StagedBuild.Tell(install);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A journal that cannot be read or written is no outcome to say; the swap's own record stays as it was.
        }

        lock (_gate) _outcome = outcome;
        Look();
    }

    /// <summary>One look: what is staged, what holds for it, what runs — and the update applied when its moment has come.</summary>
    public UpdateState Look()
    {
        if (install is null) return Publish(new UpdateState(UpdateStates.None, null, null, 0, 0, null, Outcome(), Last()));

        lock (_gate)
        {
            // Still here after asking: the close did not take. Once the launcher, finding this application running, has
            // moved the build aside, there is nothing left to drain for, and starts are held no longer.
            if (_applying && StagedBuild.IsStaged(install)) return _state;
            _applying = false;
        }

        // The build a finished swap left behind (UPDATE1): the launcher cannot delete its own running launcher inside it.
        StagedBuild.ClearPrevious(install);

        // Read at every look, as the terminal reads it at every `update` (UPDATE1d): a dismissal puts away the outcome only.
        var last = Ended(StagedBuild.ReadJournal(install));
        lock (_gate) _last = last;

        var manifest = StagedBuild.Read(install, out var unread);
        if (manifest is null && unread is null)
        {
            lock (_gate)
            {
                _drainedFor = null;
                _drainSince = null;
            }

            return Publish(new UpdateState(UpdateStates.None, null, null, 0, 0, null, Outcome(), Last()));
        }

        var id = manifest?.Id ?? $"unread:{unread!.Code}";
        var staged = manifest is null ? null : new UpdateBuild(manifest.Id, manifest.Version, manifest.Commit, manifest.At);
        UpdateProblem? known;
        lock (_gate) known = _refused.GetValueOrDefault(id);
        if (known is not null) return Publish(new UpdateState(UpdateStates.Refused, staged, null, 0, 0, known, Outcome(), Last()));

        if (unread is not null) return Refuse(id, staged, null, new UpdateProblem(unread.Code, unread.Sentence));

        if (!string.Equals(_seen, id, StringComparison.Ordinal))
        {
            _seen = id;
            log.Info("update.staged", ("build", manifest!.Id), ("version", manifest.Version));
        }

        var mode = InstallUpdate.ModeFor(InstallUpdate.Read(home), manifest!.Id);
        if (mode == UpdateMode.NotNow)
        {
            lock (_gate)
            {
                _drainedFor = null;
                _drainSince = null;
            }

            return Publish(new UpdateState(UpdateStates.Waiting, staged, mode, 0, 0, null, Outcome(), Last()));
        }

        var now = work();
        if (mode == UpdateMode.Now) return Apply(id, staged, mode, now, "now");

        lock (_gate)
        {
            if (!string.Equals(_drainedFor, id, StringComparison.Ordinal))
            {
                _drainedFor = id;
                _drainSince = Now;
                log.Info("update.draining", ("build", id), ("driven", now.Driven), ("turns", now.Turns));
            }
        }

        return InstallUpdate.Idle(now.Driven, now.Turns)
            ? Apply(id, staged, mode, now, "idle")
            : Publish(new UpdateState(UpdateStates.Draining, staged, mode, now.Driven, now.Turns, null, Outcome(), Last()));
    }

    /// <summary>
    /// The person's word, from the screen (D139 §3), written to the request the terminal writes, for the build staged now,
    /// and looked at at once — so *Update now* applies before this answers.
    /// </summary>
    /// <exception cref="Shenora.Core.Ipc.ShenoraException">
    /// <see cref="Refusals.UpdateModeUnknown"/> for a mode this build does not take; <see cref="Refusals.UpdateNothingStaged"/>
    /// when there is no install or nothing staged to say it of.
    /// </exception>
    public UpdateState Say(string? mode, string door)
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
        return Look();
    }

    /// <summary>The outcome the banner shows, said and put away; the last swap stays, as the journal keeps it (UPDATE1d).</summary>
    public UpdateState Dismiss()
    {
        lock (_gate) _outcome = null;
        return Look();
    }

    /// <summary>Look at the loop's pace, and at once when poked, until stopped.</summary>
    public void Start()
    {
        _loop ??= Task.Run(async () =>
        {
            var ct = _stopping.Token;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Look();
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    log.Warn("update.look", ("message", error.Message));
                }

                try
                {
                    await _poke.WaitAsync(Pace, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>Look now, rather than at the next pace.</summary>
    public void Poke() => _poke.Release();

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Cancellation, as it should surface.
        }

        _stopping.Dispose();
        _poke.Dispose();
    }

    private UpdateState Apply(string id, UpdateBuild? staged, string mode, UpdateWork now, string by)
    {
        // Checked by the application before it closes for it (D139 §4); the launcher checks again before it moves a file.
        if (StagedBuild.Check(install!) is { } problem)
        {
            return Refuse(id, staged, mode, new UpdateProblem(problem.Code, problem.Sentence));
        }

        double waited;
        lock (_gate)
        {
            _applying = true;
            waited = _drainSince is { } since ? Math.Max(0, (Now - since).TotalSeconds) : 0;
        }

        var applying = Publish(new UpdateState(UpdateStates.Applying, staged, mode, now.Driven, now.Turns, null, Outcome(), Last()));
        if (!relaunch())
        {
            lock (_gate) _applying = false;
            return Refuse(id, staged, mode, new UpdateProblem(
                "launcher", "the launcher at the install's root would not start, so the application stays open and nothing was replaced."));
        }

        log.Info("update.applying", ("build", id), ("version", staged?.Version), ("by", by), ("waitedSeconds", (long)waited));
        // Said and done with: a word that outlived its build would hold for the next one (InstallUpdate.ModeFor).
        InstallUpdate.Clear(home);
        close();
        return applying;
    }

    private UpdateState Refuse(string id, UpdateBuild? staged, string? mode, UpdateProblem problem)
    {
        lock (_gate)
        {
            _refused[id] = problem;
            _drainedFor = null;
            _drainSince = null;
        }

        log.Warn("update.refused", ("build", staged?.Id), ("reason", problem.Code));
        return Publish(new UpdateState(UpdateStates.Refused, staged, mode, 0, 0, problem, Outcome(), Last()));
    }

    private UpdateOutcome? Outcome()
    {
        lock (_gate) return _outcome;
    }

    private UpdateOutcome? Last()
    {
        lock (_gate) return _last;
    }

    private static UpdateOutcome Outcome(SwapRecord record) =>
        new(record.Phase, record.Id, record.Version, record.Commit, record.Reason, record.Detail);

    /// <summary>
    /// How the journal's swap ended, told or not (UPDATE1d): installed, rolled back or refused as written; confirmed by this
    /// start, installed, as <see cref="Started"/> says it before the launcher finishes; and none while one is under way or
    /// there is no journal, since the page words only an ending.
    /// </summary>
    private static UpdateOutcome? Ended(SwapRecord? record) => record?.Phase switch
    {
        SwapPhase.Installed or SwapPhase.RolledBack or SwapPhase.Refused => Outcome(record),
        SwapPhase.Confirmed => Outcome(record with { Phase = SwapPhase.Installed }),
        _ => null,
    };

    /// <summary>The state, kept, and told to the page when it changed.</summary>
    private UpdateState Publish(UpdateState state)
    {
        bool changed;
        lock (_gate)
        {
            changed = state != _state;
            _state = state;
        }

        if (changed && bus is not null) _ = bus.EmitAsync("DAORIS", "UPDATE_STATE", state);
        return state;
    }
}
