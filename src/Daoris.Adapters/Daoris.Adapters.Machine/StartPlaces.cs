using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>
/// The machine's places for process starts: a few at a time, and a person's first.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> .NET starts every process of this one under a single lock, held across <c>CreateProcess</c>, and on a
/// machine whose antivirus hooks process creation that call is slow, slower under load. A start made synchronously on
/// whichever thread asked would park that thread on the lock: a sweep's git fan-out parked one per git (51 of 76 in the
/// frozen window's stack), the window's own thread among them. A start takes one of <see cref="Bound"/> places first, waiting
/// asynchronously, so the rest of a fan-out queues as tasks rather than as threads. Only the start is placed, never the run: a
/// child that runs for minutes holds no place.</para>
///
/// <para><b>Four.</b> The lock lets one start through at a time, so more places only park more threads on it; four keep the
/// next starts ready the moment the lock frees, and a start hung in an antivirus scan holds one place, not every start.</para>
///
/// <para><b>A person's place.</b> A start says its class (<see cref="AdmissionClasses"/>): the background holds at most
/// <see cref="BackgroundBound"/> places, so one is always free of it, and a place that frees goes to a person waiting before
/// the background waiting. A reserved place, not only a priority: four background starts held in a scan would hold every
/// place a priority alone could hand on. The runtime's lock still makes a person's start wait for the starts already inside
/// <c>CreateProcess</c>, at most three, never for the queue behind them.</para>
/// </remarks>
public sealed class StartPlaces : IMachineAdmission
{
    /// <summary>How many starts may be under way at once.</summary>
    public const int Bound = 4;

    /// <summary>How many of them may be the background's: all but the one a person's start always finds.</summary>
    public const int BackgroundBound = Bound - 1;

    /// <summary>The one gate a process's starts pass, as the runtime's lock is one per process.</summary>
    public static StartPlaces Shared { get; } = new(Bound, BackgroundBound);

    private readonly int _bound;
    private readonly int _backgroundBound;

    // The places taken and who waits for one, under _gate. A waiter is in its queue until it is handed a place or abandons
    // its wait; whichever comes first takes it out, so a place is never handed to a wait that has gone.
    private readonly Lock _gate = new();
    private readonly LinkedList<TaskCompletionSource> _persons = new();
    private readonly LinkedList<TaskCompletionSource> _background = new();
    private int _taken;
    private int _takenByBackground;

    /// <summary>A gate of <paramref name="bound"/> places, <paramref name="background"/> of them the background's at most (at least one).</summary>
    public StartPlaces(int bound, int background)
    {
        _bound = bound;
        _backgroundBound = Math.Clamp(background, 1, bound);
    }

    /// <summary>How many places the gate has, free or taken.</summary>
    public int Places => _bound;

    /// <summary>How many of them the background may hold at once.</summary>
    public int BackgroundPlaces => _backgroundBound;

    /// <summary>How many places are free now.</summary>
    public int Free
    {
        get
        {
            lock (_gate) return _bound - _taken;
        }
    }

    /// <summary>
    /// A place for a start of <paramref name="admissionClass"/>, answered at once where one is free for it, else once one is handed
    /// on. Cancelling <paramref name="ct"/> while it waits throws <see cref="OperationCanceledException"/> and takes no place.
    /// </summary>
    public ValueTask<IAdmittedPlace> PlaceAsync(string admissionClass, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var person = admissionClass == AdmissionClasses.Person;
        LinkedListNode<TaskCompletionSource> waiting;
        lock (_gate)
        {
            if (Opens(person))
            {
                Take(person);
                return ValueTask.FromResult<IAdmittedPlace>(new Place(this, admissionClass, person));
            }

            waiting = Queue(person).AddLast(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        return new ValueTask<IAdmittedPlace>(WaitAsync(admissionClass, person, waiting, ct));
    }

    private async Task<IAdmittedPlace> WaitAsync(string admissionClass, bool person, LinkedListNode<TaskCompletionSource> waiting, CancellationToken ct)
    {
        using (ct.Register(() => Abandon(person, waiting, ct)))
        {
            await waiting.Value.Task.ConfigureAwait(false);
        }

        // A place handed on just as the wait is given up is given back: a start cancelled before it began never begins.
        if (ct.IsCancellationRequested)
        {
            Leave(person);
            ct.ThrowIfCancellationRequested();
        }

        return new Place(this, admissionClass, person);
    }

    /// <summary>A wait given up: out of its queue, unless a place is handed to it first, which its start then uses.</summary>
    private void Abandon(bool person, LinkedListNode<TaskCompletionSource> waiting, CancellationToken ct)
    {
        lock (_gate)
        {
            if (waiting.List is null) return;
            Queue(person).Remove(waiting);
        }

        waiting.Value.TrySetCanceled(ct);
    }

    /// <summary>A place given back, and handed on: to a person waiting first, then to the background while it has room.</summary>
    private void Leave(bool person)
    {
        List<TaskCompletionSource> handed = [];
        lock (_gate)
        {
            _taken--;
            if (!person) _takenByBackground--;
            HandOn(_persons, person: true, handed);
            HandOn(_background, person: false, handed);
        }

        // Outside the gate, and their continuations off this thread: a start handed its place runs on its own.
        foreach (var each in handed) each.TrySetResult();
    }

    private void HandOn(LinkedList<TaskCompletionSource> queue, bool person, List<TaskCompletionSource> handed)
    {
        while (queue.First is { } next && Opens(person))
        {
            queue.RemoveFirst();
            Take(person);
            handed.Add(next.Value);
        }
    }

    // Under _gate. A queue is waited in only while its class has no place, and each freed place is handed on at once, so a
    // start that finds a place open has nobody of its class ahead of it.
    private bool Opens(bool person) => _taken < _bound && (person || _takenByBackground < _backgroundBound);

    private void Take(bool person)
    {
        _taken++;
        if (!person) _takenByBackground++;
    }

    private LinkedList<TaskCompletionSource> Queue(bool person) => person ? _persons : _background;

    /// <summary>One place lent; given back once, however often it is disposed.</summary>
    private sealed class Place(StartPlaces gate, string admissionClass, bool person) : IAdmittedPlace
    {
        private int _given;

        public string AdmissionClass => admissionClass;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _given, 1) == 0) gate.Leave(person);
            return ValueTask.CompletedTask;
        }
    }
}
