namespace Daoris.Driver;

/// <summary>
/// The words a conflict of meaning is told in (INTEGRATE1e part e3b; the integration design §5.4): the conflicted files, the line's commits that
/// changed them since the member's work began, and the member of the same candidate whose work it met, where it met one. They are the
/// <c>conflicts</c> sentence <see cref="LandingSyncWords.Rebase"/> puts in the landing's own rebase words, so a candidate's member is asked as an
/// automatic landing's session is (<see cref="RebaseTelling"/>, keyed by the session and the tip), and the person reads the same sentence where
/// the member is held.
/// </summary>
internal static class IntegrationConflicts
{
    /// <summary>How many files are named: a conflict in more says how many more.</summary>
    private const int FilesAtMost = 20;

    /// <summary>How many of the line's commits are named.</summary>
    private const int CommitsAtMost = 3;

    /// <summary>
    /// The sentence for <paramref name="paths"/>, which the member <paramref name="tip"/> conflicted in on the candidate: the files, then the commits
    /// the line made to them since the work began (the line is <paramref name="lineBase"/>, the commit the candidate was formed on), then the
    /// earlier replayed members whose merge changed them. git unable to say leaves that part out: the files are what the session needs.
    /// </summary>
    /// <param name="earlier">The members replayed before this one, each by its branch and the merge commit its replay made.</param>
    internal static async Task<string> DescribeAsync(
        string tree, string lineBase, string tip, IReadOnlyList<string> paths, IReadOnlyList<(string Branch, string Replayed)> earlier, CancellationToken ct)
    {
        var files = string.Join(", ", paths.Take(FilesAtMost).Select(path => $"`{path}`")) + (paths.Count > FilesAtMost ? $" and {paths.Count - FilesAtMost} more" : "");
        var commits = new List<string>();
        var (based, mergeBase, _) = await WorkingTree.GitAsync(tree, ["merge-base", tip, lineBase], ct).ConfigureAwait(false);
        if (based == 0 && mergeBase.Trim() is { Length: > 0 } since)
        {
            var (logged, log, _) = await WorkingTree.GitAsync(
                tree, ["--no-optional-locks", "log", "--no-merges", "-n", $"{CommitsAtMost}", "--format=%h %s", $"{since}..{lineBase}", "--", .. paths], ct).ConfigureAwait(false);
            if (logged == 0)
            {
                foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var space = line.IndexOf(' ');
                    commits.Add(space < 0 ? $"`{line}`" : $"`{line[..space]}` “{line[(space + 1)..]}”");
                }
            }
        }

        var met = new List<string>();
        foreach (var (branch, replayed) in earlier)
        {
            var (diffed, names, _) = await WorkingTree.GitAsync(
                tree, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "-z", $"{replayed}^1", replayed], ct).ConfigureAwait(false);
            if (diffed == 0 && names.Split('\0', StringSplitOptions.RemoveEmptyEntries).Any(name => paths.Contains(name))) met.Add($"`{branch}`");
        }

        return files
            + (commits.Count == 0 ? "" : $", which the line's own commits changed since your work began ({string.Join(", ", commits)})")
            + (met.Count == 0 ? "" : $", and which the work of {string.Join(" and ", met)}, replayed before yours in the same candidate, changed too")
            + ".";
    }
}
