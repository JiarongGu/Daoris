using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The sessions a driver started and has not yet reported ending (DEV3, D115 §3.1). A look starts what may
/// start and returns; the runs go on here, and each one's ending waits here for the next look's report.
/// </summary>
/// <remarks>
/// <para><b>Shared across looks.</b> The watch builds one driver per look, so the runs outlive the driver
/// that started them: the watch owns this and hands it to each. A driver nobody hands one to keeps its
/// own, which is what a one-look run and a gate's run-until-idle want.</para>
///
/// <para><b>A run that never opened a record is its own look's to report</b>: a hold, a refusal and an error
/// before the record opened are what that look says about the quest. Only a run whose record opened is kept
/// here, since only it outlives the look; or, under the watch (PARALLEL1b), one its look let go of while it
/// still prepared, reserved here until it opens or comes to nothing, which a later look says
/// (<see cref="Carry"/>).</para>
///
/// <para><b>The work beside the looks is kept here too</b> (PARALLEL1b, D181 point 3): the remote sync's
/// pass, the landing's, the evaluation runtime's (PARALLEL1h), and each topic's judges, one judgement per
/// subject and each topic bounded, so a closing loop waits for them as it waits for its sessions.</para>
///
/// <para><b>Nothing here decides how a session ends.</b> Each run concludes its own record, on its exit code
/// and its quest, as it always has (D46 §4). This keeps the runs and hands their endings on.</para>
/// </remarks>
public sealed class RunningSessions
{
    private readonly object _gate = new();
    private readonly List<StartRun> _ended = [];
    private int _running;

    // Completed and replaced whenever a run finishes: what every wait here waits on.
    private TaskCompletionSource _moved = NewSignal();

    /// <summary>
    /// Which quest each running session holds, by quest id: what the lost-claim stop checks this machine's
    /// claim on (D68 §5). An entry lives exactly as long as its session's process.
    /// </summary>
    internal ConcurrentDictionary<string, string> Live { get; } = new(StringComparer.Ordinal);

    // The records a run of this loop takes up with the person's words, from the look that began it until it ends (ANSWER2).
    private readonly HashSet<string> _goingOn = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Claim a record the person's words wait on for the one run that takes it up (ANSWER2, D131 §3: one record, one harness
    /// conversation): false where a run of this loop already has it. That run is told it opened before its process starts and
    /// moves the record to working, so a look in between still reads the record waiting and would begin a second run on it,
    /// whose move to working the ledger refuses, failing the session the first run is resuming.
    /// </summary>
    internal bool TryGoOn(string session)
    {
        lock (_gate) return _goingOn.Add(session);
    }

    /// <summary>The run that took the record up has ended, however it ended: words said to it later go on in a run of their own.</summary>
    internal void WentOn(string session)
    {
        lock (_gate) _goingOn.Remove(session);
    }

    // The chains' work whose second opinion a run of this loop asks or delivers, from the look that began it until it ends (XAGENT1f).
    private readonly HashSet<string> _opinions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Claim a chain's work in a repository for the one run that asks or delivers its second opinion (XAGENT1f): false where a run
    /// of this loop already has it. A pass and a recheck run for minutes, and a look in between would ask the same one again.
    /// </summary>
    internal bool TryOpinion(string key)
    {
        lock (_gate) return _opinions.Add(key);
    }

    /// <summary>The run that held the claim has ended, however it ended.</summary>
    internal void OpinionDone(string key)
    {
        lock (_gate) _opinions.Remove(key);
    }

    /// <summary>
    /// Whether a run of this loop holds <paramref name="key"/>'s claim now (PARALLEL1g): an opinion's judgment beside the looks reads it
    /// before the gate, and finds no pass owed while one is in flight, whose end the next judgment reads.
    /// </summary>
    internal bool OpinionHeld(string key)
    {
        lock (_gate) return _opinions.Contains(key);
    }

    /// <summary>The facts the judges beside the looks keep for the plan (PARALLEL1g, D181 point 3), shared across the looks as the runs are.</summary>
    internal LookFacts Facts { get; } = new();

    // A judgment beside the looks moved a fact the plan reads since the last look took it (PARALLEL1g).
    private bool _factsMoved;

    /// <summary>
    /// A judgment beside the looks moved a fact the plan reads (PARALLEL1g): a chain it held or let go, an ask it read. Wakes a wait as an
    /// ending does, so the plan reads it at once; a judgment that moved nothing wakes nothing, so a hold never spins the loop.
    /// </summary>
    internal void FactsMoved()
    {
        TaskCompletionSource moved;
        lock (_gate)
        {
            _factsMoved = true;
            moved = _moved;
            _moved = NewSignal();
        }

        moved.TrySetResult();
    }

    /// <summary>Taken by the look that reads the facts, before it reads them: one that moves after is the next look's.</summary>
    internal void TakeFactsMoved()
    {
        lock (_gate) _factsMoved = false;
    }

    // The starts of this loop's not yet open (PARALLEL1b), each reserved by the look that began it until its record opens or it comes
    // to nothing; and what a start came to after the look that began it let go of it, for a later look's report.
    private readonly List<StartReservation> _reserved = [];
    private readonly List<StartCame> _came = [];

    /// <summary>The starts of this loop's reserved and not yet open (PARALLEL1b), as a look hands them to the planner.</summary>
    internal IReadOnlyList<StartReservation> Reserved
    {
        get { lock (_gate) return [.. _reserved]; }
    }

    /// <summary>
    /// Reserve a start (PARALLEL1b, D181 point 3): its quest or ask, and the tree it goes back into, held from the look that plans it
    /// until its record opens or it comes to nothing, so a look meanwhile neither plans it again nor gives its place under the cap
    /// (<see cref="Snapshot.Starting"/>). False where a start of this loop's already holds the quest, the ask or the tree.
    /// </summary>
    internal bool TryReserve(StartReservation start)
    {
        lock (_gate)
        {
            if (_reserved.Any(held => (start.Quest is not null && string.Equals(held.Quest, start.Quest, StringComparison.OrdinalIgnoreCase))
                                      || (start.Ask is not null && string.Equals(held.Ask, start.Ask, StringComparison.OrdinalIgnoreCase))
                                      || Planner.SameTree(held.Tree, start.Tree)))
            {
                return false;
            }

            _reserved.Add(start);
            return true;
        }
    }

    /// <summary>Let a reservation go: its record is open, so the ledger holds it, or it came to nothing. Once is enough; again is nothing.</summary>
    internal void Release(StartReservation start)
    {
        lock (_gate) _reserved.Remove(start);
    }

    /// <summary>
    /// Keep what a start a look let go of comes to (PARALLEL1b): its record open, a hold, a refusal, or a failure before its spawn, for
    /// the next look's report, which says it as the look that began it would have. An opened one wakes a wait, as an ending does, so
    /// its run is bound and its progress said at once; a hold waits for whatever wakes the next look, so a hold never spins the loop.
    /// </summary>
    internal void Carry(StartBegun started, Task<StartCame> start) =>
        _ = start.ContinueWith(done =>
        {
            var came = done.IsCompletedSuccessfully
                ? done.Result
                : new StartCame(started, new StartRun(
                    $"failed  {started.Where}: the start failed before its record opened, and is tried again at a later look: "
                    + $"{done.Exception?.InnerException?.Message ?? "it was cancelled"}", Opened: false), []);
            TaskCompletionSource moved;
            lock (_gate)
            {
                _came.Add(came);
                moved = _moved;
                _moved = NewSignal();
            }

            moved.TrySetResult();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    /// <summary>What each start a look let go of came to since the last look took them (<see cref="Carry"/>), each handed on once.</summary>
    internal IReadOnlyList<StartCame> TakeCame()
    {
        lock (_gate)
        {
            var came = _came.ToList();
            _came.Clear();
            return came;
        }
    }

    /// <summary>How many runs are still going: started, opened, and not yet concluded.</summary>
    public int Running
    {
        get { lock (_gate) return _running; }
    }

    /// <summary>
    /// Whether nothing runs, no landing pass, remote sync, judge or evaluation pass works beside the looks, and nothing ended or was
    /// said that a look has not yet reported.
    /// </summary>
    public bool Idle
    {
        get
        {
            lock (_gate)
            {
                return _running == 0 && _ended.Count == 0 && _came.Count == 0 && _landing is null && _syncing is null && _judging.Count == 0
                       && _passing is null && _passSaid.Count == 0;
            }
        }
    }

    // The remote sync beside the looks (PARALLEL1b), while a pass works, and the pass asked for meanwhile, which runs once after it.
    private Task? _syncing;
    private (Func<CancellationToken, Task<IReadOnlyList<string>>> Pass, CancellationToken Ct)? _syncAgain;

    /// <summary>Whether a remote sync pass works beside the looks (PARALLEL1b).</summary>
    internal bool Syncing
    {
        get { lock (_gate) return _syncing is not null; }
    }

    // The judges beside the looks (PARALLEL1b; the events design §A3): each topic's places, and the subjects being judged now.
    private readonly Dictionary<string, SemaphoreSlim> _topics = new(StringComparer.Ordinal);
    private readonly HashSet<string> _judging = new(StringComparer.Ordinal);

    /// <summary>
    /// Judge <paramref name="subject"/> of <paramref name="topic"/> beside the looks (PARALLEL1b, D181 point 3; the events design §A3):
    /// its work runs on its own task, a topic judging at most <paramref name="bound"/> subjects at once, and one subject never twice at
    /// once. What it says joins the next look's report and wakes a wait, as an ending does; a judgement that says nothing wakes nothing.
    /// </summary>
    /// <returns>False where that subject is being judged already: nothing more is begun, and the next look asks again.</returns>
    internal bool Judge(string topic, string subject, int bound, Func<CancellationToken, Task<IReadOnlyList<string>>> work, CancellationToken ct)
    {
        SemaphoreSlim places;
        lock (_gate)
        {
            if (!_judging.Add($"{topic}\n{subject}")) return false;
            if (!_topics.TryGetValue(topic, out places!)) _topics[topic] = places = new SemaphoreSlim(bound, bound);
        }

        _ = Task.Run(() => ApartAsync(() => JudgeAsync(topic, subject, places, work, ct)), CancellationToken.None);
        return true;
    }

    private async Task JudgeAsync(
        string topic, string subject, SemaphoreSlim places, Func<CancellationToken, Task<IReadOnlyList<string>>> work, CancellationToken ct)
    {
        IReadOnlyList<string> said;
        try
        {
            await places.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                said = await work(ct).ConfigureAwait(false);
            }
            finally
            {
                places.Release();
            }
        }
        catch (Exception error)
        {
            // A closing loop cancels it; anything else past the work's own catches is said once, in the next report.
            said = ct.IsCancellationRequested ? [] : [$"{topic}  `{subject}` was not judged this time, and is judged again at a later look: {error.Message}"];
        }

        TaskCompletionSource moved;
        lock (_gate)
        {
            _ended.AddRange(said.Select(line => new StartRun(line, Opened: false)));
            _judging.Remove($"{topic}\n{subject}");
            moved = _moved;
            _moved = NewSignal();
        }

        moved.TrySetResult();
    }

    // A pass beside the looks ended while a session of this loop's held a quest, so the next look asks where each claim stands.
    private bool _claimsDue;

    /// <summary>
    /// Whether a remote sync pass beside the looks ended since the last look that asked, while a session of this loop's held a quest
    /// (PARALLEL1b): the lost-claim stop rides every sync (D68 §5), so the look after it asks, as the whole look asked after its own
    /// sync. Taken once: true for the one look that asks. A pending one wakes a wait, as an ending does.
    /// </summary>
    internal bool TakeClaimsDue()
    {
        lock (_gate)
        {
            var due = _claimsDue;
            _claimsDue = false;
            return due;
        }
    }

    /// <summary>
    /// Run a remote sync pass beside the looks (PARALLEL1b, D181 point 3), as a landing pass runs: a remote that does not answer holds
    /// that pass, never a local start. One pass at a time, since the set's passes share one gate; one asked for while a pass works runs
    /// once after it, however many were asked, so an ending's sync is never lost to the deadline's. What a pass says joins the next
    /// look's report, and wakes a wait as an ending does; a pass that says nothing wakes nothing.
    /// </summary>
    /// <returns>True where the pass began now; false where one works, and this one follows it.</returns>
    internal bool SyncBeside(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_syncing is not null)
            {
                _syncAgain = (pass, ct);
                return false;
            }

            _syncing = Task.Run(() => ApartAsync(() => KeepSyncingAsync(pass, ct)), CancellationToken.None);
            return true;
        }
    }

    private async Task KeepSyncingAsync(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct)
    {
        while (true)
        {
            IReadOnlyList<string> said;
            try
            {
                said = await pass(ct).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                // A closing loop cancels it; anything else past the pass's own catches is said once, in the next report.
                said = ct.IsCancellationRequested ? [] : [$"sync  the pass beside the looks failed, and the next look tries again: {error.Message}"];
            }

            TaskCompletionSource moved;
            var again = false;
            lock (_gate)
            {
                _ended.AddRange(said.Select(line => new StartRun(line, Opened: false)));
                // The claims a pass may have moved are asked by the next look, and only where a session of this loop's holds one.
                if (!Live.IsEmpty && !ct.IsCancellationRequested) _claimsDue = true;
                if (_syncAgain is { } next && !next.Ct.IsCancellationRequested)
                {
                    (pass, ct) = next;
                    again = true;
                }
                else
                {
                    _syncing = null;
                }

                _syncAgain = null;
                moved = _moved;
                _moved = NewSignal();
            }

            moved.TrySetResult();
            if (!again) return;
        }
    }

    // The evaluation runtime's pass beside the looks (PARALLEL1h), while one works, and the pass asked for meanwhile, which runs once
    // after it; and what the passes said since the last look took it.
    private Task? _passing;
    private (Func<CancellationToken, Task<IReadOnlyList<string>>> Pass, CancellationToken Ct)? _passAgain;
    private readonly List<string> _passSaid = [];

    /// <summary>
    /// Run the evaluation runtime's pass beside the looks (PARALLEL1h, D181 point 3), so a pass that a read or a plugin holds never holds
    /// the next look. One pass at a time, since the runtime judges its units under one lock; one asked for while a pass works runs once
    /// after it, however many were asked. What a pass says joins the next look's report and wakes nothing, as it joined its own look's
    /// before: what wakes a plan is the pass's own nudge (a set naming a plugin anew, a timer the records persist).
    /// </summary>
    /// <returns>True where the pass began now; false where one works, and this one follows it.</returns>
    internal bool PassBeside(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_passing is not null)
            {
                _passAgain = (pass, ct);
                return false;
            }

            _passing = Task.Run(() => ApartAsync(() => KeepPassingAsync(pass, ct)), CancellationToken.None);
            return true;
        }
    }

    private async Task KeepPassingAsync(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct)
    {
        while (true)
        {
            IReadOnlyList<string> said;
            try
            {
                said = await pass(ct).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                // A closing loop cancels it; anything else past the pass's own catches is said once, in the next report.
                said = ct.IsCancellationRequested ? [] : [$"work  the plugins' steps were not judged this time, and are judged again at the next look: {error.Message}"];
            }

            TaskCompletionSource moved;
            var again = false;
            lock (_gate)
            {
                _passSaid.AddRange(said);
                if (_passAgain is { } next && !next.Ct.IsCancellationRequested)
                {
                    (pass, ct) = next;
                    again = true;
                }
                else
                {
                    _passing = null;
                }

                _passAgain = null;
                moved = _moved;
                _moved = NewSignal();
            }

            // Wakes only what waits for the set to settle: an ending's wait reads its own conditions again, and none is the pass's.
            moved.TrySetResult();
            if (!again) return;
        }
    }

    // The landing pass beside the looks (LAND2b), while it works: one at a time, since a plugin's two minutes may outlast a look.
    private Task? _landing;

    /// <summary>Whether a landing pass still works beside the looks (LAND2b): the next look chooses again only once it ended.</summary>
    public bool Landing
    {
        get { lock (_gate) return _landing is not null; }
    }

    /// <summary>
    /// Run a landing pass beside the looks (LAND2b, D145 point 2), as a start runs beside the look that began it, so a plugin's
    /// two minutes never hold one. What it says joins the next look's report, as an ending does, and wakes a wait as one does.
    /// </summary>
    /// <param name="ended">
    /// Told once the pass has ended and <see cref="Landing"/> is false again (FABRIC-END7's review), so what was held back for it can be
    /// offered at once.
    /// </param>
    /// <returns>False where a pass already works: one at a time, and the next look chooses again.</returns>
    internal bool Beside(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct, Action? ended = null)
    {
        lock (_gate)
        {
            if (_landing is not null) return false;
            _landing = Task.Run(() => ApartAsync(() => KeepLandingAsync(pass, ct, ended)), CancellationToken.None);
            return true;
        }
    }

    private async Task KeepLandingAsync(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct, Action? ended)
    {
        IReadOnlyList<string> said;
        try
        {
            said = await pass(ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // A closing loop cancels it; anything else past its own catches is said once, in the next report.
            said = ct.IsCancellationRequested ? [] : [$"landing  the sessions due to land could not be landed this look, and are tried again at the next: {error.Message}"];
        }

        TaskCompletionSource moved;
        lock (_gate)
        {
            _ended.AddRange(said.Select(line => new StartRun(line, Opened: false)));
            _landing = null;
            moved = _moved;
            _moved = NewSignal();
        }

        moved.TrySetResult();
        try
        {
            ended?.Invoke();
        }
        catch (Exception error) when (error is ObjectDisposedException or InvalidOperationException)
        {
            // The loop that would hear it has closed: nothing is held back for a loop that is gone.
        }
    }

    /// <summary>
    /// Start a run, and answer once it has opened its session record or come to nothing before that.
    /// </summary>
    /// <param name="run">The run, handed the action it calls once its record is open.</param>
    /// <returns>
    /// Null once the record opened: the run goes on here, and its ending joins a later look's report. What it
    /// came to when it ended before opening: a hold or a refusal, the look's own to report.
    /// </returns>
    /// <exception cref="Exception">Whatever the run threw before its record opened, as a look always failed on it.</exception>
    internal async Task<StartRun?> StartAsync(Func<Action, Task<StartRun>> run)
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _running++;

        Task<StartRun> started;
        try
        {
            started = run(() => opened.TrySetResult());
        }
        catch (Exception error)
        {
            started = Task.FromException<StartRun>(error);
        }

        var kept = KeepAsync(started, opened.Task);
        await Task.WhenAny(opened.Task, kept).ConfigureAwait(false);
        return opened.Task.IsCompleted ? null : await kept.ConfigureAwait(false);
    }

    /// <summary>
    /// Every ending not yet reported, in the order they ended, then what the evaluation runtime's passes beside the looks said
    /// (PARALLEL1h) — each handed on once.
    /// </summary>
    internal IReadOnlyList<StartRun> Drain()
    {
        lock (_gate)
        {
            List<StartRun> ended = [.. _ended, .. _passSaid.Select(line => new StartRun(line, Opened: false))];
            _ended.Clear();
            _passSaid.Clear();
            return ended;
        }
    }

    /// <summary>
    /// Completes once an ending waits to be reported, the claims a sync beside the looks may have moved wait to be asked
    /// (PARALLEL1b), or a judgment beside them moved a fact the plan reads (PARALLEL1g) — at once when one already does.
    /// </summary>
    public async Task EndedAsync(CancellationToken ct = default)
    {
        while (true)
        {
            Task moved;
            lock (_gate)
            {
                if (_ended.Count > 0 || _claimsDue || _factsMoved || _came.Any(came => came.Opened)) return;
                moved = _moved.Task;
            }

            await moved.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Completes once nothing runs: every session concluded and its record written, and no landing pass, remote sync, judge or
    /// evaluation pass works (LAND2b, PARALLEL1b, PARALLEL1h). Never cancelled, on purpose — it is what a closing driver waits on, so every record says how it ended
    /// (REV3, D104).
    /// </summary>
    public async Task SettledAsync()
    {
        while (true)
        {
            Task moved;
            lock (_gate)
            {
                if (_running == 0 && _landing is null && _syncing is null && _judging.Count == 0 && _passing is null) return;
                moved = _moved.Task;
            }

            await moved.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Wait for an ending, <paramref name="pace"/>, or <paramref name="ct"/>, whichever comes first. Never
    /// throws: the caller reads the token to learn whether it was the close.
    /// </summary>
    /// <param name="endings">False waits out the pace whatever ended — for a caller whose last look failed.</param>
    internal async Task NextAsync(TimeSpan pace, CancellationToken ct, bool endings = true)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var paced = Task.Delay(pace, waiting.Token);
        await (endings ? Task.WhenAny(EndedAsync(waiting.Token), paced) : Task.WhenAny(paced)).ConfigureAwait(false);
        // The one still waiting ends here, rather than hold a timer or a wait for the rest of the pace.
        await waiting.CancelAsync().ConfigureAwait(false);
    }

    private async Task<StartRun> KeepAsync(Task<StartRun> run, Task opened)
    {
        StartRun? ended = null;
        try
        {
            ended = await run.ConfigureAwait(false);
            return ended;
        }
        catch (Exception error) when (opened.IsCompleted)
        {
            // A run concludes its own record however it ends, so this is an error past that. Said once, in the
            // next look's report, rather than lost with the look that started it.
            ended = new StartRun($"failed  a session's run ended in an error the driver did not expect: {error.Message}", true);
            return ended;
        }
        finally
        {
            TaskCompletionSource moved;
            lock (_gate)
            {
                _running--;
                if (opened.IsCompleted && ended is not null) _ended.Add(ended);
                moved = _moved;
                _moved = NewSignal();
            }

            moved.TrySetResult();
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Work kept beside the looks, begun from a look's stage that does not wait on it: its starts' waits for a place are said to no
    /// stage until a span of its own takes them, never as the queue of the stage that began it (LOOKWAIT1b, D181 point 6).
    /// </summary>
    private static Task ApartAsync(Func<Task> work) => ProcessStarts.QueuedToAsync(queue: null, work);
}

/// <summary>What one start of a look is (DEV3): a quest's or an ask's intake, where it runs, on which adapter, and the plan's start.</summary>
/// <param name="Start">The quest's planned start, which its run is bound by once it opens (WORKFLOW1e); null for an intake.</param>
internal sealed record StartBegun(string? Quest, string? Ask, string Where, string? Workspace, string Adapter, Consideration? Start = null);

/// <summary>
/// What one start came to (DEV3, PARALLEL1b): null <paramref name="Came"/> for a record opened and running on, else its hold, refusal
/// or failure; with the trust holds it met (D73), which the report's screen offers.
/// </summary>
internal sealed record StartCame(StartBegun Started, StartRun? Came, IReadOnlyList<TrustHold> Untrusted)
{
    /// <summary>Whether it opened a record: what a look counts as progress, and binds.</summary>
    public bool Opened => Came is null || Came.Opened;
}
