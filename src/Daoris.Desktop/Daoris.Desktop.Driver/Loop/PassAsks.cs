namespace Daoris.Driver;

/// <summary>
/// The asks a door makes of the loop for a whole pass of the evaluation runtime (FABRIC-CUTOVER1d; the cutover design §4): a landing held by a
/// run's graph for a judgement not made yet asks for a pass that reads the units afresh, as the look's deadline does, and a press waits for it
/// within a bound. A pass takes the asks made before it began (<see cref="Take"/>) and answers them once it ends (<see cref="Done"/>); one made
/// while it runs waits for the next, which the ask's wake starts after it.
/// </summary>
/// <param name="wake">Asks the loop for the pass: link 8's wake, with the whole look's read asked of it.</param>
public sealed class PassAsks(Action wake)
{
    private readonly Lock _gate = new();

    private List<TaskCompletionSource<bool>> _waiting = [];

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
    /// Ask for a whole pass, and wait for the first that takes the ask to end, within <paramref name="bound"/>: true where it ended in time and
    /// read, false past the bound or where it failed. A bound of none asks and waits for nothing.
    /// </summary>
    public async Task<bool> WaitAsync(TimeSpan bound, CancellationToken ct)
    {
        TaskCompletionSource<bool>? waiter = bound > TimeSpan.Zero ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        lock (_gate)
        {
            _asked = true;
            if (waiter is not null) _waiting.Add(waiter);
        }

        wake();
        if (waiter is null) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(bound);
        await using (timeout.Token.Register(() => waiter.TrySetResult(false)))
        {
            var passed = await waiter.Task.ConfigureAwait(false);
            lock (_gate) _waiting.Remove(waiter);
            return passed;
        }
    }

    /// <summary>The asks a pass beginning now takes: whether one was asked, and those waiting on it.</summary>
    public (bool Asked, IReadOnlyList<TaskCompletionSource<bool>> Waiting) Take()
    {
        lock (_gate)
        {
            var taken = (_asked, (IReadOnlyList<TaskCompletionSource<bool>>)_waiting);
            _asked = false;
            _waiting = [];
            return taken;
        }
    }

    /// <summary>Answer the asks a pass took, once it ended: <paramref name="passed"/> where it read and judged.</summary>
    public static void Done((bool Asked, IReadOnlyList<TaskCompletionSource<bool>> Waiting) taken, bool passed)
    {
        foreach (var waiter in taken.Waiting) waiter.TrySetResult(passed);
    }
}
