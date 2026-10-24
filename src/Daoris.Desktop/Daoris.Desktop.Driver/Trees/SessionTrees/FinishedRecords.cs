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
/// The steps after the hold (KEEPSIZE1b, the keep-size design §3.5 steps 6 and 7, and its resume): at each Keeping pass before the purge,
/// and at the press before it lists. Outside every repository's hold, reading git read-only (§3.4's <i>Git outside the hold</i>) in the root
/// the registry gives. For each schema-2 manifest beside its folder:
/// <list type="bullet">
/// <item><c>judged</c>, <c>moved</c> or <c>refused</c>: where its branch is absent and the holding check passes, <c>deleted</c>, then on;
/// otherwise it waits its days and says why.</item>
/// <item><c>deleted</c>: its record composed from the manifest and the tip (<c>git log --first-parent --numstat</c>) and posted; answered,
/// <c>recorded</c>; not, it stays <c>deleted</c> and the next pass, the press or a restart posts it again.</item>
/// <item><c>recorded</c>, its hour come: the holding check again; failing, <c>unmarked</c>, waiting its days.</item>
/// </list>
/// </summary>
internal sealed class FinishedRecords(TreeSetAside setAside)
{
    /// <summary>One pass. <paramref name="rootOf"/> is the registry's checkout of a workspace's repository; <paramref name="post"/> the service's door, null where none answers.</summary>
    /// <param name="post">Null answer: the service keeps the record; else why it does not.</param>
    /// <param name="only">The folders a press listed; null meets every one.</param>
    internal async Task<IReadOnlyList<FinishedStep>> RunAsync(
        Func<string, string, string?> rootOf, Func<FinishedPost, CancellationToken, Task<string?>>? post, DateTimeOffset now, CancellationToken ct,
        Func<string, bool>? only = null)
    {
        var steps = new List<FinishedStep>();
        foreach (var place in setAside.SetAsideRoots.Where(Directory.Exists))
        foreach (var workspace in SetAsideEligibility.Workspaces(place))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var file in Directory.EnumerateFiles(repository, "*" + SlimManifest.Suffix).ToList())
        {
            var folder = SlimManifest.FolderOf(file)!;
            if (only is not null && !only(folder)) continue;
            if (!Directory.Exists(folder) || SlimManifest.Read(file).Manifest is not { Finished: { } finished } manifest) continue;
            var root = rootOf(finished.Workspace, manifest.Repository);
            var state = manifest.State;
            if (state is SlimStates.Judged or SlimStates.Moved or SlimStates.Refused)
            {
                if (await SetAsideEligibility.HoldingAsync(root, manifest, ct).ConfigureAwait(false) is { } waits)
                {
                    if (manifest.Why != waits && Save(file, manifest with { Why = waits })) steps.Add(new(folder, state, state, waits));
                    continue;
                }

                manifest = manifest with { State = SlimStates.Deleted, Deleted = now, Why = null };
                if (!Save(file, manifest)) continue;
                steps.Add(new(folder, state, SlimStates.Deleted, null));
                state = SlimStates.Deleted;
            }

            if (state == SlimStates.Deleted)
            {
                if (post is null) continue;
                var (commits, unread) = root is null
                    ? (null, "the registry names no checkout of its repository on this machine")
                    : await CommitsAsync(root, finished, ct).ConfigureAwait(false);
                var moved = TreeSetAside.MovedAt(Path.GetFileName(folder)) ?? manifest.Moved ?? manifest.Judged;
                string? refused;
                try
                {
                    refused = await post(new FinishedPost(
                        finished.Workspace, manifest.Repository, Path.GetFileName(folder), moved, manifest, finished, commits, unread), ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or IOException or TaskCanceledException)
                {
                    refused = error.Message;
                }

                if (refused is not null)
                {
                    if (manifest.Why != refused && Save(file, manifest with { Why = refused })) steps.Add(new(folder, state, state, refused));
                    continue;
                }

                if (Save(file, manifest with { State = SlimStates.Recorded, Recorded = now, Why = null })) steps.Add(new(folder, state, SlimStates.Recorded, null));
                continue;
            }

            // The hour come, the holding check again right before the purge reads the class: a ref dropped by hand, a line rewound, unmarks it.
            if (state == SlimStates.Recorded && manifest.Recorded is { } recorded && now - recorded >= KeepKinds.SetAsideSettle && !finished.Whole
                && await SetAsideEligibility.HoldingAsync(root, manifest, ct).ConfigureAwait(false) is { } failed)
            {
                if (Save(file, manifest with { State = SlimStates.Unmarked, Why = failed })) steps.Add(new(folder, state, SlimStates.Unmarked, failed));
            }
        }

        return steps;
    }

    /// <summary>
    /// The tree's commits (§2.1): <c>git log --first-parent --numstat &lt;grewFrom&gt;..&lt;tip&gt;</c> from the tip, oldest first, a merge of the line
    /// one commit of the tree's with its first-parent changes. Null with why where the tip no longer reads or the range is not known.
    /// </summary>
    internal static async Task<(IReadOnlyList<FinishedCommit>? Commits, string? Unread)> CommitsAsync(string root, FinishedTree tree, CancellationToken ct)
    {
        var (tipCode, _, _) = await WorkingTree.GitAsync(root, ["cat-file", "-e", $"{tree.Tip}^{{commit}}"], ct).ConfigureAwait(false);
        if (tipCode != 0) return (null, "its tip no longer reads, so its commits are not listed");
        if (tree.GrewFrom is not { } grew) return (null, "the commit it grew from is not known, so its commits are not listed");
        var (code, output, err) = await WorkingTree.GitAsync(
            root, ["log", "--first-parent", "--numstat", "--format=%x1e%H%x1f%s", $"{grew}..{tree.Tip}"], ct).ConfigureAwait(false);
        if (code != 0) return (null, $"git could not list its commits: {TreeGit.FirstLine(err)}");
        var commits = new List<FinishedCommit>();
        foreach (var chunk in output.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0 || lines[0].Split('\u001f', 2) is not [var id, var subject]) continue;
            var files = new List<(string, int, int)>();
            foreach (var line in lines.Skip(1))
            {
                if (line.Split('\t', 3) is not [var added, var removed, var path]) continue;
                files.Add((path, int.TryParse(added, out var a) ? a : -1, int.TryParse(removed, out var r) ? r : -1));
            }

            commits.Add(new(id, subject, files));
        }

        commits.Reverse();
        return (commits, null);
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
