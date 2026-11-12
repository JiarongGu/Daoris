using Daoris.Ports.Processes;

namespace Daoris.Testing;

/// <summary>
/// The machine's processes as a test scripts them. As an <see cref="IMachineAdmission"/> it lends a bounded number of places, first
/// asked first placed whatever their class, and keeps the classes asked, so a test sees how its starts were placed and how many
/// places they held, with no process and no wait but its own. A person's reserved place is the real adapter's, proved there.
/// </summary>
public sealed class ScriptedProcesses : IMachineAdmission
{
    private readonly int _places;
    private readonly Lock _gate = new();
    private readonly LinkedList<TaskCompletionSource> _waiting = new();
    private readonly List<string> _asked = [];
    private int _taken;

    /// <summary>An admission of <paramref name="places"/> places.</summary>
    public ScriptedProcesses(int places) => _places = places;

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
