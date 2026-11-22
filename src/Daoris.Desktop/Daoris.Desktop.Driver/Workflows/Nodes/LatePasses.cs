namespace Daoris.Driver;

/// <summary>
/// The units asked for while a pass works (<see cref="NodeRuntimeLook.Late"/>), judged by a pass of their own within that pass's lock at each unit
/// boundary: what the fact left to hear (a step's answer, a settle, a word) is taken again, and each unit is judged on what it then reads, also
/// where the running pass judged it already or is judging it now. The ask never waits for the pass to end, which can take minutes behind the
/// machine's git places.
/// </summary>
internal sealed class LatePasses
{
    private readonly NodeRuntimeLook _look;
    private readonly IReadOnlyDictionary<string, GovernedUnit> _governed;
    private readonly HashSet<string> _judged = new(StringComparer.Ordinal);

    /// <param name="look">The running pass's look; each unit it asks for that is not governed is told so at once.</param>
    /// <param name="governed">The units the running pass judges under, by id.</param>
    public LatePasses(NodeRuntimeLook look, IReadOnlyDictionary<string, GovernedUnit> governed)
    {
        (_look, _governed) = (look, governed);
        foreach (var unit in look.Asked.Where(unit => !governed.ContainsKey(unit))) look.Ungoverned(unit);
    }

    /// <summary>Whether a unit pass judged <paramref name="unit"/>: the running pass leaves it out of its turn.</summary>
    public bool Judged(string unit) => _judged.Contains(unit);

    /// <summary>Whether a unit pass changed the awaited set the plugins are told.</summary>
    public bool Told { get; private set; }

    /// <summary>The look of the unit pass for the units asked since the last take that the pass governs; null where none. The others are told so.</summary>
    public NodeRuntimeLook? Take()
    {
        var late = _look.Late();
        if (late.Count == 0) return null;
        foreach (var unit in late.Where(unit => !_governed.ContainsKey(unit))) _look.Ungoverned(unit);
        var asked = late.Where(_governed.ContainsKey).ToHashSet(StringComparer.Ordinal);
        return asked.Count == 0 ? null : _look with { Whole = false, Asked = asked, Late = () => new HashSet<string>(StringComparer.Ordinal) };
    }

    /// <summary>What the unit pass of <paramref name="taken"/> did, joined to the running pass's.</summary>
    public void Merge(NodeRuntimeLook taken, NodeRuntimePass pass, List<WorkPerformed> done, List<string> said)
    {
        done.AddRange(pass.Performed);
        said.AddRange(pass.Said);
        Told |= pass.AwaitedChanged;
        _judged.UnionWith(taken.Asked);
    }
}
