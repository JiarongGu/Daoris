using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// How a driven session's turn waits and ends: the judgement a turn that ends on its own background work is asked, the look a turn's
/// end takes of a quest that may have closed with uncommitted work, the bounds on a process that will not exit, and a run's capture
/// observed once the run gave up before awaiting it.
/// </summary>
internal static class SessionWaits
{
    /// <summary>How many times a went-on run is carried on: the person's words were its turn, so one more.</summary>
    public const int WentOnCarryBound = 1;

    /// <summary>Where a run's capture failed once the run is ended before awaiting it: the machine log's <c>where</c>.</summary>
    public const string AbandonedRun = "a session's run, ended as its record would not move to working";

    /// <summary>
    /// How long a given-up run's capture is waited for before it is left to finish on its own, still observed: its process is just
    /// ended, so its output closes at once, and a capture still open after this is stuck, not slow.
    /// </summary>
    public static readonly TimeSpan AbandonedWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long an agent whose session ended with its own background work still running is given to exit once its stdin closed, before
    /// its tree is ended. Its session has said all it will; the work is what can hold it.
    /// </summary>
    public static readonly TimeSpan LingerBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The judgement a turn that ends on the session's own background work is asked, one for a start and a resumed run: the quest read
    /// again, by the conclusion's own rule. Null for a resumed run whose quest is closed as the look planned it, which never waits. A
    /// start asks the ledger whether it took its quest; a resumed run carried on a take of this machine's, so it asks nothing.
    /// </summary>
    public static Func<CancellationToken, Task<bool>>? OnBackground(ServiceClient service, QuestView planned, string sessionId, bool resumed)
    {
        if (resumed && planned.Status is not ("Open" or "Taken")) return null;

        // Read again at every look while the session waits. A take once read stays this session's (its record keeps it), so the
        // sessions are read only until it is.
        var took = false;
        return async token =>
        {
            var now = await service.FindQuestAsync(planned.Id, token).ConfigureAwait(false);
            var status = now?.Status ?? "Open";
            took = took || (!resumed && status == "Taken" && await service.TookAsync(sessionId, token).ConfigureAwait(false));
            return Observation.Parks(status, planned.Awaits, now?.Awaits, resumed, took: took && status == "Taken");
        };
    }

    /// <summary>
    /// The look a turn's end takes of a quest that may have closed with uncommitted work in its tree: the quest read again, and, where
    /// it is done or submitted and the person has not stopped the session, its tree (<see cref="TurnCarries"/>). A resumed run whose
    /// quest is closed as the look planned it (a went-on run) is carried on once, not <see cref="TurnCarry.Bound"/> times, and not at
    /// all where it is an Accept's own carry-on (null: the Accept's count bounds it). A parked quest, an ask-and-wait, a stand-down and
    /// a failed exit are none of them done or submitted, so none is carried on.
    /// </summary>
    /// <param name="stopped">Whether the person (or the driver) stopped the session, read at each look and again where the words are written.</param>
    /// <param name="accepted">Whether the session's kept Accept is the cause of this resume, read as the run is built.</param>
    public static Func<CancellationToken, Task<TurnCarry>>? CarriesOn(
        string home, ServiceClient service, QuestView planned, string tree, bool resumed, Func<bool> stopped, bool accepted = false)
    {
        // A went-on run is carried on once, a turn's end twice; an Accept's carry-on is the Accept's, bounded by its own count, so the
        // run reads null for it and the session is never carried on by two counters.
        var wentOn = resumed && planned.Status is not ("Open" or "Taken");
        if (wentOn && accepted) return null;
        var limit = wentOn ? WentOnCarryBound : TurnCarry.Bound;
        return async token =>
        {
            if (stopped()) return TurnCarry.None;
            var now = await service.FindQuestAsync(planned.Id, token).ConfigureAwait(false);
            if (CommitAskers.Of(home, tree) != CommitAskers.TurnsEnd) return TurnCarry.None;
            return await TurnCarries.LookAsync(now is { Status: "Done" } || QuestResults.Submitted(now), stopped, tree, token, limit).ConfigureAwait(false);
        };
    }

    /// <summary>
    /// A run's capture once the run gave up before awaiting it, as when its record will not move to working: the process ended, then
    /// the capture observed, and a failure there written with its reason, to the machine log as an <c>error</c> naming its place and to
    /// the session's console. Left alone it reaches the finalizer, an unobserved task's AggregateException whose only place is <i>an
    /// unobserved task</i>, beside each session the double move fails.
    /// </summary>
    /// <remarks>
    /// Waited for within <see cref="AbandonedWait"/>, as the ordinary path awaits a capture once its process exited; past it the run
    /// goes on to say how it ended, and the capture is still observed whenever it finishes. Never throws.
    /// </remarks>
    /// <param name="exitRead">
    /// Told once the ended process's exit is read within <see cref="AbandonedWait"/>, before the capture is waited for: where its
    /// <c>end</c> is written. Never told past the bound.
    /// </param>
    public static async Task AbandonedAsync(
        Process? process, Task capture, string sessionId, MachineLog? log, SessionOutput? output, TimeSpan? within = null, Action? exitRead = null)
    {
        if (process is not null)
        {
            SessionProcesses.EndIfRunning(process);
            if (exitRead is not null && await ExitReadAsync(process, AbandonedWait).ConfigureAwait(false)) exitRead();
        }

        var observed = capture.ContinueWith(
            done =>
            {
                if (done.Exception is not { } failed) return;
                var error = failed.InnerExceptions.Count == 1 ? failed.InnerExceptions[0] : failed;
                log?.Failed(AbandonedRun, error);
                output?.Append(
                    sessionId,
                    $"— the run ended here, since its record would not move, failed as its output was read: {error.GetType().Name}: {error.Message}");
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            await observed.WaitAsync(within ?? AbandonedWait).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Still observed: the continuation above writes its failure whenever the capture ends.
        }
    }

    /// <summary>
    /// Exit code, or null when a pass's own minutes ran out and killed it. Either way the tree dies with it: a pass's bound and a
    /// driver shutdown both end the process, because an orphaned agent session working a quest nobody is observing is the one thing
    /// worse than a failed one.
    /// </summary>
    /// <remarks>
    /// 🔴 No working session is ended on time: a driven session, an answered park's resumed run, a native go-on and an intake wait for
    /// their exit with no bound, and the watcher looks in on one gone quiet. Only a pass with minutes of its own (a helper's, a
    /// reviewer's, a partner's, a brainstorm's turn) passes them, and the tests' one backstop (<see cref="DriverConfig.TestBound"/>)
    /// rides the same parameter.
    /// </remarks>
    /// <param name="bound">A pass's own minutes; null bounds nothing but the tests' backstop.</param>
    /// <param name="timingOut">
    /// Told the bound, in minutes, when the bound ends the process, before it is killed: what lets the protocol door say the stream's
    /// end that follows as the bound's. Never told for a shutdown.
    /// </param>
    /// <param name="quiet">
    /// The watcher's end of the run: a headless <c>--quiet-end</c>, its own cause, never a registry stop. Its exit is null as a bound's
    /// is, and the conclusion reads the watcher's minutes.
    /// </param>
    /// <param name="quietEnding">Told before the watcher's kill, as <paramref name="timingOut"/> is before a bound's.</param>
    public static async Task<int?> ExitAsync(
        Process process, int? bound, CancellationToken ct, Action<int>? timingOut = null, CancellationToken quiet = default,
        Action? quietEnding = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, quiet);
        if (bound is { } within) timeout.CancelAfter(TimeSpan.FromMinutes(within));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            // Said before the kill, since the stream's end can reach the door's reader before anything after it here runs.
            if (!ct.IsCancellationRequested)
            {
                if (quiet.IsCancellationRequested) quietEnding?.Invoke();
                else timingOut?.Invoke(bound ?? 0);
            }
            OwnedProcesses.End(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (ct.IsCancellationRequested) throw; // shutdown: the caller records "stopped"
            return null; // timeout: the caller records "failed"
        }
    }

    /// <summary>
    /// Wait up to <paramref name="bound"/> for an agent to exit; past it, end its process tree, saying so to the run first. True where
    /// its tree is ended here.
    /// </summary>
    public static async Task<bool> EndedLingeringAsync(Process process, TimeSpan bound, AcpEnding? ending)
    {
        using var within = new CancellationTokenSource(bound);
        try
        {
            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            ending?.Lingering();
            SessionProcesses.EndIfRunning(process);
            return true;
        }
    }

    /// <summary>Whether <paramref name="process"/>'s exit is read within <paramref name="within"/>; one that cannot say has not exited.</summary>
    public static async Task<bool> ExitReadAsync(Process process, TimeSpan within)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(within).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (error is TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
