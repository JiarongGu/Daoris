using Daoris.Contracts.Sessions;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A finished tree's record as the driver posts it: the manifest's facts and the commits read from
/// the tip, oldest first; <see cref="Commits"/> null where they could not be read, and <see cref="Unread"/> says why.
/// </summary>
internal sealed record FinishedPost(
    string Workspace, string Repository, string Folder, DateTimeOffset Moved, SlimManifest Manifest, FinishedTree Tree,
    IReadOnlyList<FinishedCommit>? Commits, string? Unread);

/// <summary>One commit of a finished tree's, by <c>--first-parent</c>: its id, subject and files, each with the lines added and removed (-1 where git counts none).</summary>
internal sealed record FinishedCommit(string Id, string Subject, IReadOnlyList<(string Path, int Added, int Removed)> Files);

/// <summary>What one record pass did of one folder: resumed, recorded, still waiting, or unmarked, with why.</summary>
internal sealed record FinishedStep(string Folder, string From, string To, string? Why);

/// <summary>
/// Why the service did not keep a record: its words, and whether it has no door for one at all (a service older than this driver, which
/// answers 404). Either way the folder stays <c>deleted</c>, awaiting its record past its days and said, and every later pass posts again:
/// an upgraded service keeps it.
/// </summary>
internal sealed record RecordRefusal(string Why, bool NoDoor = false);

/// <summary>
/// The steps after the hold (7, and its resume): at each Keeping pass before the purge,
/// and at the press's run before it removes. Outside every repository's hold, reading git read-only in
/// the root the registry gives. For each schema-2 manifest beside its folder:
/// <list type="bullet">
/// <item><c>judged</c>, <c>moved</c> or <c>refused</c>: where its branch is absent and the holding check passes, <c>deleted</c>, then on;
/// otherwise it waits its days and says why.</item>
/// <item><c>deleted</c>: its record composed from the manifest and the tip (<c>git log --first-parent --numstat</c>) and posted; answered,
/// <c>recorded</c> at the moment read after the service's answer; not, it stays <c>deleted</c> and the
/// next pass, the press or a restart posts it again, a service with no door for it included.</item>
/// <item><c>recorded</c>, its hour come: the holding check again; failing for certain, <c>unmarked</c>, waiting its days. Passing authorizes
/// nothing: the purge runs its own check right before each rename.</item>
/// </list>
/// <para>🔴 A registry that could not be read is not a registry that names no checkout: with none, no
/// step runs, so nothing is unmarked and no record is posted without its commits for an outage. A git read that failed posts nothing
/// either, nor does a repository the registry does not name; only a tip git says is missing, or a range
/// not known, is posted without its commits.</para>
/// <para>Within the pass's budget: between folders, never inside one's steps.</para>
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
                // 🔴 A repository the registry does not name is read as a git read that failed, never as one
                // with no commits, since the post is idempotent and a record kept without them stays so; it waits, said, past its days.
                var (commits, unread, failedRead) = root is null
                    ? (null, null, SetAsideEligibility.NoCheckout)
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

            // The hour come, the holding check again: a ref dropped by hand, a line rewound, unmarks it. One that passes authorizes nothing and
            // writes nothing: the purge runs its own right before each rename.
            if (state == SlimStates.Recorded && manifest.Recorded is { } recorded && clock() - recorded >= KeepKinds.SetAsideSettle && !finished.Whole)
            {
                var (held, why) = await SetAsideEligibility.HoldingAsync(root, manifest, ct).ConfigureAwait(false);
                if (held == Holding.Failed)
                {
                    if (Save(file, manifest with { State = SlimStates.Unmarked, Why = why })) steps.Add(new(folder, state, SlimStates.Unmarked, why));
                }
                else if (held == Holding.Unread && manifest.Why != why && Save(file, manifest with { Why = why }))
                {
                    // Unread is never certain; it keeps the folder recorded, said, and is asked again.
                    steps.Add(new(folder, state, state, why));
                }
            }
        }

        return steps;
    }

    /// <summary>
    /// The tree's commits: <c>git log --first-parent --numstat &lt;grewFrom&gt;..&lt;tip&gt;</c> from the tip, oldest first, a merge of the line
    /// one commit of the tree's with its first-parent changes. Null with why where git says the tip is missing or the range is not known; a
    /// read git could not make is <c>Failed</c>, and nothing is posted on it.
    /// </summary>
    internal static async Task<(IReadOnlyList<FinishedCommit>? Commits, string? Unread, string? Failed)> CommitsAsync(
        string root, FinishedTree tree, CancellationToken ct)
    {
        // Missing is git's word that the object is not here; no answer is not that (git-ref-absence.md).
        var tip = await GitObjectReads.InfoAsync(root, tree.Tip, ct).ConfigureAwait(false);
        if (tip is { Missing: true }) return (null, "its tip no longer reads, so its commits are not listed", null);
        if (tip is null) return (null, null, "git could not say whether its tip reads");
        if (tree.GrewFrom is not { } grew) return (null, "the commit it grew from is not known, so its commits are not listed", null);
        // `-z` with no rename detection, so each file is `added\tremoved\tpath\0` and its path is git's
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

/// <summary>
/// A finished tree's record as the service takes it (<see cref="FinishedPost"/>), posted through the service's client: the
/// workplace's own member of it, which names the manifest and the tree it carries, so the client names neither and every caller keeps
/// <c>service.PostWorkRecordAsync</c> as spelled, as a call or as the delegate a pass is handed.
/// </summary>
internal static class FinishedPosts
{
    extension(ServiceClient client)
    {
            /// <summary>
            /// Post a finished tree's record to <c>POST /api/trees/record</c>: the manifest's facts and the
            /// commits read from the tip. Null where the service keeps it, a second post of a kept identity included; else why it does not, in its
            /// words, a store a newer build stamped among them; a 404, a service with no door for it, says so (<see cref="RecordRefusal.NoDoor"/>).
            /// </summary>
        internal async Task<RecordRefusal?> PostWorkRecordAsync(FinishedPost record, CancellationToken ct = default)
            {
                var tree = record.Tree;
                var sent = await client.SendAsync(
                    new PostWorkRecord(
                        record.Workspace, record.Repository, record.Folder, tree.Tree, record.Moved.ToUniversalTime(), tree.Unit, record.Manifest.Branch, tree.Line,
                        new WorkRecordHoldWire(tree.Hold.Kind, tree.Hold.Where, tree.Hold.Form), tree.Tip, tree.GrewFrom, tree.Ref,
                        record.Commits is { } commits
                            ? [.. commits.Select(commit => new WorkRecordCommitWire(
                                commit.Id, commit.Subject, [.. commit.Files.Select(file => new WorkRecordFileWire(file.Path, file.Added, file.Removed))]))]
                            : null,
                        record.Unread, new WorkRecordLeftWire([.. tree.Left.Names], tree.Left.Count, tree.Left.Cut), tree.Whole,
                        // The landing captured before the move, or why none is named.
                        tree.Landing is { } landing
                            ? new WorkRecordLandingWire(landing.Branch, landing.Line, landing.At.ToUniversalTime(), landing.PullRequest)
                            : null,
                        tree.LandingWhy),
                    ct).ConfigureAwait(false);
                if (sent.Reply.Ok) return null;
                if (sent.Reply.Status == 404) return new("the service has no door for a finished tree's record, so its folder keeps its days", NoDoor: true);
                return new(RefusalWords.ErrorWords(sent.Reply.Payload) ?? sent.Reply.Payload);
            }
    }
}
