namespace Daoris.Driver;

/// <summary>
/// The asks one pass took (<see cref="PassAsks.Take"/>): whether a door asks for a whole pass, the units the doors ask it for, judged first in
/// its turn, and the doors waiting on it, each answered as soon as the unit it asked for is judged, else once the pass ends.
/// </summary>
public sealed class PassTaken
{
    private readonly IReadOnlyList<(string? Unit, TaskCompletionSource<bool> Answer)> _waiting;

    internal PassTaken(bool asked, IReadOnlySet<string> units, IReadOnlyList<(string? Unit, TaskCompletionSource<bool> Answer)> waiting)
    {
        Asked = asked;
        Units = units;
        _waiting = waiting;
    }

    /// <summary>Whether a door asked for a whole pass.</summary>
    public bool Asked { get; }

    /// <summary>The units the doors asked for, by id: what the pass judges first (<see cref="NodeRuntimeLook.Asked"/>).</summary>
    public IReadOnlySet<string> Units { get; }

    /// <summary>The pass kept <paramref name="unit"/>'s judgement: each door waiting on that unit is answered now, before the pass ends.</summary>
    public void Judged(string unit)
    {
        foreach (var (_, answer) in _waiting.Where(each => string.Equals(each.Unit, unit, StringComparison.Ordinal))) answer.TrySetResult(true);
    }

    /// <summary>The pass ended: every door not answered yet is, <paramref name="passed"/> where it read and judged.</summary>
    public void Done(bool passed)
    {
        foreach (var (_, answer) in _waiting) answer.TrySetResult(passed);
    }
}
