using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>The shell's close reason, shared by its conversation and driven processes.</summary>
public sealed record ShellClose(string? Build)
{
    public static ShellClose Person { get; } = new((string?)null);
    public static ShellClose Update(string build) => new(build);
    public const string Words = "Daoris restarted while you were working. Go on where you stopped.";
    public static Noted Closed(string build) => Noted.Of(
        EndNoteCodes.UpdateClosed, "Daoris stopped for an update.", ("build", build));
}

/// <summary>
/// The live session processes, by session id — the one piece of session state that never reaches the
/// service: the record travels, the process handle stays with whoever spawned it. This
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
    public ShellClose Close { get; set; } = ShellClose.Person;

    /// <summary>Told the session and how many processes were still running in its job when its tracking ended and were ended with it.</summary>
    public Action<string, int>? Reaped { get; set; }

    private sealed class Entry
    {
        public required Process Process;
        public bool StopRequested;

        /// <summary>Why the driver ended it, when the driver did — null when the person did. With its code.</summary>
        public Noted? Reason;

        /// <summary>What the person's stop says on the record, where the door that stopped it said more (a pause).</summary>
        public Noted? Note;

        /// <summary>Why a person's line is refused here, in the driver's words — null for a conversation.</summary>
        public string? RefusesInput;

        /// <summary>Whether it is handed a server that drives Daoris's browser.</summary>
        public bool DrivesBrowser;

        /// <summary>
        /// The job it joined as it is tracked, held here too so the watcher reads its CPU and state E ends
        /// what holds its capture; the disposer <see cref="Track"/> returns still owns it and closes it.
        /// </summary>
        public ProcessJob? Job;

        /// <summary>
        /// Started through the launcher: its job is the launcher's own, opened by name the first time it is asked for,
        /// never held over the start, which would race the launcher's joins.
        /// </summary>
        public bool Launched;

        /// <summary>How many delegate keys are drawn as it is tracked: its session's last process ending forgets those.</summary>
        public long Drawn;
    }

    /// <summary>The delegate keys of the sessions started here, each forgotten as its session's last process here ends.</summary>
    public SessionDelegates Delegates { get; } = new();

    private readonly object _gate = new();

    // Taken before the gate, so a session's marker follows its entries in the order they changed.
    private readonly object _marks = new();

    // An entry per process, oldest first: a session's second start beside a live process is kept beside it, and the one the
    // reads answer is Answered's. A list is never empty.
    private readonly Dictionary<string, List<Entry>> _running = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether an entry's process has exited; one whose state cannot be read is taken as exited, as a replaced entry reads.</summary>
    private static bool Exited(Entry entry)
    {
        try
        {
            return entry.Process.HasExited;
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// The entry a read of the session answers: the newest whose process still runs, so a start beside a live one answers
    /// as it does when it is the only one and the live one answers again once it is gone; where none runs, the newest.
    /// </summary>
    private static Entry Answered(List<Entry> held)
    {
        for (var at = held.Count - 1; at >= 0; at--)
        {
            if (held.Count == 1 || !Exited(held[at])) return held[at];
        }

        return held[^1];
    }

    /// <summary>The entry the session's reads answer, or null where none is held; under the gate.</summary>
    private Entry? Held(string sessionId) => _running.TryGetValue(sessionId, out var held) ? Answered(held) : null;

    /// <summary>
    /// Where the stop made of the session is read from: the newest entry a stop reached, so a run stopped reads its own stop
    /// as it exits, though a process still running beside it is what <see cref="Held"/> answers; none stopped, as <see cref="Held"/>.
    /// </summary>
    private Entry? Stopped(string sessionId)
    {
        if (!_running.TryGetValue(sessionId, out var held)) return null;
        return held.FindLast(entry => entry.StopRequested) ?? Answered(held);
    }
    private readonly Dictionary<string, IAgentInbox> _inboxes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where a driven session's held words stand, each time that changes — the page shows them.</summary>
    public event Action<string, IAgentQueue>? HeldChanged;

    /// <summary>
    /// Open a driven session's inbox: what the person tells it while it works, held for the
    /// protocol door to hand over. Here, beside the processes, because a session outlives the tick whose
    /// driver started it, and the page's routes reach this registry.
    /// </summary>
    public IAgentInbox OpenWordInbox(string sessionId)
    {
        var inbox = AgentCourse.Current.OpenInbox(queue => HeldChanged?.Invoke(sessionId, queue));
        inbox.OnClosing(
            () =>
            {
                lock (_gate)
                {
                    if (_inboxes.TryGetValue(sessionId, out var held) && ReferenceEquals(held, inbox)) _inboxes.Remove(sessionId);
                }
            },
            // The person's stop ends what is kept for the run with it; any other end leaves it on the record to go on.
            // The person's End and carry on keeps them, for the carry-on it starts.
            personStopped: () => StopMade(sessionId) is { Reason: null } stop && !Observation.IsQuietPressed(stop.Note?.Parts),
            left: words => KeptLeft?.Invoke(sessionId, words));
        lock (_gate) _inboxes[sessionId] = inbox;
        return inbox;
    }

    /// <summary>
    /// The words kept on a driven session's record for its run that stay there as its inbox closes under them: its door
    /// failed, or the shell shut down. They wait on the ended record for the session's go-on, and whoever shows the conversation
    /// says so under their ids.
    /// </summary>
    public event Action<string, IReadOnlyList<IAgentWord>>? KeptLeft;

    /// <summary>A running driven session's inbox, or null: one on the pipe door, a conversation, or none.</summary>
    public IAgentInbox? WordInboxOf(string sessionId)
    {
        lock (_gate) return _inboxes.GetValueOrDefault(sessionId);
    }

    private readonly Dictionary<string, Func<string, CancellationToken, Task<bool>>> _taskStops = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hold what stops a session's background work while its protocol-door session is open:
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
    /// Stop one task a session runs — true when its harness said it stopped. Null when no
    /// session held here runs tasks under that id: one on the pipe door, another machine's, or ended.
    /// </summary>
    public async Task<bool?> StopTaskAsync(string sessionId, string taskId, CancellationToken ct)
    {
        Func<string, CancellationToken, Task<bool>>? stop;
        lock (_gate) stop = _taskStops.GetValueOrDefault(sessionId);
        return stop is null ? null : await stop(taskId, ct).ConfigureAwait(false);
    }

    private readonly Dictionary<string, Func<DriverConfig, SetupSwap?>> _setups = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hold how a running driven session reads a change of its permission prompts in the driver's file, for
    /// as long as its run lives: here, beside its inbox, so a press on the page reaches the session the look that started it outlived.
    /// The run disposes the answer when it ends.
    /// </summary>
    /// <param name="swapIn">What the session's permission prompts changed to in a file as given, from what it started under; null for none.</param>
    public IDisposable OpenSetup(string sessionId, Func<DriverConfig, SetupSwap?> swapIn)
    {
        lock (_gate) _setups[sessionId] = swapIn;
        return new Closing(() =>
        {
            lock (_gate)
            {
                if (_setups.TryGetValue(sessionId, out var held) && held == swapIn) _setups.Remove(sessionId);
            }
        });
    }

    /// <summary>
    /// Each driven session running here whose permission prompts a change of the driver's file from <paramref name="before"/> to
    /// <paramref name="after"/> moves: its change against what it started under before, and after, either null for none.
    /// </summary>
    public IReadOnlyList<(string Session, SetupSwap? Before, SetupSwap? After)> SetupsMoved(DriverConfig before, DriverConfig after)
    {
        KeyValuePair<string, Func<DriverConfig, SetupSwap?>>[] held;
        lock (_gate) held = [.. _setups];
        return [.. held
            .Select(each => (Session: each.Key, Before: each.Value(before), After: each.Value(after)))
            .Where(each => each.Before != each.After)
            .OrderBy(each => each.Session, StringComparer.Ordinal)];
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
    /// Who is driving Daoris's browser: the running sessions handed a server that drives it, so a
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
            lock (_gate) return [.. _running.Where(entry => Answered(entry.Value).DrivesBrowser).Select(entry => entry.Key)];
        }
    }

    /// <summary>The conversations being opened here whose process is not started yet, which the person's stop reaches.</summary>
    public OpeningStops Openings { get; } = new();

    /// <summary>
    /// End a session's process, marking the end as the person's — or, with a <paramref name="reason"/>,
    /// as the driver's own, for that reason. True when there is one to stop; false is an answer too —
    /// the session already finished and its record says how, or another driver on this machine holds
    /// it, or nothing does and its record is an orphan (<see cref="Orphans"/>). The person's stop of one being opened here before its
    /// process exists ends its opening (<see cref="Openings"/>).
    /// </summary>
    /// <param name="note">
    /// For the person's stop, what its record says in place of the plain one: a pause's stop names the pause.
    /// Still the person's stop, which nothing decides from.
    /// </param>
    public bool Stop(string sessionId, Noted? reason = null, Noted? note = null)
    {
        Entry? entry;
        lock (_gate)
        {
            if ((entry = Held(sessionId)) is not null)
            {
                entry.StopRequested = true;
                entry.Reason ??= reason;
                entry.Note ??= note;
            }
        }

        if (entry is null) return reason is null && Openings.Stop(sessionId);

        try
        {
            OwnedProcesses.End(entry.Process);
        }
        catch (Exception error) when (Ending(error))
        {
            // Exited between the lookup and the kill, or a process in its tree is already ending — the
            // flag still marks whose decision the end is.
        }

        // Then its job: what the tree's walk cannot reach, a process whose parent had already exited, is still in it, and
        // a launcher's own job no longer ends with the launcher once the driver holds it.
        ProcessJob? job;
        lock (_gate) job = JobOf(entry);
        job?.Terminate();
        return true;
    }

    /// <summary>
    /// Whether a kill's failure only says the tree is already going: the root exited first, or a process in
    /// the tree refused access mid-exit. A whole-tree kill reports those inside an <see cref="AggregateException"/>,
    /// which a catch of the single types would let through a person's stop and a chat's cleanup.
    /// </summary>
    internal static bool Ending(Exception error) => error switch
    {
        InvalidOperationException or System.ComponentModel.Win32Exception => true,
        AggregateException many => many.Flatten().InnerExceptions.All(Ending),
        _ => false,
    };

    /// <summary>
    /// End a spawned harness's process tree if it is still running — the reaper every spawn site holds
    /// from the moment its process started.
    /// </summary>
    /// <remarks>
    /// A failure between the spawn and the wait — the record refusing to move to `working` (a stop
    /// pressed during `starting` does exactly that), the host restarting, a client timeout — would
    /// conclude the record and leave the agent working the quest untracked, unmarked, in a tree whose
    /// lock had just been freed for the next session. On the ordinary path the process has already
    /// exited here and this does nothing.
    /// </remarks>
    public static void EndIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited) OwnedProcesses.End(process);
        }
        catch (Exception error) when (Ending(error))
        {
            // Exited, never associated, or access to the tree refused mid-exit — either way nothing is left
            // running that this could end.
        }
    }

    /// <summary>Whether this session's end is asked for — read by the driver before it concludes.</summary>
    public bool WasStopRequested(string sessionId)
    {
        lock (_gate) return Stopped(sessionId) is { StopRequested: true };
    }

    /// <summary>Why the driver ended this session itself — null when nobody did, or the person did.</summary>
    public Noted? StopReason(string sessionId)
    {
        lock (_gate) return Stopped(sessionId)?.Reason;
    }

    /// <summary>What the person's stop of this session says on its record, where its door said more; null for the plain stop.</summary>
    public Noted? StopNote(string sessionId)
    {
        lock (_gate) return Stopped(sessionId)?.Note;
    }

    /// <summary>
    /// The stop made of this session, read whole: the driver's reason or the person's note, as the first stop set them;
    /// null where nobody stopped it, or nothing here runs it.
    /// </summary>
    public SessionStop? StopMade(string sessionId)
    {
        lock (_gate)
        {
            return Stopped(sessionId) is { StopRequested: true } entry ?new SessionStop(entry.Reason, entry.Note) : null;
        }
    }

    /// <summary>
    /// Why a person's line is refused for this session, in the driver's own words — null when it takes
    /// input, or is not running here at all.
    /// </summary>
    public string? RefusesInput(string sessionId)
    {
        lock (_gate) return Held(sessionId)?.RefusesInput;
    }

    /// <summary>
    /// Send a person's message to a chat session — one line into the harness's stdin.
    /// </summary>
    /// <remarks>
    /// False for a session that has ended, and for one that takes no input. 🔴 That second answer is
    /// a POLICY, held before anything is written, because the structural one stopped being enough at
    /// A pipe-door driven session is spawned with no input stream, but a protocol-door session's
    /// stdin is open — the driver writes the protocol's frames into it — and a person's line
    /// written there lands in the middle of the JSON-RPC stream. Everything the driver starts is such
    /// a session: an intake and a driven quest session, each one turn. Only a
    /// conversation takes turns with a person.
    /// </remarks>
    public bool Send(string sessionId, string message)
    {
        Entry? entry;
        lock (_gate)
        {
            if ((entry = Held(sessionId)) is null || entry.RefusesInput is not null) return false;
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
    /// says and exits on its own, which is a `completed` record. `Stop` is the other verb, and
    /// it means something different — the person cut it off. Refused for a session that takes no
    /// input: on the protocol door, closing that stream would end the protocol's turn, not a chat.
    /// </remarks>
    public bool CloseInput(string sessionId)
    {
        Entry? entry;
        lock (_gate)
        {
            if ((entry = Held(sessionId)) is null || entry.RefusesInput is not null) return false;
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

    /// <summary>
    /// Register a live process. Dispose the handle when the session concludes. A session's process still running when another is
    /// tracked stays tracked beside it, and each handle's end takes only its own entry and, where none runs after, the marker;
    /// a process that has exited is replaced.
    /// </summary>
    /// <param name="refusesInput">
    /// Why this session takes no person's line, in the driver's words — null for a conversation, the
    /// one kind that takes turns with a person.
    /// </param>
    /// <param name="drivesBrowser">Whether it is handed a server that drives Daoris's browser.</param>
    /// <remarks>
    /// 🔴 The process joins a job as it is tracked, and disposing the handle ends whatever is
    /// still in it — a dev server a session started from a background shell, found running after its
    /// session had ended. Called straight after the spawn by every site, so the job is joined before
    /// the harness has had time to start anything.
    /// <para>A start made through the launcher is held by no job here: its harness is born in this process's agents' job
    /// and in the launcher's own, which ends its tree as the launcher ends; and a job taken over the launcher now could win the race with
    /// its joins, and leave it unable to join the agents' job other agents hold (<see cref="PieceLauncher"/>). Its job is the launcher's own,
    /// opened by its name once the launcher has made it (<see cref="JobOf"/>), and held from then as the job above is.</para>
    /// </remarks>
    public IDisposable Track(string sessionId, Process process, string? refusesInput = null, bool drivesBrowser = false)
    {
        var launched = ThroughLauncher(process);
        var job = launched ? null : ProcessJob.Hold(process);
        var entry = new Entry
        {
            Process = process, RefusesInput = refusesInput, DrivesBrowser = drivesBrowser, Job = job, Launched = launched, Drawn = Delegates.Drawn,
        };
        lock (_marks)
        {
            lock (_gate)
            {
                if (!_running.TryGetValue(sessionId, out var held)) _running[sessionId] = held = [];
                // A process that has exited is replaced, and one still running is kept beside this one. A stop pressed after the
                // exited one's end and before this one is tracked flagged that entry alone: it goes on to the one that replaces it.
                if (held.FindLast(each => each.StopRequested && Exited(each)) is { } stopped)
                {
                    entry.StopRequested = true;
                    entry.Reason = stopped.Reason;
                    entry.Note = stopped.Note;
                }

                held.RemoveAll(Exited);
                held.Add(entry);
            }

            Mark(sessionId, process);
        }

        return new Untrack(this, sessionId, entry);
    }

    /// <summary>
    /// The job a running session's process joined: what the watcher reads its CPU from, where its accounting holds
    /// (<see cref="ProcessJob.Holds"/>), and what state E and a stop end. Null where the session runs no process here.
    /// </summary>
    /// <remarks>
    /// 🔴 A start through the launcher: its job is the launcher's own (<see cref="PieceLauncher.OwnJobOf"/>), opened here the
    /// first time it is asked for and held, so the launcher's joins are long over and nothing races them. Until the launcher has made it
    /// the answer is null, and the watcher reads busy as unknown for that look; once held, the job outlives the launcher until the tracking
    /// lets go, as the job a direct start joins does.
    /// </remarks>
    public ProcessJob? JobOf(string sessionId)
    {
        lock (_gate) return Held(sessionId) is { } entry ? JobOf(entry) : null;
    }

    /// <summary>
    /// The running session's job, taken from its tracking: neither a stop nor the tracking's end closes it after this, so what
    /// its process leaves running stays in it until the taker ends it and waits for it to empty, then disposes it. Null where none is held.
    /// </summary>
    public ProcessJob? TakeJob(string sessionId)
    {
        lock (_gate)
        {
            if (Held(sessionId) is not { } entry) return null;
            var job = JobOf(entry);
            entry.Job = null;
            entry.Launched = false;
            return job;
        }
    }

    // An entry's own job, under the gate: a stop ends the one it reached, whichever process answers the session once that one has exited.
    private static ProcessJob? JobOf(Entry entry)
    {
        if (entry.Job is null && entry.Launched) entry.Job = LaunchedJob(entry.Process);
        return entry.Job;
    }

    // The launcher's own job, where it holds the launcher: a name the launcher would not take leaves none to read.
    private static ProcessJob? LaunchedJob(Process process)
    {
        try
        {
            return ProcessJob.OpenHolding(PieceLauncher.OwnJobOf(process.Id), process.Handle);
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
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
        if (RunsHere(sessionId)) return true;
        return MarkerOf(sessionId) is { } marker && File.Exists(marker) && MarkerAlive(marker);
    }

    /// <summary>
    /// Whether this registry runs the session's process: one its own stop reaches. A process another
    /// Daoris process of this machine runs is alive on this machine and not here.
    /// </summary>
    public bool RunsHere(string sessionId)
    {
        lock (_gate) return _running.ContainsKey(sessionId);
    }

    /// <summary>
    /// Whether any session process marked in <paramref name="markers"/> is alive on this machine: what a
    /// reader with no service to ask knows of the home's sessions. A folder that cannot be read, or a marker that cannot, counts as alive:
    /// "cannot tell" is never "none". An opener's mark is no process of a session's.
    /// </summary>
    public static bool AnyAlive(string? markers)
    {
        if (markers is null) return false;
        List<string> files;
        try
        {
            if (!Directory.Exists(markers)) return false;
            files = [.. Directory.EnumerateFiles(markers, "*.pid")];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        return files.Any(MarkerAlive);
    }

    /// <summary>Whether the process a marker names is alive, as the one it marked: its id and its start time.</summary>
    internal static bool MarkerAlive(string marker)
    {
        string[] fields;
        try
        {
            fields = AtomicFile.ReadText(marker).Trim().Split(' ');
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        if (fields.Length != 2 || !int.TryParse(fields[0], out var pid) || !long.TryParse(fields[1], out var started))
        {
            return false;
        }

        return IsRunning(pid, started);
    }

    /// <summary>Whether the process with this id is alive as the one that started at <paramref name="started"/> (UTC ticks): what a marker names.</summary>
    internal static bool IsRunning(int pid, long started)
    {
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

    /// <summary>
    /// Mark a record this process has just opened and is starting: until its spawn's <see cref="Track"/> replaces it, the
    /// record's marker names the process that opened it, with the word <c>opening</c>, so a loop's sweep tells a <c>queued</c> or
    /// <c>starting</c> record a live driver here is starting from one whose driver went (<see cref="OpenerWent"/>).
    /// </summary>
    /// <remarks>
    /// An opener's mark is no process of the session's: <see cref="AliveOnThisMachine"/> reads it as none, as a build before it
    /// does (its third field), so a word, a stop and the history's clearing judge the record as before. A start that comes to nothing
    /// ends its record itself, and the sweep reads a mark only for a record still <c>queued</c> or <c>starting</c>, so a mark left by
    /// one is read by nothing until the history's clearing removes it with the session.
    /// </remarks>
    public void MarkOpening(string sessionId)
    {
        using var self = Process.GetCurrentProcess();
        MarkOpening(sessionId, self);
    }

    /// <summary>The mark of <paramref name="opener"/>, another process: a test's driver that went.</summary>
    internal void MarkOpening(string sessionId, Process opener)
    {
        if (MarkerOf(sessionId) is not { } marker) return;
        try
        {
            var line = $"{opener.Id} {opener.StartTime.ToUniversalTime().Ticks} {OpeningWord}";
            Directory.CreateDirectory(markers!);
            AtomicFile.WriteText(marker, line);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            // Unmarked, the start is left as before this mark: only the person's stop ends it.
        }
    }

    /// <summary>The third field of an opener's mark.</summary>
    private const string OpeningWord = "opening";

    /// <summary>
    /// Whether the process that opened this record and started it has gone: its opener's mark is kept, no process of
    /// the session's is held here, and no process runs as the one it names. False where there is no such mark: a record opened
    /// before the mark, in the breath between its open and its mark, by another home, or whose spawn's marker replaced it; and where
    /// the mark cannot be read, since "cannot tell" must never become "claim it".
    /// </summary>
    public bool OpenerWent(string sessionId)
    {
        lock (_gate)
        {
            if (_running.ContainsKey(sessionId)) return false;
        }

        if (MarkerOf(sessionId) is not { } marker || !File.Exists(marker)) return false;

        string[] fields;
        try
        {
            fields = AtomicFile.ReadText(marker).Trim().Split(' ');
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (fields.Length != 3 || fields[2] != OpeningWord || !int.TryParse(fields[0], out var pid) || !long.TryParse(fields[1], out var started))
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) >= TimeSpan.TicksPerSecond;
        }
        catch (ArgumentException)
        {
            // No process has that id now.
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Exited as we looked: gone. A start time not ours to read: taken as alive.
            return error is InvalidOperationException;
        }
    }

    public bool HasMarker(string sessionId) => MarkerOf(sessionId) is { } marker && File.Exists(marker);

    /// <summary>The marker's path, or null where this registry keeps none or the id could not be a file name.</summary>
    private string? MarkerOf(string sessionId) =>
        markers is null || !SafeNames.IsFolderId(sessionId)
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

    internal int SweepDeadMarkers(IReadOnlySet<string> active) => markers is null ? 0 : DeadMarkers.Sweep(markers, active);

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

    /// <summary>Whether <paramref name="process"/> is started as the launcher's; one whose start cannot be read is not.</summary>
    private static bool ThroughLauncher(Process process)
    {
        try
        {
            return PieceLauncher.Carries(process.StartInfo);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class Untrack(SessionProcesses owner, string sessionId, Entry entry) : IDisposable
    {
        public void Dispose()
        {
            ProcessJob? job;
            lock (owner._marks)
            {
                Entry? kept = null;
                var removed = false;
                lock (owner._gate)
                {
                    // Its own entry only. A session's other process is kept, answered and marked, and this end — a second
                    // start's, or a run's that a later one replaced — takes neither it nor its marker.
                    if (owner._running.TryGetValue(sessionId, out var held) && held.Remove(entry))
                    {
                        removed = true;
                        if (held.Count == 0)
                        {
                            owner._running.Remove(sessionId);
                            owner.Delegates.Forget(sessionId, entry.Drawn);
                        }
                        else
                        {
                            kept = Answered(held);
                        }
                    }

                    // Taken under the gate, so no JobOf opens a launcher's job again after this.
                    job = JobOf(entry);
                    entry.Job = null;
                    entry.Launched = false;
                }

                if (removed)
                {
                    // The marker names the process still running, as the one it left; none running, it goes as it always did.
                    if (kept is not null && !Exited(kept)) owner.Mark(sessionId, kept.Process);
                    else owner.Unmark(sessionId);
                }
            }

            // Last: whatever the session started and left running ends with it, the launcher's own job opened here where the driver
            // never did, so a launcher still running does not keep the job, and the tree, alive past its session.
            if (SessionTreeReap.End(job) is > 0 and var left) owner.Reaped?.Invoke(sessionId, left);
            job?.Dispose();
        }
    }
}
