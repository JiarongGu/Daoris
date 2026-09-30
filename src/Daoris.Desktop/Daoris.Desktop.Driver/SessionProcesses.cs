using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The live session processes, by session id — the one piece of session state that never reaches the
/// service (D46 §4): the record travels, the process handle stays with whoever spawned it. This
/// registry is what makes "stop that session" reachable from a control surface, across the per-tick
/// driver instances that actually own the spawning.
/// </summary>
/// <remarks>
/// 🔴 <b>"Not held here" is not "dead".</b> A terminal's driver or chat shares the home and holds its
/// own processes, so given a <c>markers</c> directory every tracked process leaves a marker there —
/// its id and start time — and any driver on this machine can ask whether a session's process is
/// alive (<see cref="AliveOnThisMachine"/>). A record claiming a process that no marker proves is an
/// orphan (<see cref="Orphans"/>); one this registry merely does not hold is not (2026-09-25).
/// </remarks>
/// <param name="markers">
/// Where the machine's process markers live — the home's <c>sessions/</c>, beside the transcripts.
/// Null keeps no marker, which is a test's registry, never a driver's.
/// </param>
public sealed class SessionProcesses(string? markers = null)
{
    private sealed class Entry
    {
        public required Process Process;
        public bool StopRequested;

        /// <summary>Why the driver ended it, when the driver did — null when the person did.</summary>
        public string? Reason;

        /// <summary>Why a person's line is refused here, in the driver's words — null for a conversation.</summary>
        public string? RefusesInput;

        /// <summary>Whether it was handed a server that drives Daoris's browser (BRW8).</summary>
        public bool DrivesBrowser;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DrivenInbox> _inboxes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where a driven session's held words stand, each time that changes (SESS3) — the page shows them.</summary>
    public event Action<string, ChatQueue>? HeldChanged;

    /// <summary>
    /// Open a driven session's inbox (SESS3): what the person tells it while it works, held for the
    /// protocol door to hand over. Here, beside the processes, because a session outlives the tick whose
    /// driver started it, and the page's routes reach this registry.
    /// </summary>
    public DrivenInbox OpenInbox(string sessionId)
    {
        var inbox = new DrivenInbox(queue => HeldChanged?.Invoke(sessionId, queue));
        inbox.OnClosing(() =>
        {
            lock (_gate)
            {
                if (_inboxes.TryGetValue(sessionId, out var held) && ReferenceEquals(held, inbox)) _inboxes.Remove(sessionId);
            }
        });
        lock (_gate) _inboxes[sessionId] = inbox;
        return inbox;
    }

    /// <summary>A running driven session's inbox, or null: one on the pipe door, a conversation, or none.</summary>
    public DrivenInbox? InboxOf(string sessionId)
    {
        lock (_gate) return _inboxes.GetValueOrDefault(sessionId);
    }

    private readonly Dictionary<string, Func<string, CancellationToken, Task<bool>>> _taskStops = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hold what stops a session's background work (CONSOLE3a) while its protocol-door session is open:
    /// here, beside its inbox, for the same reason. The door disposes the answer when the session ends.
    /// </summary>
    public IDisposable OpenTaskStops(string sessionId, Func<string, CancellationToken, Task<bool>> stop)
    {
        lock (_gate) _taskStops[sessionId] = stop;
        return new Closing(() =>
        {
            lock (_gate)
            {
                if (_taskStops.TryGetValue(sessionId, out var held) && held == stop) _taskStops.Remove(sessionId);
            }
        });
    }

    /// <summary>
    /// Stop one task a session runs (CONSOLE3a) — true when its harness said it stopped. Null when no
    /// session held here runs tasks under that id: one on the pipe door, another machine's, or ended.
    /// </summary>
    public async Task<bool?> StopTaskAsync(string sessionId, string taskId, CancellationToken ct)
    {
        Func<string, CancellationToken, Task<bool>>? stop;
        lock (_gate) stop = _taskStops.GetValueOrDefault(sessionId);
        return stop is null ? null : await stop(taskId, ct).ConfigureAwait(false);
    }

    private sealed class Closing(Action close) : IDisposable
    {
        private int _closed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0) close();
        }
    }

    /// <summary>Session ids with a live process, for whoever renders "what is running right now".</summary>
    public IReadOnlyList<string> Running
    {
        get
        {
            lock (_gate) return [.. _running.Keys];
        }
    }

    /// <summary>
    /// Who is driving Daoris's browser (BRW8): the running sessions handed a server that drives it, so a
    /// person knows whose hands are on the page before typing into it. From the handing to the end —
    /// whether the agent has used it yet is its own, and never reaches the driver.
    /// </summary>
    /// <remarks>
    /// Only this registry's: a terminal's driver shares the home and holds its own processes, and has no
    /// shell to hand it a browser, so every session that can drive this shell's browser is held here.
    /// </remarks>
    public IReadOnlyList<string> DrivingBrowser
    {
        get
        {
            lock (_gate) return [.. _running.Where(entry => entry.Value.DrivesBrowser).Select(entry => entry.Key)];
        }
    }

    /// <summary>
    /// End a session's process, marking the end as the person's — or, with a <paramref name="reason"/>,
    /// as the driver's own, for that reason. True when there was one to stop; false is an answer too —
    /// the session already finished and its record says how, or another driver on this machine holds
    /// it, or nothing does and its record is an orphan (<see cref="Orphans"/>).
    /// </summary>
    public bool Stop(string sessionId, string? reason = null)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_running.TryGetValue(sessionId, out entry)) return false;
            entry.StopRequested = true;
            entry.Reason ??= reason;
        }

        try
        {
            entry.Process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (Ending(error))
        {
            // Exited between the lookup and the kill, or a process in its tree was already ending — the
            // flag still marks whose decision the end was.
        }

        return true;
    }

    /// <summary>
    /// Whether a kill's failure only says the tree was already going: the root exited first, or a process in
    /// the tree refused access mid-exit. A whole-tree kill reports those inside an <see cref="AggregateException"/>,
    /// which a catch of the single types let through a person's stop and a chat's cleanup (MOD8, FIX-LOG).
    /// </summary>
    internal static bool Ending(Exception error) => error switch
    {
        InvalidOperationException or System.ComponentModel.Win32Exception => true,
        AggregateException many => many.Flatten().InnerExceptions.All(Ending),
        _ => false,
    };

    /// <summary>
    /// End a spawned harness's process tree if it is still running — the reaper every spawn site holds
    /// from the moment its process started (REV3).
    /// </summary>
    /// <remarks>
    /// A failure between the spawn and the wait — the record refusing to move to `working` (a stop
    /// pressed during `starting` does exactly that), the host restarting, a client timeout — used to
    /// conclude the record and leave the agent working the quest untracked, unmarked, in a tree whose
    /// lock had just been freed for the next session. On the ordinary path the process has already
    /// exited here and this does nothing.
    /// </remarks>
    public static void EndIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (Ending(error))
        {
            // Exited, never associated, or access to the tree refused mid-exit — either way nothing is left
            // running that this could end.
        }
    }

    /// <summary>Whether this session's end was asked for — read by the driver before it concludes.</summary>
    public bool WasStopRequested(string sessionId)
    {
        lock (_gate) return _running.TryGetValue(sessionId, out var entry) && entry.StopRequested;
    }

    /// <summary>Why the driver ended this session itself — null when nobody did, or the person did.</summary>
    public string? StopReason(string sessionId)
    {
        lock (_gate) return _running.TryGetValue(sessionId, out var entry) ? entry.Reason : null;
    }

    /// <summary>
    /// Why a person's line is refused for this session, in the driver's own words — null when it takes
    /// input, or is not running here at all.
    /// </summary>
    public string? RefusesInput(string sessionId)
    {
        lock (_gate) return _running.TryGetValue(sessionId, out var entry) ? entry.RefusesInput : null;
    }

    /// <summary>
    /// Send a person's message to a chat session (D49 §3) — one line into the harness's stdin.
    /// </summary>
    /// <remarks>
    /// False for a session that has ended, and for one that takes no input. 🔴 That second answer is
    /// a POLICY, held before anything is written, because the structural one stopped being enough at
    /// ACP1: a pipe-door driven session is spawned with no input stream, but a protocol-door session's
    /// stdin is open — the driver writes the protocol's frames into it (D53) — and a person's line
    /// written there lands in the middle of the JSON-RPC stream. Everything the driver starts is such
    /// a session: an intake (INT4h) and a driven quest session (INT4i), each one turn. Only a
    /// conversation takes turns with a person.
    /// </remarks>
    public bool Send(string sessionId, string message)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_running.TryGetValue(sessionId, out entry) || entry.RefusesInput is not null) return false;
        }

        try
        {
            var input = entry.Process.StandardInput;
            lock (entry)
            {
                input.WriteLine(message);
                input.Flush();
            }

            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or ObjectDisposedException)
        {
            // Exited between the lookup and the write, or spawned with no input stream. Either way the
            // message went nowhere, and the caller is told rather than left to assume it landed.
            return false;
        }
    }

    /// <summary>
    /// Close a chat's input — the person has finished talking, and the harness should wind up.
    /// </summary>
    /// <remarks>
    /// Ending the conversation rather than killing it: a harness given end-of-input finishes what it
    /// was saying and exits on its own, which is a `completed` record. `Stop` is the other verb, and
    /// it means something different — the person cut it off. Refused for a session that takes no
    /// input: on the protocol door, closing that stream would end the protocol's turn, not a chat.
    /// </remarks>
    public bool CloseInput(string sessionId)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_running.TryGetValue(sessionId, out entry) || entry.RefusesInput is not null) return false;
        }

        try
        {
            entry.Process.StandardInput.Close();
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Register a live process. Dispose the handle when the session concludes.</summary>
    /// <param name="refusesInput">
    /// Why this session takes no person's line, in the driver's words — null for a conversation, the
    /// one kind that takes turns with a person.
    /// </param>
    /// <param name="drivesBrowser">Whether it was handed a server that drives Daoris's browser (BRW8).</param>
    /// <remarks>
    /// 🔴 The process joins a job as it is tracked (ORPHAN1), and disposing the handle ends whatever is
    /// still in it — a dev server a session started from a background shell, found running after its
    /// session had ended. Called straight after the spawn by every site, so the job is joined before
    /// the harness has had time to start anything.
    /// </remarks>
    public IDisposable Track(string sessionId, Process process, string? refusesInput = null, bool drivesBrowser = false)
    {
        var job = ProcessJob.Hold(process);
        lock (_gate) _running[sessionId] = new Entry { Process = process, RefusesInput = refusesInput, DrivesBrowser = drivesBrowser };
        Mark(sessionId, process);
        return new Untrack(this, sessionId, job);
    }

    /// <summary>
    /// Whether a process for this session is alive on this machine: held here, or marked by any driver
    /// that shares the home and still running as the process it marked.
    /// </summary>
    /// <remarks>
    /// The start time is what tells a live marker from a crash's leftover whose pid the machine has
    /// since handed to something else. A process whose start time cannot be read is taken as alive:
    /// "cannot tell" must never become "claim it".
    /// </remarks>
    public bool AliveOnThisMachine(string sessionId)
    {
        lock (_gate)
        {
            if (_running.ContainsKey(sessionId)) return true;
        }

        if (MarkerOf(sessionId) is not { } marker || !File.Exists(marker)) return false;

        string[] fields;
        try
        {
            fields = File.ReadAllText(marker).Trim().Split(' ');
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        if (fields.Length != 2 || !int.TryParse(fields[0], out var pid) || !long.TryParse(fields[1], out var started))
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond;
        }
        catch (ArgumentException)
        {
            // No process has that id now.
            return false;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Exited as we looked, or its start time is not ours to read: not a claim either way.
            return error is System.ComponentModel.Win32Exception;
        }
    }

    /// <summary>The marker's path, or null where this registry keeps none or the id could not be a file name.</summary>
    private string? MarkerOf(string sessionId) =>
        markers is null || sessionId.Length == 0 || sessionId.IndexOfAny(['/', '\\', ':']) >= 0 || sessionId.Contains("..")
            ? null
            : Path.Combine(markers, sessionId + ".pid");

    /// <summary>Written beside, then renamed (atomic); best-effort, since the process runs either way.</summary>
    private void Mark(string sessionId, Process process)
    {
        if (MarkerOf(sessionId) is not { } marker) return;
        try
        {
            var line = $"{process.Id} {process.StartTime.ToUniversalTime().Ticks}";
            Directory.CreateDirectory(markers!);
            AtomicFile.WriteText(marker, line);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            // Unmarked, this process is invisible to another driver's sweep — never to this one's own
            // registry, which is what stops and conversations use.
        }
    }

    private void Unmark(string sessionId)
    {
        if (MarkerOf(sessionId) is not { } marker) return;
        try
        {
            File.Delete(marker);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A marker left behind names a dead process, which the start time already tells apart.
        }
    }

    private sealed class Untrack(SessionProcesses owner, string sessionId, ProcessJob job) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate) owner._running.Remove(sessionId);
            owner.Unmark(sessionId);
            // Last: whatever the session started and left running ends with it (ORPHAN1).
            job.Dispose();
        }
    }
}
