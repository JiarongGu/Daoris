using Daoris.Ports.Processes;

namespace Daoris.Testing;

/// <summary>
/// The machine's processes as a test scripts them. As an <see cref="IMachineAdmission"/> it lends a bounded number of places, first
/// asked first placed whatever their class; as an <see cref="IProcessHost"/> it answers each start by the first row whose predicate
/// holds, and a start no row answers throws, naming it; as an <see cref="IProcessLiveness"/> a run runs until it ends, and an id the
/// test marks runs at its creation time. No process starts and nothing waits but the test's own.
/// </summary>
public sealed class ScriptedProcesses : IMachineAdmission, IProcessHost, IProcessLiveness
{
    // Where the scripted runs' ids and creation times start, so none is a real process of the test's machine by chance.
    private const int FirstId = 7_000_000;
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly int _places;
    private readonly Lock _gate = new();
    private readonly LinkedList<TaskCompletionSource> _waiting = new();
    private readonly List<string> _asked = [];
    private readonly List<(Func<ProcessSpec, bool> When, Func<ProcessSpec, ScriptedAnswer> Answer)> _rows = [];
    private readonly List<ProcessSpec> _started = [];
    private readonly Dictionary<int, (DateTimeOffset Created, Func<bool> Runs)> _live = [];
    private Func<int, DateTimeOffset, bool>? _beyond;
    private int _taken;
    private int _lastId = FirstId;

    /// <summary>An admission of <paramref name="places"/> places.</summary>
    public ScriptedProcesses(int places) => _places = places;

    /// <summary>Answers each start <paramref name="when"/> holds for with what <paramref name="answer"/> makes of it.</summary>
    public ScriptedProcesses On(Func<ProcessSpec, bool> when, Func<ProcessSpec, ScriptedAnswer> answer)
    {
        lock (_gate) _rows.Add((when, answer));
        return this;
    }

    /// <summary>Every start made so far, in order.</summary>
    public IReadOnlyList<ProcessSpec> Started
    {
        get
        {
            lock (_gate) return [.. _started];
        }
    }

    /// <summary>
    /// Says the process <paramref name="id"/> made at <paramref name="created"/> runs, as one the test stands in for: always, or while
    /// <paramref name="runs"/> says so.
    /// </summary>
    public ScriptedProcesses Live(int id, DateTimeOffset created, Func<bool>? runs = null)
    {
        lock (_gate) _live[id] = (created, runs ?? (() => true));
        return this;
    }

    public async ValueTask<IChildRun> StartAsync(ProcessSpec specification, CancellationToken ct)
    {
        await using var place = await PlaceAsync(specification.AdmissionClass, ct).ConfigureAwait(false);
        int id;
        DateTimeOffset created;
        Func<ProcessSpec, ScriptedAnswer> answer;
        lock (_gate)
        {
            answer = _rows.FirstOrDefault(each => each.When(specification)).Answer
                ?? throw new InvalidOperationException($"no row answers the start of {specification.Program} {string.Join(' ', specification.Arguments)}");
            _started.Add(specification);
            id = ++_lastId;
            created = Epoch.AddSeconds(id - FirstId);
            _live[id] = (created, () => true);
        }

        return new ScriptedRun(this, id, created, answer(specification), specification.RawOutput);
    }

    /// <summary>Answers whether a process this script never made or marked runs by <paramref name="runs"/>, instead of no.</summary>
    public ScriptedProcesses Beyond(Func<int, DateTimeOffset, bool> runs)
    {
        lock (_gate) _beyond = runs;
        return this;
    }

    public bool Runs(int id, DateTimeOffset created)
    {
        (DateTimeOffset Created, Func<bool> Runs) live;
        Func<int, DateTimeOffset, bool>? beyond;
        lock (_gate)
        {
            beyond = _beyond;
            if (!_live.TryGetValue(id, out live)) return beyond?.Invoke(id, created) ?? false;
        }

        return (live.Created - created).Duration() < TimeSpan.FromSeconds(1) && live.Runs();
    }

    internal void Ended(int id)
    {
        lock (_gate) _live.Remove(id);
    }

    /// <summary>How many places are free now.</summary>
    public int Free
    {
        get
        {
            lock (_gate) return _places - _taken;
        }
    }

    /// <summary>The class of every place asked so far, in order.</summary>
    public IReadOnlyList<string> Asked
    {
        get
        {
            lock (_gate) return [.. _asked];
        }
    }

    public ValueTask<IAdmittedPlace> PlaceAsync(string admissionClass, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LinkedListNode<TaskCompletionSource> waiting;
        lock (_gate)
        {
            _asked.Add(admissionClass);
            if (_taken < _places && _waiting.Count == 0)
            {
                _taken++;
                return ValueTask.FromResult<IAdmittedPlace>(new Place(this, admissionClass));
            }

            waiting = _waiting.AddLast(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        return new ValueTask<IAdmittedPlace>(WaitAsync(admissionClass, waiting, ct));
    }

    private async Task<IAdmittedPlace> WaitAsync(string admissionClass, LinkedListNode<TaskCompletionSource> waiting, CancellationToken ct)
    {
        using (ct.Register(() => Abandon(waiting, ct)))
        {
            await waiting.Value.Task.ConfigureAwait(false);
        }

        if (ct.IsCancellationRequested)
        {
            Leave();
            ct.ThrowIfCancellationRequested();
        }

        return new Place(this, admissionClass);
    }

    private void Abandon(LinkedListNode<TaskCompletionSource> waiting, CancellationToken ct)
    {
        lock (_gate)
        {
            if (waiting.List is null) return;
            _waiting.Remove(waiting);
        }

        waiting.Value.TrySetCanceled(ct);
    }

    private void Leave()
    {
        TaskCompletionSource? next = null;
        lock (_gate)
        {
            _taken--;
            if (_waiting.First is { } first)
            {
                _waiting.RemoveFirst();
                _taken++;
                next = first.Value;
            }
        }

        next?.TrySetResult();
    }

    private sealed class Place(ScriptedProcesses gate, string admissionClass) : IAdmittedPlace
    {
        private int _given;

        public string AdmissionClass => admissionClass;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _given, 1) == 0) gate.Leave();
            return ValueTask.CompletedTask;
        }
    }
}
