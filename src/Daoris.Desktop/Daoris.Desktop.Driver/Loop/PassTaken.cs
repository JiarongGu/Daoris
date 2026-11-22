namespace Daoris.Driver;

/// <summary>
/// The asks one pass took (<see cref="PassAsks.Take"/>): whether a door asks for a whole pass, the units the doors ask it for, judged first in
/// its turn, and the doors waiting on it, each answered as soon as the unit it asked for is judged, else once the pass ends. A unit asked for
/// while the pass runs is taken by it as well (<see cref="Late"/>), within <see cref="PassAsks.LatePerPass"/>; the rest wait for the next pass.
/// </summary>
public sealed class PassTaken
{
    private readonly Lock _gate = new();

    private readonly List<(string? Unit, TaskCompletionSource<PassOutcome> Answer, bool Whole)> _waiting;

    private readonly Func<int, (IReadOnlySet<string> Units, List<(string? Unit, TaskCompletionSource<PassOutcome> Answer, bool Whole)> Waiting)>? _late;

    private int _lateTaken;

    internal PassTaken(
        bool asked, IReadOnlySet<string> units, IReadOnlyList<(string? Unit, TaskCompletionSource<PassOutcome> Answer, bool Whole)> waiting,
        Func<int, (IReadOnlySet<string> Units, List<(string? Unit, TaskCompletionSource<PassOutcome> Answer, bool Whole)> Waiting)>? late = null)
    {
        Asked = asked;
        Units = units;
        _waiting = [.. waiting];
        _late = late;
    }

    /// <summary>Whether a door asked for a whole pass.</summary>
    public bool Asked { get; }

    /// <summary>The units the doors asked for, by id: what the pass judges first (<see cref="NodeRuntimeLook.Asked"/>).</summary>
    public IReadOnlySet<string> Units { get; }

    /// <summary>
    /// The units asked for since this pass began, each taken once: the pass judges them at its next unit boundary and answers the doors waiting on
    /// them as it judges each (<see cref="Judged"/>), else not judged at its end. Empty where none is asked, or where the pass has taken
    /// <see cref="PassAsks.LatePerPass"/> already: the rest stay asks for the next pass.
    /// </summary>
    public IReadOnlySet<string> Late()
    {
        if (_late is null) return new HashSet<string>(StringComparer.Ordinal);
        lock (_gate)
        {
            if (_lateTaken >= PassAsks.LatePerPass) return new HashSet<string>(StringComparer.Ordinal);
        }

        var (units, waiting) = _late(PassAsks.LatePerPass - _lateTaken);
        lock (_gate)
        {
            _waiting.AddRange(waiting);
            _lateTaken += units.Count;
        }

        return units;
    }

    /// <summary>The pass left <paramref name="unit"/>, asked while it ran, unjudged at its ask: each unit door waiting on it is answered so now, and its fact stays open.</summary>
    public void NotJudged(string unit) => Answer(unit, PassOutcome.NotJudged, whole: false);

    /// <summary>The pass kept <paramref name="unit"/>'s judgement: each door waiting on that unit is answered now, before the pass ends.</summary>
    public void Judged(string unit) => Answer(unit, PassOutcome.Judged, whole: true);

    /// <summary>The pass found <paramref name="unit"/> not governed by the read it judges under: each unit door waiting on it is answered so now; a whole door waits for the pass's end.</summary>
    public void Ungoverned(string unit) => Answer(unit, PassOutcome.NotGoverned, whole: false);

    /// <summary>The pass could not judge <paramref name="unit"/>, asked while it ran: each unit door waiting on it is answered so now, and the pass goes on.</summary>
    public void Failed(string unit) => Answer(unit, PassOutcome.Failed, whole: false);

    private void Answer(string unit, PassOutcome outcome, bool whole)
    {
        lock (_gate)
        {
            foreach (var (_, answer, _) in _waiting.Where(each => string.Equals(each.Unit, unit, StringComparison.Ordinal) && (whole || !each.Whole))) answer.TrySetResult(outcome);
        }
    }

    /// <summary>The pass ended: every door not answered yet is, <paramref name="passed"/> where it read and judged; a wait for one unit never judged is answered not judged.</summary>
    public void Done(bool passed)
    {
        lock (_gate)
        {
            foreach (var (_, answer, whole) in _waiting)
            {
                answer.TrySetResult(!passed ? PassOutcome.Failed : whole ? PassOutcome.Ended : PassOutcome.NotJudged);
            }
        }
    }
}
