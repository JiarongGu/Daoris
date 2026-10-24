namespace Daoris.Driver;

/// <summary>
/// A session branch's work a landing replayed onto the line (REVIEWLANDED1, D102's note): the line had moved, or the landing's records
/// conflicts were resolved by hand, so the landing replayed the session's commits under ids of their own and merged them, and the
/// session's own commits are on no branch by ancestry. <c>trees land --plan</c> read them landed by the landing's own per-commit reading
/// (<see cref="LineHolds.JudgeAsync"/>: the walk by content, then each commit's patch id), while the review row and the removal judged by
/// ancestry and kept the tree. This is that reading as a form of the content proof (<see cref="ContentHolds.HeldByContentAsync"/>), asked
/// after the files and before SQUASHSEEN1's merge, so every door that asks the proof reads it alike.
/// </summary>
/// <remarks>
/// <para><b>Every commit on the line</b>: the reading leaves none of the branch's commits for a landing to carry. A commit it keeps is work
/// the line lacks, and the proof names those commits (<see cref="ContentProof.Lacks"/>), so a session told to rebase after its landing says
/// its new commits as work to land, never its replayed ones.</para>
/// <para><b>The line as it stands, never a commit it passed</b> (D102, D148): a file the branch changed that reads otherwise on the line's tip
/// is held only where the line's newest change to it is a commit holding one of the branch's patches. A line that took the patch and
/// changed or reverted the file since does not hold it, as a commit the line holds only by patch is never an end.</para>
/// </remarks>
internal static class ReplayHolds
{
    /// <summary>What the per-commit reading found: every commit on the line (<paramref name="Held"/>), or the commits the line lacks.</summary>
    /// <param name="Lacks">The commits a landing would carry, oldest first: never empty; null where all are held, or none is lacked.</param>
    internal sealed record Reading(bool Held, IReadOnlyList<LandingCommit>? Lacks);

    /// <summary>
    /// Whether the line at <paramref name="lineTip"/> holds every commit of <paramref name="tip"/> beyond it, as a landing reads them, and has
    /// not changed since a file the branch changed that reads otherwise there. Null where git could not say: this form only adds holds, so
    /// an unread reading leaves the other forms and their words as they were.
    /// </summary>
    /// <param name="line">The repository's line, for the walk by content.</param>
    /// <param name="differs">The files the branch changed since it left the line that read otherwise on <paramref name="lineTip"/>.</param>
    internal static async Task<Reading?> ReadAsync(
        string cwd, string tip, string lineTip, string? line, IReadOnlyList<string> differs, CancellationToken ct)
    {
        if (await LineHolds.JudgeAsync(cwd, tip, lineTip, line, ct).ConfigureAwait(false) is not { } held || held.Commits.Count == 0) return null;
        if (held.Kept.Count > 0) return new(false, held.Kept);
        if (differs.Count == 0) return new(true, null);

        // The line's own commits since the branch left it, newest first, each marked `=` where it holds one of the branch's patches, with
        // the files it changed: one log. Newest by the graph, never by date: commits made in one second list in either order by date.
        var (code, log, _) = await WorkingTree.GitAsync(
            cwd,
            [
                "-c", "core.quotePath=false", "log", "--topo-order", "--cherry-mark", "--left-only", "--no-merges", "--no-renames", "--format=%x1e%m%H",
                "--name-only", $"{lineTip}...{tip}",
            ],
            ct).ConfigureAwait(false);
        return code != 0 ? null : new(LastChangedByPatch(log, differs), null);
    }

    /// <summary>
    /// Whether the newest commit of <paramref name="log"/> (git's <c>%x1e%m%H</c> then <c>--name-only</c>, newest first) changing each of
    /// <paramref name="differs"/> is one git marks <c>=</c>, holding one of the branch's patches; false where another changed it, or none.
    /// </summary>
    internal static bool LastChangedByPatch(string log, IReadOnlyList<string> differs)
    {
        var newest = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var entry in log.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = entry.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0 || lines[0].Length < 2) continue;
            foreach (var path in lines.Skip(1)) newest.TryAdd(path, lines[0][0] == '=');
        }

        return differs.All(path => newest.TryGetValue(path, out var patch) && patch);
    }
}
