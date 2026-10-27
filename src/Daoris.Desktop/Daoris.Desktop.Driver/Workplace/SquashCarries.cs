namespace Daoris.Driver;

/// <summary>
/// A session branch's work a squash carried with other work (SQUASHSEEN1, D102's note): the parent landed two sessions' work on the line
/// as one squashed commit, so neither branch's tree is that commit's tree, and the files they both changed (the backlog, the records, a
/// generated index) read otherwise on the line than on either branch. SQUASHTIDY1's two proofs saw neither, so both sessions were told to
/// rebase onto the line, ran again and found everything there. This is the third form of the content proof
/// (<see cref="ContentHolds.HeldByContentAsync"/>), the one reading every door asks: merging the branch into the line as it stands adds
/// nothing.
/// </summary>
/// <remarks>
/// <para><b>Merging adds nothing</b>: <c>git merge-tree</c> of the branch onto the line's tip, from where the branch left the line, writes
/// the tip's own tree, the repository's own merge attributes applied (its records merge by union), except in files the line marks
/// generated (the landing takes the line's copy of them, LANDREBASE1) and the declared backlog where the line holds every row the branch
/// added or removed (<see cref="RowsHeld"/>). A conflict in any other file, or any other file the merge would change, is work the line
/// lacks: not held. A branch that changed only generated files has nothing of its own to find.</para>
/// <para><b>The line as it stands, never a commit it passed</b> (D102, D148): a line that took the work and changed or reverted it since
/// does not hold it, as a commit the line holds only by patch is never an end.</para>
/// </remarks>
internal static class SquashCarries
{
    /// <summary>
    /// Whether merging <paramref name="tip"/> into <paramref name="lineTip"/> from <paramref name="mergeBase"/> adds nothing.
    /// </summary>
    /// <remarks>
    /// A read git could not make finds nothing here, as a trial git cannot make says nothing (<see cref="LineHolds.TrialAsync"/>): this
    /// form only adds holds, so a git too old to merge in memory leaves SQUASHTIDY1's proofs and their words as they were.
    /// </remarks>
    /// <param name="paths">The files the branch changed since <paramref name="mergeBase"/>, none of them a rename.</param>
    internal static async Task<bool> HeldAsync(
        string cwd, string tip, string mergeBase, string lineTip, IReadOnlyList<string> paths, CancellationToken ct)
    {
        if (await GeneratedPaths.MarkedAsync(cwd, paths, ct, source: lineTip).ConfigureAwait(false) is not { } generated) return false;
        return paths.Any(path => !generated.Contains(path)) && await AddsNothingAsync(cwd, tip, mergeBase, lineTip, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether merging <paramref name="tip"/> onto <paramref name="onto"/> from <paramref name="mergeBase"/> adds nothing to it, generated
    /// files and the declared backlog's rows aside; false where git could not say.
    /// </summary>
    private static async Task<bool> AddsNothingAsync(string cwd, string tip, string mergeBase, string onto, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            cwd, ["merge-tree", "--write-tree", "--name-only", "--no-messages", $"--merge-base={mergeBase}", onto, tip], ct).ConfigureAwait(false);
        var written = output.Split('\n', 2)[0].Trim();
        if (code is not (0 or 1) || written.Length == 0) return false;
        if (await DifferingAsync(cwd, onto, written, ct).ConfigureAwait(false) is not { } differing) return false;
        if (differing.Count == 0) return true;

        var backlog = await BacklogMerge.DeclaredAsync(cwd, onto, ct).ConfigureAwait(false);
        if (backlog is null || differing is not [var only] || only != backlog) return false;
        var texts = new List<string>(3);
        foreach (var side in new[] { mergeBase, onto, tip })
        {
            var (read, text, _) = await WorkingTree.GitAsync(cwd, ["cat-file", "blob", $"{side}:{backlog}"], ct).ConfigureAwait(false);
            if (read != 0) return false;
            texts.Add(text);
        }

        return RowsHeld(texts[0], texts[1], texts[2]);
    }

    /// <summary>
    /// Whether <paramref name="line"/> holds every row the branch added to or removed from the backlog since <paramref name="base"/>: a
    /// row whose count the branch changed reads on the line as many times as on the branch. Rows are the backlog's non-blank lines, and
    /// the line's own changes to other rows are its own.
    /// </summary>
    /// <remarks>
    /// The backlog by rows rather than by merge (LANDBACKLOG1's <see cref="BacklogMerge.ByBase"/> leaves a base line both sides changed as
    /// a conflict): two sessions that each moved out a row next to the other's leave one hunk on the squash that covers both, which no
    /// merge settles, though every row each moved is moved on the line.
    /// </remarks>
    internal static bool RowsHeld(string @base, string line, string branch)
    {
        var (before, ours, theirs) = (Rows(@base), Rows(line), Rows(branch));
        return before.Keys.Concat(theirs.Keys).Distinct(StringComparer.Ordinal)
            .Where(row => before.GetValueOrDefault(row) != theirs.GetValueOrDefault(row))
            .All(row => ours.GetValueOrDefault(row) == theirs.GetValueOrDefault(row));

        static Dictionary<string, int> Rows(string text) =>
            text.Split('\n').Select(each => each.TrimEnd('\r')).Where(each => each.Trim().Length > 0)
                .GroupBy(each => each, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    /// <summary>The files <paramref name="tree"/> holds otherwise than <paramref name="onto"/>, generated ones aside; null where git could not say.</summary>
    private static async Task<IReadOnlyList<string>?> DifferingAsync(string cwd, string onto, string tree, CancellationToken ct)
    {
        if (await LandedProofs.NamesAsync(cwd, onto, tree, ct).ConfigureAwait(false) is not { } names) return null;
        if (names.Count == 0) return names;
        return await GeneratedPaths.MarkedAsync(cwd, names, ct, source: onto).ConfigureAwait(false) is { } generated
            ? [.. names.Where(name => !generated.Contains(name))]
            : null;
    }
}
