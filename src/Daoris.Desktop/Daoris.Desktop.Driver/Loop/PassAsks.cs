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

    private List<(string? Unit, TaskCompletionSource<PassOutcome> Answer, bool Whole)> _waiting = [];

    private HashSet<string> _units = new(StringComparer.Ordinal);

    // The units asked for by a unit ask (never a whole one) and not taken yet: what a pass already running may still take (TakeLate).
    private HashSet<string> _alone = new(StringComparer.Ordinal);

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
    public async Task<bool> WaitAsync(string? unit, TimeSpan bound, CancellationToken ct) =>
        (await AskAsync(unit, bound, whole: true, ct).ConfigureAwait(false)).IsAnswer();

    /// <summary>Ask a pass to judge <paramref name="unit"/> alone and wait within <paramref name="bound"/>: how it answered. A running pass takes it (<see cref="PassTaken.Late"/>).</summary>
    public Task<PassOutcome> WaitUnitAsync(string unit, TimeSpan bound, CancellationToken ct) => AskAsync(unit, bound, whole: false, ct);

    private async Task<PassOutcome> AskAsync(string? unit, TimeSpan bound, bool whole, CancellationToken ct)
    {
        TaskCompletionSource<PassOutcome>? answer = bound > TimeSpan.Zero ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        lock (_gate)
        {
            _asked |= whole;
            if (unit is not null) _units.Add(unit);
            if (unit is not null && !whole) _alone.Add(unit);
            if (answer is not null) _waiting.Add((unit, answer, whole));
        }

        wake();
        if (answer is null) return PassOutcome.Bound;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(bound);
        await using (timeout.Token.Register(() => answer.TrySetResult(PassOutcome.Bound)))
        {
            var passed = await answer.Task.ConfigureAwait(false);
            lock (_gate) _waiting.Remove((unit, answer, whole));
            return passed;
        }
    }

    /// <summary>The asks a pass beginning now takes: whether one is asked, the units asked for, and those waiting on it.</summary>
    public PassTaken Take()
    {
        lock (_gate)
        {
            var taken = new PassTaken(_asked, _units, _waiting, TakeLate);
            _asked = false;
            _units = new HashSet<string>(StringComparer.Ordinal);
            _alone = new HashSet<string>(StringComparer.Ordinal);
            _waiting = [];
            return taken;
        }
    }

    /// <summary>The unit asks made since the last take, for a pass running: its units and the doors waiting on them. A whole ask stays for the next pass.</summary>
    private (IReadOnlySet<string> Units, List<(string? Unit, TaskCompletionSource<PassOutcome> Answer, bool Whole)> Waiting) TakeLate()
    {
        lock (_gate)
        {
            var wholes = _waiting.Where(each => each.Whole && each.Unit is not null).Select(each => each.Unit!).ToHashSet(StringComparer.Ordinal);
            var units = _alone.Where(unit => !wholes.Contains(unit)).ToHashSet(StringComparer.Ordinal);
            _alone.ExceptWith(units);
            _units.ExceptWith(units);
            var taken = _waiting.Where(each => !each.Whole && each.Unit is not null && units.Contains(each.Unit)).ToList();
            _waiting = [.. _waiting.Except(taken)];
            return (units, taken);
        }
    }
}
