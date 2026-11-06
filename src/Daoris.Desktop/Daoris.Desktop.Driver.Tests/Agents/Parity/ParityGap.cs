namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A difference the oracle knows by name (the agent-lyntai design §6.3, 1d): what each path observed of it, set apart before the rest is
/// compared exactly. A gap that sets nothing apart on either path is closed, and fails until its name goes, so a gap never outlives its
/// difference and never hides another.
/// </summary>
/// <param name="Name">What differs, in the words its closing row uses.</param>
/// <param name="Daoris">What of the Daoris path's observations the gap accounts for.</param>
/// <param name="Layer">What of the layer path's observations the gap accounts for.</param>
internal sealed record ParityGap(
    string Name, Func<IReadOnlyList<Observed>, IEnumerable<Observed>> Daoris, Func<IReadOnlyList<Observed>, IEnumerable<Observed>> Layer)
{
    public override string ToString() => Name;

    /// <summary>Nothing on the path.</summary>
    public static IEnumerable<Observed> None(IReadOnlyList<Observed> seen) => [];

    /// <summary>
    /// The two observations with every gap's part set apart, each gap having set something apart; throws naming a gap that set nothing.
    /// </summary>
    public static (ParityObservation Daoris, ParityObservation Layer) Apart(
        ParityObservation daoris, ParityObservation layer, IReadOnlyList<ParityGap> gaps)
    {
        var (mine, theirs) = (new HashSet<Observed>(ReferenceEqualityComparer.Instance), new HashSet<Observed>(ReferenceEqualityComparer.Instance));
        foreach (var gap in gaps)
        {
            var (d, l) = (gap.Daoris(daoris.Seen).ToList(), gap.Layer(layer.Seen).ToList());
            if (d.Count + l.Count == 0) throw new Xunit.Sdk.XunitException($"the gap `{gap.Name}` set nothing apart: it is closed, so its name goes.");
            mine.UnionWith(d);
            theirs.UnionWith(l);
        }

        return (daoris.With(seen => seen.Where(each => !mine.Contains(each))), layer.With(seen => seen.Where(each => !theirs.Contains(each))));
    }
}
