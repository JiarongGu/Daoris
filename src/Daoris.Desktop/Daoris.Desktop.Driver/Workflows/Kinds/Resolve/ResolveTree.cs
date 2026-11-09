using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// A tree handed to a resolver: the checkout, the tree on its branch at the tip it is handed at, the target's tip the rebase stopped onto,
/// the work's commits it left out, and the ref the resolver's unresolved edits are kept on.
/// </summary>
public sealed record ResolveHand(
    string Root, string Tree, string Repository, string Branch, string Tip, string Onto, IReadOnlyList<string> LeftOut, string EditsRef);

/// <summary>A file the resolver is asked to settle, with its hunks as the tree holds them: each the work's, the base's and the target's lines.</summary>
public sealed record ResolveFile(string Path, IReadOnlyList<string> Hunks);

/// <summary>
/// The stop a resolver is asked about: the stopped commit, its subject and patch id, the commit the replay stood on, and the files left
/// conflicted; the index's settled entries are what the proof holds every other path to.
/// </summary>
public sealed record ResolveStopAt(string Commit, string Subject, string? PatchId, string Head, IReadOnlyList<ResolveFile> Files)
{
    public IReadOnlyList<string> Paths => [.. Files.Select(file => file.Path)];

    /// <summary>The stop's identity, by which its tries are counted.</summary>
    public NodeStop Stop => new(ProblemSorts.Conflict, PatchId, Paths);

    /// <summary>Every settled index entry at the stop, path to mode and blob.</summary>
    internal IReadOnlyDictionary<string, string> Staged { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Where the replay in the resolver's tree stands after a step of the hand-off.</summary>
public static class ResolveSteps
{
    /// <summary>At a stop no rule settles: the resolver is asked.</summary>
    public const string Stopped = "stopped";

    /// <summary>Replayed to its end: the commit is the work onto the target.</summary>
    public const string Done = "done";

    /// <summary>The resolver's turn ended with the stop's files not yet settled.</summary>
    public const string Unresolved = "unresolved";

    /// <summary>The resolution changed a file the problem did not name.</summary>
    public const string Refused = "refused";

    /// <summary>git failed, or the tree is not as it is handed.</summary>
    public const string Failed = "failed";
}

/// <summary>A step of the hand-off: where the replay stands, its stop or its commit, and its words.</summary>
public sealed record ResolveStep(string Kind, string Said)
{
    public ResolveStopAt? At { get; init; }

    public string? Commit { get; init; }
}

/// <summary>
/// The git of a hand-off in the resolver's tree (§2.4 of the process steps' design): prepared at the stop on a detached head, so the tree's
/// branch never moves; read at the resolver's end, its resolution staged and gone on from with each later stop settled as the rebase settles
/// them; proved to change only the named files; and put back on its branch at its tip, what the resolver made kept on the edits ref first.
/// </summary>
internal sealed class ResolveTree(ReplaySettles settles)
{
    private static readonly string[] Begins = ["-c", "rebase.updateRefs=false", "-c", "rebase.autoStash=false", "-c", "merge.conflictStyle=diff3", "rebase", "--onto"];

    private static readonly string[] Continues = ["-c", "core.editor=:", "-c", "merge.conflictStyle=diff3", "rebase", "--continue"];

    /// <summary>
    /// The tree read as handed (clean, on its branch at its tip), then the work's kept commits replayed onto the target detached, each stop a
    /// rule settles gone on from, until the first it does not: there the rebase stands, what it settled staged and only its files conflicted.
    /// </summary>
    public async Task<ResolveStep> PrepareAsync(ResolveHand hand, CancellationToken ct)
    {
        if (await AsHandedAsync(hand, ct).ConfigureAwait(false) is { } unfit) return new(ResolveSteps.Failed, unfit);
        var (listed, own, listErr) = await WorkingTree.GitAsync(hand.Root, ["rev-list", "--reverse", hand.Tip, $"^{hand.Onto}"], ct).ConfigureAwait(false);
        if (listed != 0) return new(ResolveSteps.Failed, $"git could not list its commits: {Failure(listErr)}");
        var kept = own.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(sha => !hand.LeftOut.Contains(sha)).ToList();
        if (kept.Count == 0) return new(ResolveSteps.Done, "Nothing of it is left to replay.") { Commit = hand.Onto };
        var (moved, _, moveErr) = await WorkingTree.GitAsync(hand.Tree, ["switch", "--quiet", "--detach", hand.Tip], ct).ConfigureAwait(false);
        if (moved != 0) return new(ResolveSteps.Failed, $"git would not detach the tree: {Failure(moveErr)}");
        return await GoneOnAsync(hand, [.. Begins, hand.Onto, $"{kept[0]}^"], ct).ConfigureAwait(false);
    }

    /// <summary>The resolver's unresolved edits of an earlier ask, laid over the stop's files; nothing where none is kept.</summary>
    public static async Task ApplyEditsAsync(ResolveHand hand, ResolveStopAt at, CancellationToken ct)
    {
        var (has, _, _) = await WorkingTree.GitAsync(hand.Tree, ["rev-parse", "--verify", "--quiet", hand.EditsRef], ct).ConfigureAwait(false);
        if (has != 0) return;
        foreach (var path in at.Paths)
        {
            await WorkingTree.GitAsync(hand.Tree, ["checkout", hand.EditsRef, "--", path], ct).ConfigureAwait(false);
        }
    }

    /// <summary>The edits ref let go of once the stop it held edits for is settled.</summary>
    public static Task DropEditsAsync(ResolveHand hand, CancellationToken ct) =>
        WorkingTree.GitAsync(hand.Tree, ["update-ref", "-d", hand.EditsRef], ct);

    /// <summary>
    /// The tree at the resolver's end, HEAD included: the stop's files settled with no markers and nothing else changed, staged and gone on from
    /// (a commit or <c>rebase --continue</c> of the resolver's read as its edits), each later stop a rule settles settled, and the stop's commit
    /// proved to differ from what the rebase made only in the named files.
    /// </summary>
    public async Task<ResolveStep> ReadAsync(ResolveHand hand, ResolveStopAt at, CancellationToken ct)
    {
        var rebasing = await RebasingAsync(hand.Tree, ct).ConfigureAwait(false);
        var head = await CommitOfAsync(hand.Tree, "HEAD", ct).ConfigureAwait(false);
        var stoppedAt = rebasing ? await CommitOfAsync(hand.Tree, "REBASE_HEAD", ct).ConfigureAwait(false) : null;
        // At a later stop the resolver's own `rebase --continue` reached, that stop's files are the replay's to settle, not the resolver's.
        var atItsStop = rebasing && SameCommit(stoppedAt, at.Commit);
        if (!rebasing && (SameCommit(head, at.Head) || !await DescendsAsync(hand.Tree, at.Head, ct).ConfigureAwait(false)))
        {
            return new(ResolveSteps.Unresolved, "The replay was given up before its stop was settled.") { At = at };
        }

        if (atItsStop && await MarkedAsync(hand.Tree, at, ct).ConfigureAwait(false) is { } marked) return new(ResolveSteps.Unresolved, marked) { At = at };
        if ((atItsStop || !rebasing) && await OthersAsync(hand.Tree, at, ct).ConfigureAwait(false) is { Count: > 0 } others)
        {
            return new(ResolveSteps.Refused, Unnamed(others)) { At = at };
        }

        if (atItsStop && SameCommit(head, at.Head)) await WorkingTree.GitAsync(hand.Tree, ["add", "--", .. at.Paths], ct).ConfigureAwait(false);
        var step = rebasing
            ? await GoneOnAsync(hand, atItsStop ? Continues : null, ct).ConfigureAwait(false)
            : new ResolveStep(ResolveSteps.Done, "") { Commit = head };
        if (step.Kind is not (ResolveSteps.Done or ResolveSteps.Stopped)) return step;
        return await ProvedAsync(hand.Tree, at, ct).ConfigureAwait(false) is { Count: > 0 } changed ? new(ResolveSteps.Refused, Unnamed(changed)) { At = at } : step;
    }

    /// <summary>
    /// The tree put back as handed: where <paramref name="keepEdits"/>, what the resolver made (HEAD's commits since the stop, then the
    /// index and working files as a commit on top) kept on the edits ref; the rebase aborted; the tree on its branch at its tip. Null where it
    /// stands so; else why not.
    /// </summary>
    public static async Task<string?> RestoreAsync(ResolveHand hand, bool keepEdits, CancellationToken ct)
    {
        if (keepEdits && await RebasingAsync(hand.Tree, ct).ConfigureAwait(false)) await KeepEditsAsync(hand, ct).ConfigureAwait(false);
        if (await RebasingAsync(hand.Tree, ct).ConfigureAwait(false)) await WorkingTree.GitAsync(hand.Tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        await WorkingTree.GitAsync(hand.Tree, ["reset", "--quiet", "--hard"], ct).ConfigureAwait(false);
        await WorkingTree.GitAsync(hand.Tree, ["clean", "-fdq"], ct).ConfigureAwait(false);
        var (switched, _, err) = await WorkingTree.GitAsync(hand.Tree, ["switch", "--quiet", hand.Branch], ct).ConfigureAwait(false);
        if (switched != 0) return $"git would not put the tree back on `{hand.Branch}`: {Failure(err)}";
        return await AsHandedAsync(hand, ct).ConfigureAwait(false);
    }

    /// <summary>Why the tree is not as the hand-off takes and leaves it (clean, on its branch at its tip); null where it is.</summary>
    private static async Task<string?> AsHandedAsync(ResolveHand hand, CancellationToken ct)
    {
        var (named, branch, _) = await WorkingTree.GitAsync(hand.Tree, ["symbolic-ref", "--quiet", "HEAD"], ct).ConfigureAwait(false);
        if (named != 0 || branch.Trim() != $"refs/heads/{hand.Branch}") return $"the tree is not on `{hand.Branch}`";
        if (!SameCommit(await CommitOfAsync(hand.Tree, "HEAD", ct).ConfigureAwait(false), hand.Tip)) return $"the tree is not at `{TextBounds.ShortCommit(hand.Tip, 7)}`";
        var (read, status, _) = await WorkingTree.GitAsync(hand.Tree, ["--no-optional-locks", "status", "--porcelain", "--untracked-files=all"], ct).ConfigureAwait(false);
        return read != 0 ? "git could not read the tree" : status.Trim().Length > 0 ? "the tree holds work no commit carries" : null;
    }

    /// <summary>The replay gone on from git's <paramref name="first"/> (or from the stop it stands at), until its end or a stop no rule settles.</summary>
    private async Task<ResolveStep> GoneOnAsync(ResolveHand hand, IReadOnlyList<string>? first, CancellationToken ct)
    {
        var stops = await settles.StopsAsync(hand.Root, hand.Repository, hand.Onto, ct).ConfigureAwait(false);
        var (code, output, err, paths) = await ReplaySettles.SettleStopsAsync(hand.Tree, first, [], stops, partly: true, ct).ConfigureAwait(false);
        if (code == 0 && !await RebasingAsync(hand.Tree, ct).ConfigureAwait(false))
        {
            return new(ResolveSteps.Done, "") { Commit = await CommitOfAsync(hand.Tree, "HEAD", ct).ConfigureAwait(false) };
        }

        if (paths.Count == 0) return new(ResolveSteps.Failed, $"git could not go on with the replay: {Failure(err.Length > 0 ? err : output)}");
        return new(ResolveSteps.Stopped, "") { At = await StopAtAsync(hand.Tree, paths, ct).ConfigureAwait(false) };
    }

    /// <summary>The stop the tree stands at, with each file's hunks and the index's settled entries.</summary>
    private static async Task<ResolveStopAt> StopAtAsync(string tree, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var commit = await CommitOfAsync(tree, "REBASE_HEAD", ct).ConfigureAwait(false) ?? "";
        var (_, subject, _) = await WorkingTree.GitAsync(tree, ["log", "-1", "--format=%s", commit], ct).ConfigureAwait(false);
        var files = new List<ResolveFile>();
        foreach (var path in paths)
        {
            var full = Path.Combine(tree, path);
            files.Add(new ResolveFile(path, File.Exists(full) ? ResolveHunks.Of(await File.ReadAllTextAsync(full, ct).ConfigureAwait(false)) : []));
        }

        return new ResolveStopAt(commit, subject.Trim(), await RebaseReplay.PatchIdAsync(tree, commit, ct).ConfigureAwait(false),
            await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) ?? "", files)
        {
            Staged = await StagedAsync(tree, ct).ConfigureAwait(false),
        };
    }

    /// <summary>Why a named file is not settled yet: a conflict git still records, or markers left in it; null where every one is.</summary>
    private static async Task<string?> MarkedAsync(string tree, ResolveStopAt at, CancellationToken ct)
    {
        var (_, unmerged, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "--diff-filter=U"], ct)
            .ConfigureAwait(false);
        foreach (var path in at.Paths)
        {
            var full = Path.Combine(tree, path);
            if (File.Exists(full) && ResolveHunks.Of(await File.ReadAllTextAsync(full, ct).ConfigureAwait(false)).Count > 0) return $"`{path}` still holds conflict markers";
        }

        return unmerged.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(path => !at.Paths.Contains(path)) is { } other
            ? $"`{other}` is still conflicted"
            : null;
    }

    /// <summary>The paths beside the named ones the resolver changed in the tree and has not committed.</summary>
    private static async Task<IReadOnlyList<string>> OthersAsync(string tree, ResolveStopAt at, CancellationToken ct)
    {
        var (_, status, _) = await WorkingTree.GitAsync(
            tree, ["--no-optional-locks", "-c", "core.quotePath=false", "status", "--porcelain", "--untracked-files=all"], ct).ConfigureAwait(false);
        return [.. status.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line => line.Length > 3).Select(line => line[3..].Trim())
            .Where(path => !at.Paths.Contains(path, StringComparer.Ordinal))];
    }

    /// <summary>The paths beside the named ones whose content the stop's commit, as made, holds otherwise than the rebase settled them.</summary>
    private static async Task<IReadOnlyList<string>> ProvedAsync(string tree, ResolveStopAt at, CancellationToken ct)
    {
        var (_, listed, _) = await WorkingTree.GitAsync(tree, ["rev-list", "--reverse", "--ancestry-path", $"{at.Head}..HEAD"], ct).ConfigureAwait(false);
        if (listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() is not { } made) return [];
        var (_, entries, _) = await WorkingTree.GitAsync(tree, ["-c", "core.quotePath=false", "ls-tree", "-r", "--full-tree", made], ct).ConfigureAwait(false);
        var holds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in entries.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            var parts = line[..tab].Split(' ');
            holds[line[(tab + 1)..]] = $"{parts[0]} {parts[2]}";
        }

        return [.. holds.Keys.Concat(at.Staged.Keys).Distinct(StringComparer.Ordinal)
            .Where(path => !at.Paths.Contains(path, StringComparer.Ordinal) && holds.GetValueOrDefault(path) != at.Staged.GetValueOrDefault(path))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The index's settled entries, path to mode and blob.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> StagedAsync(string tree, CancellationToken ct)
    {
        var (_, listed, _) = await WorkingTree.GitAsync(tree, ["-c", "core.quotePath=false", "ls-files", "--stage"], ct).ConfigureAwait(false);
        var staged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            var parts = line[..tab].Split(' ');
            if (parts[2] == "0") staged[line[(tab + 1)..]] = $"{parts[0]} {parts[1]}";
        }

        return staged;
    }

    /// <summary>What the resolver made, on the edits ref: HEAD's commits since the stop, and the index and working files as a commit on top.</summary>
    private static async Task KeepEditsAsync(ResolveHand hand, CancellationToken ct)
    {
        await WorkingTree.GitAsync(hand.Tree, ["add", "--all"], ct).ConfigureAwait(false);
        var (wrote, tree, _) = await WorkingTree.GitAsync(hand.Tree, ["write-tree"], ct).ConfigureAwait(false);
        if (wrote != 0) return;
        var (made, commit, _) = await WorkingTree.GitAsync(hand.Tree,
            ["-c", "user.name=Daoris", "-c", "user.email=daoris@localhost", "commit-tree", tree.Trim(), "-p", "HEAD", "-m", "The resolver's edits"], ct)
            .ConfigureAwait(false);
        if (made == 0) await WorkingTree.GitAsync(hand.Tree, ["update-ref", hand.EditsRef, commit.Trim()], ct).ConfigureAwait(false);
    }

    private static async Task<bool> RebasingAsync(string tree, CancellationToken ct)
    {
        foreach (var state in new[] { "rebase-merge", "rebase-apply" })
        {
            var (code, path, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--path-format=absolute", "--git-path", state], ct).ConfigureAwait(false);
            if (code == 0 && Directory.Exists(path.Trim())) return true;
        }

        return false;
    }

    private static async Task<string?> CommitOfAsync(string tree, string rev, CancellationToken ct)
    {
        var (code, sha, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", rev], ct).ConfigureAwait(false);
        return code == 0 ? sha.Trim() : null;
    }

    private static async Task<bool> DescendsAsync(string tree, string ancestor, CancellationToken ct) =>
        (await WorkingTree.GitAsync(tree, ["merge-base", "--is-ancestor", ancestor, "HEAD"], ct).ConfigureAwait(false)).Code == 0;

    private static bool SameCommit(string? one, string? other) => one is not null && string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    private static string Unnamed(IReadOnlyList<string> paths) =>
        $"The resolution changed {string.Join(", ", paths.Take(3).Select(path => $"`{path}`"))}{(paths.Count > 3 ? $" and {paths.Count - 3} more" : "")}, which the problem did not name.";
}

/// <summary>A conflicted file's hunks, each from its <c>&lt;&lt;&lt;&lt;&lt;&lt;&lt;</c> line to its <c>&gt;&gt;&gt;&gt;&gt;&gt;&gt;</c> line.</summary>
internal static class ResolveHunks
{
    public static IReadOnlyList<string> Of(string text)
    {
        var hunks = new List<string>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var at = 0; at < lines.Length; at++)
        {
            if (!lines[at].StartsWith("<<<<<<< ", StringComparison.Ordinal) && lines[at] != "<<<<<<<") continue;
            var end = Array.FindIndex(lines, at, line => line.StartsWith(">>>>>>>", StringComparison.Ordinal));
            if (end < 0) break;
            hunks.Add(string.Join('\n', lines[at..(end + 1)]));
            at = end;
        }

        return hunks;
    }
}
