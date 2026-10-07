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
/// <para><b>A run that never opened a record is its own look's to report</b>, never a later one's: a hold,
/// a refusal and an error before the record opened are what that look says about the quest. Only a run
/// whose record opened is kept here, since only it outlives the look.</para>
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

    /// <summary>How many runs are still going: started, opened, and not yet concluded.</summary>
    public int Running
    {
        get { lock (_gate) return _running; }
    }

    /// <summary>Whether nothing runs, no landing pass works beside the looks, and nothing ended that a look has not yet reported.</summary>
    public bool Idle
    {
        get { lock (_gate) return _running == 0 && _ended.Count == 0 && _landing is null; }
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
    /// <returns>False where a pass already works: one at a time, and the next look chooses again.</returns>
    internal bool Beside(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_landing is not null) return false;
            _landing = Task.Run(() => KeepLandingAsync(pass, ct), CancellationToken.None);
            return true;
        }
    }

    private async Task KeepLandingAsync(Func<CancellationToken, Task<IReadOnlyList<string>>> pass, CancellationToken ct)
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

    /// <summary>Every ending not yet reported, in the order they ended — each handed on once.</summary>
    internal IReadOnlyList<StartRun> Drain()
    {
        lock (_gate)
        {
            var ended = _ended.ToList();
            _ended.Clear();
            return ended;
        }
    }

    /// <summary>Completes once an ending waits to be reported — at once when one already does.</summary>
    public async Task EndedAsync(CancellationToken ct = default)
    {
        while (true)
        {
            Task moved;
            lock (_gate)
            {
                if (_ended.Count > 0) return;
                moved = _moved.Task;
            }

            await moved.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Completes once nothing runs: every session concluded and its record written, and no landing pass works (LAND2b). Never
    /// cancelled, on purpose — it is what a closing driver waits on, so every record says how it ended (REV3, D104).
    /// </summary>
    public async Task SettledAsync()
    {
        while (true)
        {
            Task moved;
            lock (_gate)
            {
                if (_running == 0 && _landing is null) return;
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
}
