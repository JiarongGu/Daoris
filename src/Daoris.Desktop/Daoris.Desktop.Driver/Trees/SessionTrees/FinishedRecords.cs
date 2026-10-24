namespace Daoris.Driver;

/// <summary>
/// A finished tree's record as the driver posts it (KEEPSIZE1b, the keep-size design §2.2): the manifest's facts and the commits read from
/// the tip, oldest first; <see cref="Commits"/> null where they could not be read, and <see cref="Unread"/> says why.
/// </summary>
internal sealed record FinishedPost(
    string Workspace, string Repository, string Folder, DateTimeOffset Moved, SlimManifest Manifest, FinishedTree Tree,
    IReadOnlyList<FinishedCommit>? Commits, string? Unread);

/// <summary>One commit of a finished tree's, by <c>--first-parent</c>: its id, subject and files, each with the lines added and removed (-1 where git counts none).</summary>
internal sealed record FinishedCommit(string Id, string Subject, IReadOnlyList<(string Path, int Added, int Removed)> Files);

/// <summary>What one record pass did of one folder (§3.5): resumed, recorded, still waiting, or unmarked, with why.</summary>
internal sealed record FinishedStep(string Folder, string From, string To, string? Why);

/// <summary>
/// Why the service did not keep a record: its words, and whether it has no door for one at all (a service older than this driver, which
/// answers 404). Either way the folder stays <c>deleted</c>, awaiting its record past its days and said, and every later pass posts again
/// (Codex's read of the branch, finding 3): an upgraded service keeps it.
/// </summary>
internal sealed record RecordRefusal(string Why, bool NoDoor = false);

/// <summary>
/// The steps after the hold (KEEPSIZE1b, the keep-size design §3.5 steps 6 and 7, and its resume): at each Keeping pass before the purge,
/// and at the press's run before it removes. Outside every repository's hold, reading git read-only (§3.4's <i>Git outside the hold</i>) in
/// the root the registry gives. For each schema-2 manifest beside its folder:
/// <list type="bullet">
/// <item><c>judged</c>, <c>moved</c> or <c>refused</c>: where its branch is absent and the holding check passes, <c>deleted</c>, then on;
/// otherwise it waits its days and says why.</item>
/// <item><c>deleted</c>: its record composed from the manifest and the tip (<c>git log --first-parent --numstat</c>) and posted; answered,
/// <c>recorded</c> at the moment read after the service's answer (Codex's read of the branch, finding 5); not, it stays <c>deleted</c> and the
/// next pass, the press or a restart posts it again, a service with no door for it included.</item>
/// <item><c>recorded</c>, its hour come: the holding check again; failing for certain, <c>unmarked</c>, waiting its days. Passing authorizes
/// nothing: the purge runs its own check right before each rename.</item>
/// </list>
/// <para>🔴 A registry that could not be read is not a registry that names no checkout (the second maker's read, finding 3): with none, no
/// step runs, so nothing is unmarked and no record is posted without its commits for an outage. A git read that failed posts nothing
/// either; only a tip git says is missing, or a range not known, is posted without its commits.</para>
/// <para>Within the pass's budget (finding 6): between folders, never inside one's steps.</para>
/// </summary>
internal sealed class FinishedRecords(TreeSetAside setAside)
{
    /// <summary>One pass. <paramref name="rootOf"/> is the registry's checkout of a workspace's repository, null where the registry did not read; <paramref name="post"/> the service's door, null where none answers.</summary>
    /// <param name="post">Null answer: the service keeps the record; else why it does not.</param>
    /// <param name="only">The folders a press listed; null meets every one.</param>
    /// <param name="spent">Whether the pass's budget is spent or a stop asked; the rest waits for the next pass.</param>
    /// <param name="clock">What time it is, read at each step as it ends: a record is <c>recorded</c> at the moment read after its answer.</param>
    internal async Task<IReadOnlyList<FinishedStep>> RunAsync(
        Func<string, string, string?>? rootOf, Func<FinishedPost, CancellationToken, Task<RecordRefusal?>>? post, Func<DateTimeOffset> clock,
        CancellationToken ct, Func<string, bool>? only = null, Func<bool>? spent = null)
    {
        var steps = new List<FinishedStep>();
        if (rootOf is null) return steps;
        foreach (var place in setAside.SetAsideRoots.Where(Directory.Exists))
        foreach (var workspace in SetAsideEligibility.Workspaces(place))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var file in Directory.EnumerateFiles(repository, "*" + SlimManifest.Suffix).ToList())
        {
            if (spent?.Invoke() == true || ct.IsCancellationRequested) return steps;
            var folder = SlimManifest.FolderOf(file)!;
            if (only is not null && !only(folder)) continue;
            if (!Directory.Exists(folder) || SlimManifest.Read(file).Manifest is not { Finished: { } finished } manifest) continue;
            var root = rootOf(finished.Workspace, manifest.Repository);
            var state = manifest.State;
            if (state is SlimStates.Judged or SlimStates.Moved or SlimStates.Refused)
            {
                var (held, waits) = await SetAsideEligibility.HoldingAsync(root, manifest, ct).ConfigureAwait(false);
                if (held != Holding.Passed)
                {
                    if (manifest.Why != waits && Save(file, manifest with { Why = waits })) steps.Add(new(folder, state, state, waits));
                    continue;
                }

                manifest = manifest with { State = SlimStates.Deleted, Deleted = clock(), Why = null };
                if (!Save(file, manifest)) continue;
                steps.Add(new(folder, state, SlimStates.Deleted, null));
                state = SlimStates.Deleted;
            }

            if (state == SlimStates.Deleted)
            {
                if (post is null) continue;
                var (commits, unread, failedRead) = root is null
                    ? (null, "the registry names no checkout of its repository on this machine", null)
                    : await CommitsAsync(root, finished, ct).ConfigureAwait(false);
                if (failedRead is not null)
                {
                    // A git read that failed is no "no commits": the record waits, whole, for the next pass.
                    if (manifest.Why != failedRead && Save(file, manifest with { Why = failedRead })) steps.Add(new(folder, state, state, failedRead));
                    continue;
                }

                var moved = TreeSetAside.MovedAt(Path.GetFileName(folder)) ?? manifest.Moved ?? manifest.Judged;
                RecordRefusal? refused;
                try
                {
                    refused = await post(new FinishedPost(
                        finished.Workspace, manifest.Repository, Path.GetFileName(folder), moved, manifest, finished, commits, unread), ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or IOException or TaskCanceledException)
                {
                    refused = new(error.Message);
                }

                if (refused is not null)
                {
                    if (manifest.Why != refused.Why && Save(file, manifest with { Why = refused.Why })) steps.Add(new(folder, state, state, refused.Why));
                    continue;
                }

                // The hour runs from the moment read after the service's answer, never from before the pass's git and the post.
                if (Save(file, manifest with { State = SlimStates.Recorded, Recorded = clock(), Why = null })) steps.Add(new(folder, state, SlimStates.Recorded, null));
                continue;
            }

            // The hour come, the holding check again right before the purge reads the class: a ref dropped by hand, a line rewound, unmarks it;
            // a check that passes stamps its moment, the only one the hour's purge takes the folder on (finding 1).
            if (state == SlimStates.Recorded && manifest.Recorded is { } recorded && clock() - recorded >= KeepKinds.SetAsideSettle && !finished.Whole)
            {
                var (held, why) = await SetAsideEligibility.HoldingAsync(root, manifest, ct).ConfigureAwait(false);
                if (held == Holding.Failed)
                {
                    if (Save(file, manifest with { State = SlimStates.Unmarked, Why = why })) steps.Add(new(folder, state, SlimStates.Unmarked, why));
                }
                else if (held == Holding.Unread && manifest.Why != why && Save(file, manifest with { Why = why }))
                {
                    // Codex's read of the design, finding 3: unread is never certain; it keeps the folder recorded, said, and is asked again.
                    steps.Add(new(folder, state, state, why));
                }
            }
        }

        return steps;
    }

    /// <summary>
    /// The tree's commits (§2.1): <c>git log --first-parent --numstat &lt;grewFrom&gt;..&lt;tip&gt;</c> from the tip, oldest first, a merge of the line
    /// one commit of the tree's with its first-parent changes. Null with why where git says the tip is missing or the range is not known; a
    /// read git could not make is <c>Failed</c>, and nothing is posted on it.
    /// </summary>
    internal static async Task<(IReadOnlyList<FinishedCommit>? Commits, string? Unread, string? Failed)> CommitsAsync(
        string root, FinishedTree tree, CancellationToken ct)
    {
        // `cat-file -e <full id>`: 1 is git's word that the object is missing; any other failure is no answer (git-ref-absence.md).
        var (tipCode, _, tipErr) = await WorkingTree.GitAsync(root, ["cat-file", "-e", tree.Tip], ct).ConfigureAwait(false);
        if (tipCode == 1) return (null, "its tip no longer reads, so its commits are not listed", null);
        if (tipCode != 0) return (null, null, $"git could not say whether its tip reads: {TreeGit.FirstLine(tipErr)}");
        if (tree.GrewFrom is not { } grew) return (null, "the commit it grew from is not known, so its commits are not listed", null);
        // Codex's read of the branch, finding 10: `-z` with no rename detection, so each file is `added\tremoved\tpath\0` and its path is git's
        // own bytes, never quoted or escaped for display; the header is `\x1e<id>\x1f<subject>`, its subject ending at the first line break.
        var (code, output, err) = await WorkingTree.GitAsync(
            root, ["-c", "core.quotePath=false", "log", "-z", "--no-renames", "--first-parent", "--numstat", "--format=%x1e%H%x1f%s", $"{grew}..{tree.Tip}"], ct)
            .ConfigureAwait(false);
        if (code != 0) return (null, null, $"git could not list its commits: {TreeGit.FirstLine(err)}");
        var commits = new List<FinishedCommit>();
        foreach (var chunk in output.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
        {
            var header = chunk.IndexOfAny(['\n', '\0']) is var end and >= 0 ? chunk[..end] : chunk;
            if (header.Split('\u001f', 2) is not [var id, var subject]) continue;
            var files = new List<(string, int, int)>();
            foreach (var entry in chunk[header.Length..].Split('\0'))
            {
                if (entry.TrimStart('\n').Split('\t', 3) is not [var added, var removed, var path] || path.Length == 0) continue;
                files.Add((path, int.TryParse(added, out var a) ? a : -1, int.TryParse(removed, out var r) ? r : -1));
            }

            commits.Add(new(id.Trim(), subject, files));
        }

        commits.Reverse();
        return (commits, null, null);
    }

    private static bool Save(string file, SlimManifest manifest)
    {
        try
        {
            manifest.Write(file);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
