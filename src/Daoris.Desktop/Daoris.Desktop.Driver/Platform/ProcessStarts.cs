using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Whose a process start is (PARALLEL1d, D181 point 5): a person's never waits behind the background's.
/// </summary>
public enum StartClass
{
    /// <summary>
    /// A start a person is waiting on: an agent for a conversation, a session going on with the person's words, a terminal's
    /// shell, a press (an update, a folder, an agent's install or sign-in, a plugin's trial), and the host the window waits on.
    /// </summary>
    Person,

    /// <summary>The machine's own: git, a plugin's background work, a probe, an agent the loop starts.</summary>
    Background,
}

/// <summary>
/// Process starts, a few at a time and never on the caller's thread (UIFREEZE1), a person's first (PARALLEL1d).
/// </summary>
/// <remarks>
/// <para><b>Why.</b> .NET starts every process of this one under a single lock, held across <c>CreateProcess</c>, and on a
/// machine whose antivirus hooks process creation that call is slow, slower under load. A start was made synchronously on
/// whichever thread asked, so a sweep's git fan-out parked a thread per git on the lock (51 of 76 in the frozen window's
/// stack), the window's own thread among them.</para>
///
/// <para><b>The gate.</b> A start first takes one of <see cref="Bound"/> places, waiting asynchronously, so the rest of a
/// fan-out queues as tasks rather than as threads; then it runs on the thread pool, so the caller's thread is free from the
/// first wait. Only the start is gated, never the run: a child that runs for minutes holds no place, and nothing waits on
/// it but its own caller.</para>
///
/// <para><b>Four.</b> The lock lets one start through at a time, so more places only park more threads on it; four keep the
/// next starts ready the moment the lock frees, and a start hung in an antivirus scan holds one place, not every start.</para>
///
/// <para><b>A person's place</b> (PARALLEL1d, D181 point 5). The four were shared, so an agent a person asked for queued
/// behind a git fan-out of hundreds. Now a start says whose it is (<see cref="StartClass"/>): the background holds at most
/// <see cref="BackgroundBound"/> places, so one is always free of it, and a place that frees goes to a person waiting before
/// the background waiting. A reserved place, not only a priority: four background starts held in a scan would hold every
/// place a priority alone could hand on. The total of four that protects the machine stays. The runtime's lock still makes a
/// person's start wait for the starts already inside <c>CreateProcess</c>, at most three, never for the queue behind them.</para>
///
/// <para><b>Every start of the desktop's</b> passes it (PROCSTART2): git's, a session's, a harness action's, a plugin's, the
/// host's, Edge's, and the file manager the window kit's launcher opens (<see cref="RunAsync(Action, CancellationToken)"/>).
/// <c>ProcessStartsCensusTests</c> names the few that stay outside, each with why, and whose each start inside is.</para>
/// </remarks>
public sealed class ProcessStarts
{
    /// <summary>How many starts may be under way at once.</summary>
    public const int Bound = 4;

    /// <summary>How many of them may be the background's: all but the one a person's start always finds.</summary>
    public const int BackgroundBound = Bound - 1;

    /// <summary>The one gate every start of the desktop's passes, as the lock is one per process.</summary>
    public static ProcessStarts Shared { get; } = new(Bound, BackgroundBound, Process.Start);

    // The stand-in a test's flow makes its starts with (Within); null in production.
    private static readonly AsyncLocal<Func<ProcessStartInfo, Process?>?> s_within = new();

    // Whom a wait for a place in this flow is said to (LOOKWAIT1b): the stage whose work it is, set for that work alone by
    // QueuedToAsync; null in a flow with none, and in work begun beside a stage.
    private static readonly AsyncLocal<Func<IDisposable>?> s_queue = new();

    private readonly int _bound;
    private readonly int _backgroundBound;
    private readonly Func<ProcessStartInfo, Process?> _start;

    // The places taken and who waits for one, under _gate. A waiter is in its queue until it is handed a place or abandons
    // its wait; whichever comes first takes it out, so a place is never handed to a wait that has gone.
    private readonly object _gate = new();
    private readonly LinkedList<TaskCompletionSource> _persons = new();
    private readonly LinkedList<TaskCompletionSource> _background = new();
    private int _taken;
    private int _takenByBackground;

    /// <summary>A gate of <paramref name="bound"/> places over <paramref name="start"/>, all but one the background's (at least one).</summary>
    internal ProcessStarts(int bound, Func<ProcessStartInfo, Process?> start)
        : this(bound, Math.Max(1, bound - 1), start)
    {
    }

    /// <summary>A gate of <paramref name="bound"/> places, <paramref name="background"/> of them the background's at most.</summary>
    internal ProcessStarts(int bound, int background, Func<ProcessStartInfo, Process?> start)
    {
        _bound = bound;
        _backgroundBound = Math.Clamp(background, 1, bound);
        _start = start;
    }

    /// <summary>How many places the gate has, free or taken.</summary>
    internal int Places => _bound;

    /// <summary>How many of them the background may hold at once.</summary>
    internal int BackgroundPlaces => _backgroundBound;

    /// <summary>How many places are free now: the tests' window on the gate.</summary>
    internal int Free
    {
        get
        {
            lock (_gate) return _bound - _taken;
        }
    }

    /// <summary>
    /// A person's start of <paramref name="info"/> (<see cref="StartAsync(ProcessStartInfo, StartClass, CancellationToken)"/>):
    /// the doors a person waits on start through this.
    /// </summary>
    public Task<Process?> StartAsync(ProcessStartInfo info, CancellationToken ct) => StartAsync(info, StartClass.Person, ct);

    /// <summary>
    /// Start <paramref name="info"/> once a place is free for <paramref name="whose"/>, on the thread pool. Cancelling
    /// <paramref name="ct"/> while waiting for a place throws <see cref="OperationCanceledException"/>; once the start is under
    /// way it is seen through, since a process abandoned as it started would run on unowned. The runtime's refusal to start is
    /// thrown as it threw it.
    /// </summary>
    public Task<Process?> StartAsync(ProcessStartInfo info, StartClass whose, CancellationToken ct)
    {
        var start = s_within.Value ?? _start;
        return RunAsync(() => start(info), whose, ct);
    }

    /// <summary>
    /// Run <paramref name="start"/>, a person's start the runtime's <see cref="Process"/> is not handed directly (the window
    /// kit's launcher opening the file manager, a terminal's shell), as <see cref="StartAsync(ProcessStartInfo, CancellationToken)"/>
    /// runs one: once a place is free, on the thread pool.
    /// </summary>
    public Task RunAsync(Action start, CancellationToken ct) => RunAsync<object?>(() =>
    {
        start();
        return null;
    }, StartClass.Person, ct);

    private async Task<T> RunAsync<T>(Func<T> start, StartClass whose, CancellationToken ct)
    {
        var timed = s_timed.Value;
        var asked = timed is null ? 0 : Stopwatch.GetTimestamp();
        await EnterAsync(whose, ct).ConfigureAwait(false);
        var entered = timed is null ? 0 : Stopwatch.GetTimestamp();
        try
        {
            return await Task.Run(start, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Leave(whose);
            timed?.Invoke(entered - asked, Stopwatch.GetTimestamp() - entered);
        }
    }

    // Who is told a start's wait and creation in this flow (Timing); null in a flow nobody times.
    private static readonly AsyncLocal<Action<long, long>?> s_timed = new();

    /// <summary>
    /// Tell <paramref name="timed"/> the wait for a place and the creation inside it, in <see cref="Stopwatch"/> ticks, of each start this
    /// flow makes through a gate until disposed (GITAPP1a-i, the git-app design §1.2: the gate's wait, the creation and the run apart).
    /// It changes nothing of the start: one the runtime refused or that threw is timed still, and one cancelled while it waited for its
    /// place, which never started, is not.
    /// </summary>
    internal static IDisposable Timing(Action<long, long> timed)
    {
        var before = s_timed.Value;
        s_timed.Value = timed;
        return new Untimed(before);
    }

    private sealed class Untimed(Action<long, long>? before) : IDisposable
    {
        public void Dispose() => s_timed.Value = before;
    }

    /// <summary>Take a place for <paramref name="whose"/>, now where one is free for it, else once one is handed on.</summary>
    private async Task EnterAsync(StartClass whose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        LinkedListNode<TaskCompletionSource> waiting;
        lock (_gate)
        {
            if (Opens(whose))
            {
                Take(whose);
                return;
            }

            waiting = Queue(whose).AddLast(waiter);
        }

        // LOOKWAIT1b: said to the stage whose work made this start, from here until it is handed its place or gives up its wait.
        using (s_queue.Value?.Invoke())
        using (ct.Register(() => Abandon(whose, waiting, ct)))
        {
            await waiter.Task.ConfigureAwait(false);
        }
    }

    /// <summary>A wait given up: out of its queue, unless a place was handed to it first, which its start then uses.</summary>
    private void Abandon(StartClass whose, LinkedListNode<TaskCompletionSource> waiting, CancellationToken ct)
    {
        lock (_gate)
        {
            if (waiting.List is null) return;
            Queue(whose).Remove(waiting);
        }

        waiting.Value.TrySetCanceled(ct);
    }

    /// <summary>A place given back, and handed on: to a person waiting first, then to the background while it has room.</summary>
    private void Leave(StartClass whose)
    {
        List<TaskCompletionSource> handed = [];
        lock (_gate)
        {
            _taken--;
            if (whose == StartClass.Background) _takenByBackground--;
            HandOn(_persons, StartClass.Person, handed);
            HandOn(_background, StartClass.Background, handed);
        }

        // Outside the gate, and their continuations off this thread: a start handed its place runs on its own.
        foreach (var each in handed) each.TrySetResult();
    }

    private void HandOn(LinkedList<TaskCompletionSource> queue, StartClass whose, List<TaskCompletionSource> handed)
    {
        while (queue.First is { } next && Opens(whose))
        {
            queue.RemoveFirst();
            Take(whose);
            handed.Add(next.Value);
        }
    }

    // Under _gate. A queue is waited in only while its class has no place, and each freed place is handed on at once, so a
    // start that finds a place open has nobody of its class ahead of it.
    private bool Opens(StartClass whose) =>
        _taken < _bound && (whose == StartClass.Person || _takenByBackground < _backgroundBound);

    private void Take(StartClass whose)
    {
        _taken++;
        if (whose == StartClass.Background) _takenByBackground++;
    }

    private LinkedList<TaskCompletionSource> Queue(StartClass whose) => whose == StartClass.Person ? _persons : _background;

    /// <summary>
    /// The test seam (PROCSTART2): until disposed, every start this flow makes through a gate is made by
    /// <paramref name="start"/> instead of the runtime, still on the thread pool and inside its place, so a test sees where a
    /// site's start was made and answers it without a process. Production opens none.
    /// </summary>
    public static IDisposable Within(Func<ProcessStartInfo, Process?> start)
    {
        var before = s_within.Value;
        s_within.Value = start;
        return new Ending(before);
    }

    private sealed class Ending(Func<ProcessStartInfo, Process?>? before) : IDisposable
    {
        public void Dispose() => s_within.Value = before;
    }

    /// <summary>
    /// Run <paramref name="work"/> with each wait for a place that a start of its flow makes said to <paramref name="queue"/>
    /// (LOOKWAIT1b, D181 point 6): called as a wait begins, and what it returns disposed as the wait ends, handed its place or given
    /// up. A look's stage hands its span, so its line says the wait as the stage's queue, apart from its hold. Null says them to
    /// nobody: work begun beside a stage, which the stage does not wait on, is never the stage's, until a stage of its own takes it.
    /// </summary>
    /// <remarks>
    /// Set inside this method's own flow, so it reaches the work and what the work starts, and never the caller: an async method's
    /// changes to its flow do not return to its caller. Nothing is restored, and no span ended in another flow can leave it set.
    /// </remarks>
    public static async Task<T> QueuedToAsync<T>(Func<IDisposable>? queue, Func<Task<T>> work)
    {
        s_queue.Value = queue;
        return await work().ConfigureAwait(false);
    }

    /// <inheritdoc cref="QueuedToAsync{T}(Func{IDisposable}?, Func{Task{T}})"/>
    public static async Task QueuedToAsync(Func<IDisposable>? queue, Func<Task> work)
    {
        s_queue.Value = queue;
        await work().ConfigureAwait(false);
    }
}
