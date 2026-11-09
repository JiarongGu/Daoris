namespace Daoris.Driver;

/// <summary>
/// A heavy run's admission line naming the previous line's build bases its tree holds unreleased (the tree-pool design §37.3, 3,
/// §38.2, 7): <c>daoris gate run</c> runs what it is given and refuses nothing, and builds no base first, so what it runs over one
/// without building reads that build until the door releases it.
/// </summary>
internal static class UnreleasedBases
{
    /// <summary>The line for the tree at <paramref name="tree"/>, or null where its record names no base.</summary>
    internal static string? Line(string home, string tree)
    {
        var bases = TreeFilesRecord.Read(home, tree)?.Bases ?? [];
        if (bases.Count == 0) return null;
        var one = bases.Count == 1;
        return $"run-heavy: {string.Join(", ", bases.Select(each => $"`{each.Path}`"))} {(one ? "is" : "are")} the previous line's build, unreleased; "
            + $"this run refuses nothing, so what it runs over {(one ? "it" : "them")} without building reads that build until "
            + $"`daoris tree build <path>` releases {(one ? "it" : "each")}.";
    }
}
