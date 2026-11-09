namespace Daoris.Driver;

/// <summary>
/// Where a dependency stands in install order: the place of the rule that decides it, a member the found layer named in a workspace
/// entry beside its root taking its root's place where a later layer decides the root. The found entry lists the root first and its
/// members after; a later entry naming the root alone would otherwise leave the members at the found entry's place, ahead of it, and a
/// member's install in its own folder installs the whole workspace before its root.
/// </summary>
internal static class WorkspaceOrder
{
    /// <summary>The place of <paramref name="path"/>, and whether it is a found member standing with its root's.</summary>
    internal static (int Place, bool Member) Of(IReadOnlyList<LayerSaid> layers, string path, bool ignoreCase)
    {
        var place = Decider(layers, path, ignoreCase);
        if (place < 0 || layers[place] is not { Layer: FileLayer.Found, Rule.Paths.Length: > 1 } entry) return (place, false);
        if (GitPattern.LiteralOf(entry.Rule, entry.Rule.Paths[0]) is not { } root || string.Equals(root, path, StringComparison.Ordinal)) return (place, false);
        return (Math.Max(place, Decider(layers, root, ignoreCase)), true);
    }

    private static int Decider(IReadOnlyList<LayerSaid> layers, string path, bool ignoreCase) =>
        layers.ToList().FindLastIndex(each => each.Rule.Kind == FileKind.Dependency && each.Rule.Matches(path, ignoreCase));
}
