namespace Daoris.Driver;

/// <summary>
/// What the files stage says of the tree's build bases (the tree-pool design §36.3, 4, §38.2, 3): each release the door made since the
/// last stage that set entries aside, said once; then the bases the record names, with the runners their rules' <c>refuses</c>
/// integrated with the guard, or the word that none refuses them, and that a command typed into a shell is not refused. The session is
/// told the same.
/// </summary>
internal static class TreeBaseWords
{
    internal static (IReadOnlyList<Noted> Said, string? Told) Say(string home, string tree, FileRules rules, FileFloorFacts floor)
    {
        if (TreeFilesRecord.Read(home, tree) is not { } record) return ([], null);
        var said = new List<Noted>();
        var unsaid = record.Released.Where(each => !each.Said && each.Trimmed > 0).ToList();
        said.AddRange(unsaid.Select(Trimmed));
        if (unsaid.Count > 0)
        {
            TreeFilesRecord.Change(home, tree, state => state with
            {
                Released = [.. state.Released.Select(each => unsaid.Any(one => one.Path == each.Path && one.At == each.At) ? each with { Said = true } : each)],
            });
        }

        if (record.Bases.Count == 0) return (said, null);
        IReadOnlyList<string> bases = [.. record.Bases.Select(each => each.Path)];
        IReadOnlyList<string> runners = [.. record.Bases.SelectMany(each => rules.For(each.Path, floor).Rule?.Refuses ?? []).Distinct(StringComparer.Ordinal)];
        said.Add(runners.Count > 0 ? Guarded(bases, runners) : Unguarded(bases));
        return (said, Told(bases, runners));
    }

    private static string Listed(IEnumerable<string> paths) => string.Join(", ", paths.Select(path => $"`{path}`"));

    private const string Kept = "hold the previous line's build, kept so this line's build copies only what changed. Run `daoris tree build <path>` on one "
        + "before you run or read what is under it: that build releases it and sets aside what it does not own.";

    private static Noted Guarded(IReadOnlyList<string> bases, IReadOnlyList<string> runners) => Noted.Of(
        StartNoteCodes.StartedTreeBase,
        $"{Listed(bases)} {Kept} Until then {Listed(runners)} refuse it; a command typed into a shell is not refused, and reads the previous line's build.",
        ("bases", bases), ("runners", runners));

    private static Noted Unguarded(IReadOnlyList<string> bases) => Noted.Of(
        StartNoteCodes.StartedTreeBaseUnguarded,
        $"{Listed(bases)} {Kept} Nothing in this repository refuses it until then, so a command run over it reads the previous line's build.",
        ("bases", bases));

    private static Noted Trimmed(TreeBaseRelease release) => Noted.Of(
        StartNoteCodes.TreeBaseTrimmed,
        $"Daoris's build of `{release.Project}` released `{release.Path}` and set aside {release.Trimmed} of the entries under it that its build does not own, "
            + "with the previous line's files.",
        ("path", release.Path), ("project", release.Project), ("count", release.Trimmed));

    private static string Told(IReadOnlyList<string> bases, IReadOnlyList<string> runners) =>
        $"Daoris kept the previous line's build in {Listed(bases)}, so your build copies only what changed. Before you run or read anything under one, "
        + "run `daoris tree build <path>` on it: it builds that project under the machine's admission and releases it. "
        + (runners.Count > 0 ? $"{Listed(runners)} refuse it until then. " : "")
        + "A command you type into a shell is not refused, and reads the previous line's build.";
}
