namespace Daoris.Driver;

/// <summary>
/// The asks a door makes of the loop for a whole pass of the evaluation runtime: a landing held by a run's graph for a judgement not made yet
/// asks for a pass that reads the units afresh and judges that run first, and a press waits for it within a bound. A pass takes the asks
/// made before it began (<see cref="Take"/>) and answers each once its unit is judged, else at its end; one made while it runs, its unit
/// kept however long the press waited, is the next pass's, which the ask's wake starts after it.
/// </summary>
/// <param name="wake">Asks the loop for the pass: the wake, with the whole look's read asked of it.</param>
public sealed class PassAsks(Action wake)
{
    private readonly Lock _gate = new();

    private List<(string? Unit, TaskCompletionSource<bool> Answer)> _waiting = [];

    private HashSet<string> _units = new(StringComparer.Ordinal);

    private bool _asked;

    /// <summary>Whether a whole pass is asked and no pass has taken the ask yet.</summary>
    public bool Asked
    {
        get
        {
            lock (_gate) return _asked;
        }
    }

    /// <summary>
    /// Ask for a whole pass that judges <paramref name="unit"/> first and wait for it, within <paramref name="bound"/>: true where it judged the
    /// unit or ended in time, false past the bound or where it failed. A bound of none asks and waits for nothing.
    /// </summary>
    public async Task<bool> WaitAsync(string? unit, TimeSpan bound, CancellationToken ct)
    {
        TaskCompletionSource<bool>? answer = bound > TimeSpan.Zero ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        lock (_gate)
        {
            _asked = true;
            if (unit is not null) _units.Add(unit);
            if (answer is not null) _waiting.Add((unit, answer));
        }

        wake();
        if (answer is null) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(bound);
        await using (timeout.Token.Register(() => answer.TrySetResult(false)))
        {
            var passed = await answer.Task.ConfigureAwait(false);
            lock (_gate) _waiting.Remove((unit, answer));
            return passed;
        }
    }

    /// <summary>The asks a pass beginning now takes: whether one is asked, the units asked for, and those waiting on it.</summary>
    public PassTaken Take()
    {
        lock (_gate)
        {
            var taken = new PassTaken(_asked, _units, _waiting);
            _asked = false;
            _units = new HashSet<string>(StringComparer.Ordinal);
            _waiting = [];
            return taken;
        }
    }
}
