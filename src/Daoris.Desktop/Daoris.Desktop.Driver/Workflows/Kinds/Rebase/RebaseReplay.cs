using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>A work a <c>rebase</c> replays: its repository's checkout, the repository and workspace by name, its branch and the tip it is bound at.</summary>
public sealed record RebaseWork(string Root, string Repository, string Workspace, string Branch, string Tip);

/// <summary>What a <c>rebase</c> replays onto: the target's full ref and the tip of it the step is bound to.</summary>
public sealed record RebaseTarget(string Ref, string Tip)
{
    /// <summary>The target as words name it: a branch by its short name.</summary>
    public string Name => Ref.StartsWith("refs/heads/", StringComparison.Ordinal) ? Ref["refs/heads/".Length..] : Ref;
}

/// <summary>A path of the work's tree no commit carries, with the blob the tree held (null where the path is gone from it).</summary>
public sealed record RebaseUncommitted(string Path, string? Blob);

/// <summary>
/// What a <c>rebase</c> came to: its outcome (<see cref="RebaseOutcomes"/>, or <see cref="NodeStates.Failed"/> on a git error) and its words;
/// on <c>ready</c> the commit and the target's tip it stands on; on <c>conflicted</c> the stop, its commit and subject; on <c>uncommitted</c> the paths.
/// </summary>
public sealed record RebaseMade(string Outcome, string Said)
{
    public string? Commit { get; init; }

    public string? Onto { get; init; }

    /// <summary>The work's commits the target holds, left out of the replay: by ancestry, by patch, by a squash or by an operation that brought them in.</summary>
    public IReadOnlyList<string> LeftOut { get; init; } = [];

    public int Replayed { get; init; }

    public NodeStop? Stop { get; init; }

    public string? StopCommit { get; init; }

    public IReadOnlyList<RebaseUncommitted> Uncommitted { get; init; } = [];
}

/// <summary>
/// The <c>rebase</c> step's replay of a work onto a target, in a tree of Daoris's own (<see cref="ReplaySettles"/>): only what the target lacks
/// is replayed. A commit the target holds by ancestry or by patch, or one up to a tip in <c>held</c> (the squash its landing record names, the
/// tip a landed operation bound the work at as its member), is left out; a work wholly held is <c>ready</c> at the target's tip, and one
/// already standing on it at its own tip. The person's checkout, the work's branch and its tree are read and never written; nothing moves but
/// what the caller keeps of the commit made.
/// </summary>
internal sealed class RebaseReplay(ReplaySettles settles)
{
    /// <summary>Replays <paramref name="work"/> onto <paramref name="target"/>, its tree at <paramref name="tree"/> read first for uncommitted work.</summary>
    /// <param name="held">Tips whose commits the target holds by something other than ancestry: each left out with everything before it.</param>
    internal async Task<RebaseMade> ReplayAsync(RebaseWork work, string? tree, RebaseTarget target, IReadOnlyList<string> held, CancellationToken ct)
    {
        if (tree is not null && Directory.Exists(tree))
        {
            var (read, paths) = await UncommittedAsync(tree, ct).ConfigureAwait(false);
            if (read is not null) return new(NodeStates.Failed, read);
            if (paths.Count > 0)
            {
                return new(RebaseOutcomes.Uncommitted, $"Its tree holds work no commit carries: {Listed(paths.Select(each => each.Path))}.")
                {
                    Uncommitted = paths, Stop = new NodeStop(ProblemSorts.Uncommitted, null, [.. paths.Select(each => each.Path)]),
                };
            }
        }

        var root = work.Root;
        var (listed, own, listErr) = await WorkingTree.GitAsync(root, ["rev-list", "--reverse", work.Tip, $"^{target.Tip}", .. held.Select(tip => $"^{tip}")], ct)
            .ConfigureAwait(false);
        if (listed != 0) return new(NodeStates.Failed, $"git could not list its commits: {Failure(listErr)}");
        var mine = own.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        if (await LineHolds.ReadBeyondAsync(root, work.Tip, target.Tip, null, ct).ConfigureAwait(false) is not { } beyond)
        {
            return new(NodeStates.Failed, $"git could not compare its commits with `{target.Name}`.");
        }

        var kept = beyond.Where(each => !each.Held && mine.Contains(each.Sha)).Select(each => each.Sha).ToList();
        var leftOut = beyond.Select(each => each.Sha).Where(sha => !kept.Contains(sha)).ToList();
        if (kept.Count == 0)
        {
            return new(RebaseOutcomes.Ready, $"`{target.Name}` holds all of it: nothing to replay.") { Commit = target.Tip, Onto = target.Tip, LeftOut = leftOut };
        }

        if (leftOut.Count == 0 && await AncestorAsync(root, target.Tip, work.Tip, ct).ConfigureAwait(false))
        {
            return new(RebaseOutcomes.Ready, $"It stands on `{target.Name}`'s tip: nothing to replay.") { Commit = work.Tip, Onto = target.Tip };
        }

        LandingCommit[] commits = [.. beyond.Select(each => new LandingCommit(each.Sha, ""))];
        var made = await settles.ReplayKeptAsideAsync(root, work.Workspace, work.Repository, target.Tip, commits, [.. commits.Where(each => kept.Contains(each.Sha))], ct)
            .ConfigureAwait(false);
        if (made.Now is { } now)
        {
            return new(RebaseOutcomes.Ready, $"Rebased {kept.Count} commit(s) onto `{target.Name}`" + Settled(made) + ".")
            {
                Commit = now, Onto = target.Tip, LeftOut = leftOut, Replayed = kept.Count,
            };
        }

        if (!made.Conflict) return new(NodeStates.Failed, $"Replaying it onto `{target.Name}` failed: {made.Words}");
        if (made.StoppedAt is not { } stop) return new(RebaseOutcomes.Conflicted, $"Could not settle the replay onto `{target.Name}`: {made.Words}") { LeftOut = leftOut };
        var subject = await SubjectAsync(root, stop.Commit, ct).ConfigureAwait(false);
        return new(RebaseOutcomes.Conflicted,
            $"Could not settle {Listed(stop.Paths)} at `{TextBounds.ShortCommit(stop.Commit, 7)}` “{subject}”: {made.Words}")
        {
            Onto = target.Tip, LeftOut = leftOut, StopCommit = stop.Commit,
            Stop = new NodeStop(ProblemSorts.Conflict, await PatchIdAsync(root, stop.Commit, ct).ConfigureAwait(false), [.. stop.Paths.Order(StringComparer.Ordinal)]),
        };
    }

    /// <summary>What the replay settled, said after its count.</summary>
    private static string Settled(ReplaySettles.KeptReplay made)
    {
        var said = new List<string>();
        if (made.Backlog is { } backlog) said.Add($"settled `{backlog.Path}` by its rows");
        said.AddRange(made.Tables.Select(table => $"settled `{table.Path}` by its base"));
        if (made.Generated.Count > 0) said.Add($"took {Listed(made.Generated)} at the target's copy");
        said.AddRange(made.Lined.Select(each => $"settled `{each.Path}` by its line rule"));
        if (made.Joined.Count > 0 || made.Once.Count > 0) said.Add("settled the union records");
        return (said.Count == 0 ? "" : "; " + string.Join(", ", said)) + (made.Departed.Length > 0 ? $" {made.Departed.Trim()}" : "");
    }

    /// <summary>The tree's paths no commit carries, each with its blob; or why it could not be read.</summary>
    private static async Task<(string? Unread, IReadOnlyList<RebaseUncommitted> Paths)> UncommittedAsync(string tree, CancellationToken ct)
    {
        var (code, status, err) = await WorkingTree.GitAsync(
            tree, ["--no-optional-locks", "-c", "core.quotePath=false", "status", "--porcelain", "--untracked-files=all"], ct).ConfigureAwait(false);
        if (code != 0) return ($"git could not read its tree: {Failure(err)}", []);
        var paths = new List<RebaseUncommitted>();
        foreach (var line in status.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line => line.Length > 3))
        {
            var path = line[3..].Trim();
            if (path.IndexOf(" -> ", StringComparison.Ordinal) is var arrow and >= 0) path = path[(arrow + 4)..];
            var (hashed, blob, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "hash-object", "--", path], ct).ConfigureAwait(false);
            paths.Add(new RebaseUncommitted(path, hashed == 0 ? blob.Trim() : null));
        }

        return (null, paths);
    }

    private static async Task<bool> AncestorAsync(string root, string ancestor, string of, CancellationToken ct) =>
        (await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", ancestor, of], ct).ConfigureAwait(false)).Code == 0;

    private static async Task<string> SubjectAsync(string root, string commit, CancellationToken ct)
    {
        var (code, subject, _) = await WorkingTree.GitAsync(root, ["log", "-1", "--format=%s", commit], ct).ConfigureAwait(false);
        return code == 0 ? subject.Trim() : "";
    }

    /// <summary>The stopped commit's patch id: the same stop met again after a rebind, whatever id its replay gave it.</summary>
    internal static async Task<string?> PatchIdAsync(string root, string commit, CancellationToken ct)
    {
        var (shown, patch, _) = await WorkingTree.GitAsync(root, ["show", "--format=", "--no-color", commit], ct).ConfigureAwait(false);
        if (shown != 0) return null;
        var (code, id, _) = await WorkingTree.GitAsync(root, ["patch-id", "--stable"], patch, ct).ConfigureAwait(false);
        return code == 0 && id.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [{ } first, ..] ? first : null;
    }

    private static string Listed(IEnumerable<string> paths)
    {
        var all = paths.ToList();
        return string.Join(", ", all.Take(3).Select(path => $"`{path}`")) + (all.Count > 3 ? $" and {all.Count - 3} more" : "");
    }
}
