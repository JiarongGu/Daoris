namespace Daoris.Driver;

/// <summary>
/// What abandon's proof found of one session tree and its branch (PAUSE1d, D132 point 7, design §3.3): a word from a fixed
/// list, in the public spelling the page and the terminal share.
/// </summary>
public static class OnlyHereKind
{
    /// <summary>No commit after its base is on any ref but this machine's local <c>daoris/*</c> branches: discarded.</summary>
    public const string OnlyHere = "only-here";

    /// <summary>A commit after its base is on another ref (the line, a branch of the person's, a remote, a tag): kept whole.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>Its branch is checked out somewhere other than its own tree: somebody works in it, so it is kept.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>git could not say: kept, as the clean-up keeps what it cannot judge.</summary>
    public const string Unknown = "unknown";

    /// <summary>Neither the tree's folder nor its branch is on this machine any more: nothing to discard.</summary>
    public const string Gone = "gone";
}

/// <summary>Abandon's proof of one tree and its branch, and what the first press names of them (design §3.2, §3.3).</summary>
/// <param name="Kind">One of <see cref="OnlyHereKind"/>.</param>
public sealed record TreeOnlyHere(string Kind)
{
    /// <summary>The repository's checkout git answered for: this machine's path, never handed to a page or a record.</summary>
    public string? Root { get; init; }

    /// <summary>Whether the tree's folder is here, a repository of its own; false is its branch alone.</summary>
    public bool TreeHere { get; init; }

    /// <summary>The branch's tip, which brings a deleted branch back while git keeps its commits.</summary>
    public string? Tip { get; init; }

    /// <summary>How many commits after the base it holds, up to the tip; null where git could not count.</summary>
    public int? Commits { get; init; }

    /// <summary>How many paths in the tree carry uncommitted changes; null where there is no tree to ask.</summary>
    public int? Uncommitted { get; init; }

    /// <summary>Up to <see cref="SessionTrees.FilesNamed"/> of those paths, relative to the repository.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>For <see cref="OnlyHereKind.Elsewhere"/>: a ref that holds one of its commits, as git names it short.</summary>
    public string? Where { get; init; }

    /// <summary>Whether the abandon may discard it: nothing it holds is anywhere else.</summary>
    public bool Discards => Kind == OnlyHereKind.OnlyHere;
}

/// <summary>
/// What an abandon asks of the trees (PAUSE1d, D132 §3.3): the proof, the discard behind it, and what a kept tree holds for
/// its review. <see cref="SessionTrees"/> answers by git; a test hands in its own.
/// </summary>
public interface IWorkTrees
{
    /// <summary>Whether nothing this tree and its branch hold is anywhere else (<see cref="SessionTrees.OnlyHereAsync"/>).</summary>
    Task<TreeOnlyHere> OnlyHereAsync(string tree, string branch, string? baseCommit, string? root, CancellationToken ct = default);

    /// <summary>Discard a tree and its branch the proof cleared, or its branch alone where the tree is gone.</summary>
    Task<TreeRemoval> DiscardAsync(string tree, string branch, TreeOnlyHere judged, CancellationToken ct = default);

    /// <summary>What a kept tree holds for its review (<see cref="SessionTrees.WorkAsync"/>): D88's proof, the reader's.</summary>
    Task<TreeWork?> WorkAsync(string tree, CancellationToken ct = default);
}

/// <summary>
/// Abandon's proof (PAUSE1d, D132 point 7, design §3.3): a tree and its branch are discarded only when none of their work
/// has left Daoris's own branches on this machine, so what goes is exactly the abandoned work and nothing anyone else may
/// have. D88's proof turned around: the clean-up removes what is proven elsewhere, abandon what is proven nowhere else.
/// </summary>
/// <remarks>
/// <para><b>The commits judged</b> are those after the session's base up to the branch's tip; with no base, the merge-base
/// with the repository's line (D86) stands in. <b>Anywhere else</b> is any branch but this machine's local
/// <c>refs/heads/daoris/*</c>, any remote-tracking branch (a pushed <c>daoris/*</c> included: pushed means somebody may have
/// it), and any tag. D88's <see cref="UnlandedAsync"/> leaves <c>*/daoris/*</c> out, rightly for a clean-up, since a pushed
/// session branch has not landed.</para>
///
/// <para><b>Partly elsewhere is kept whole</b>, as the clean-up keeps it: which half the person meant is its review's to
/// decide. A count git cannot give keeps it. 🔴 git walks UP: a tree is asked only when it is a repository of its own, and a
/// checkout only when it is the top of one (FIX-LOG).</para>
/// </remarks>
public sealed partial class SessionTrees : IWorkTrees
{
    /// <summary>How many uncommitted paths the first press names (design §3.2).</summary>
    public const int FilesNamed = 5;

    /// <summary>
    /// Whether nothing <paramref name="tree"/> and <paramref name="branch"/> hold is anywhere else. Reads, and never writes.
    /// </summary>
    /// <param name="tree">The session's tree, under this home's trees; its folder may be gone.</param>
    /// <param name="branch">Its branch, <c>daoris/</c> and the tree's folder name.</param>
    /// <param name="baseCommit">The base of the tree's first session (SURF6); null takes the merge-base with the line.</param>
    /// <param name="root">The repository's registered checkout, which answers for a branch whose tree is gone.</param>
    public async Task<TreeOnlyHere> OnlyHereAsync(
        string tree, string branch, string? baseCommit, string? root, CancellationToken ct = default)
    {
        var unknown = new TreeOnlyHere(OnlyHereKind.Unknown);
        // Only a tree this home opened and a session branch of Daoris's are ever judged for a discard.
        if (!IsHeld(tree) || !branch.StartsWith("daoris/", StringComparison.Ordinal)) return unknown;

        var full = Path.GetFullPath(tree);
        var treeHere = !TreeGone(full) && await WorkingTree.IsTopLevelAsync(full, ct).ConfigureAwait(false);
        string checkout;
        if (treeHere)
        {
            var (code, commonDir, _) = await WorkingTree.GitAsync(
                full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
            if (code != 0) return unknown;
            checkout = Path.GetDirectoryName(commonDir.Trim())!;
        }
        else if (root is not null && await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            checkout = root;
        }
        else
        {
            return unknown;
        }

        // The tip: the tree's HEAD, or the branch where the tree is gone, which is gone too when git has no such branch.
        var (tipCode, tipOut, _) = treeHere
            ? await WorkingTree.GitAsync(full, ["rev-parse", "HEAD"], ct).ConfigureAwait(false)
            : await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false);
        if (tipCode != 0) return treeHere ? unknown with { Root = checkout, TreeHere = true } : new TreeOnlyHere(OnlyHereKind.Gone) { Root = checkout };
        var tip = tipOut.Trim();
        var judged = new TreeOnlyHere(OnlyHereKind.Unknown) { Root = checkout, TreeHere = treeHere, Tip = tip };

        if (treeHere)
        {
            var (statusCode, porcelain, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain", "-z"], ct).ConfigureAwait(false);
            if (statusCode == 0)
            {
                var paths = Changed(porcelain);
                judged = judged with { Uncommitted = paths.Count, Files = [.. paths.Take(FilesNamed)] };
            }
        }

        // Checked out anywhere but its own tree (the person's checkout, another tree): somebody works in it.
        var (listCode, worktrees, _) = await WorkingTree.GitAsync(checkout, ["worktree", "list", "--porcelain"], ct).ConfigureAwait(false);
        if (listCode != 0) return judged;
        if (CheckedOutAt(worktrees, branch).Any(path => !SamePath(path, full))) return judged with { Kind = OnlyHereKind.CheckedOut };

        var from = await BaseOfAsync(checkout, full, tip, baseCommit, ct).ConfigureAwait(false);
        if (from is null) return judged;

        var (countCode, count, _) = await WorkingTree.GitAsync(checkout, ["rev-list", "--count", $"{from}..{tip}"], ct).ConfigureAwait(false);
        if (countCode != 0 || !int.TryParse(count.Trim(), out var commits)) return judged;
        judged = judged with { Commits = commits };

        var (hereCode, hereOut, _) = await WorkingTree.GitAsync(checkout, [.. OnlyHereRange(tip, from)], ct).ConfigureAwait(false);
        if (hereCode != 0) return judged;
        var here = Lines(hereOut);
        if (here.Count == commits) return judged with { Kind = OnlyHereKind.OnlyHere };

        // Some of its work left: name a ref that holds a commit of it, the oldest such commit's first holder.
        var (allCode, allOut, _) = await WorkingTree.GitAsync(checkout, ["rev-list", $"{from}..{tip}"], ct).ConfigureAwait(false);
        var elsewhere = allCode == 0 ? Lines(allOut).Where(commit => !here.Contains(commit, StringComparer.OrdinalIgnoreCase)).LastOrDefault() : null;
        var where = elsewhere is null ? null : await HolderAsync(checkout, elsewhere, ct).ConfigureAwait(false);
        return judged with { Kind = OnlyHereKind.Elsewhere, Where = where };
    }

    /// <summary>
    /// Discard what <see cref="OnlyHereAsync"/> cleared (design §3.3): the tree and its branch, the person saying it again and
    /// meaning it (<see cref="RemoveAsync"/> with force), or the branch alone where the tree is gone. Reached only behind the
    /// proof, which the caller made a moment ago; anything it did not clear is refused here.
    /// </summary>
    public async Task<TreeRemoval> DiscardAsync(string tree, string branch, TreeOnlyHere judged, CancellationToken ct = default)
    {
        if (!judged.Discards) return new(false, $"`{branch}` is not proven to be held nowhere else, so it stays.");
        if (judged.TreeHere) return await RemoveAsync(tree, force: true, ct).ConfigureAwait(false);
        if (!branch.StartsWith("daoris/", StringComparison.Ordinal) || judged.Root is not { } root)
        {
            return new(false, $"`{branch}` is not a session branch of Daoris's, so it stays.");
        }

        // A tree whose folder went without git being told is still registered, and git will not delete a branch a
        // registered tree has checked out: its stale registration goes first, as `git worktree prune` takes only such.
        if (IsHeld(tree) && await RegisteredAsync(root, tree, ct).ConfigureAwait(false) is true)
        {
            await WorkingTree.GitAsync(root, ["worktree", "prune"], ct).ConfigureAwait(false);
        }

        var (code, _, err) = await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
        return code == 0
            ? new(true, $"deleted the branch `{branch}`, whose tree was already gone.")
            : new(false, $"git would not delete the branch `{branch}`: {FirstLine(err)}");
    }

    /// <summary>
    /// The arguments that list the commits after <paramref name="from"/> up to <paramref name="tip"/> that no branch but this
    /// machine's local <c>daoris/*</c>, no remote-tracking branch and no tag holds (design §3.3). <c>--exclude</c> applies to
    /// the <c>--branches</c> after it only, so a pushed <c>daoris/*</c> counts as elsewhere.
    /// </summary>
    internal static IReadOnlyList<string> OnlyHereRange(string tip, string from) =>
        ["rev-list", tip, $"^{from}", "--not", "--exclude=daoris/*", "--branches", "--remotes", "--tags"];

    /// <summary>The paths <c>git status --porcelain -z</c> names, in its order: a rename's or a copy's source is not one of them.</summary>
    internal static IReadOnlyList<string> Changed(string porcelain)
    {
        var entries = porcelain.Split('\0');
        var paths = new List<string>();
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            paths.Add(entry[3..]);
            // A rename or a copy is followed by its source, which is not a path of its own (git-status, -z).
            if (entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') at++;
        }

        return paths;
    }

    /// <summary>Where git has <paramref name="branch"/> checked out, from its own list.</summary>
    private static IEnumerable<string> CheckedOutAt(string porcelain, string branch)
    {
        string? path = null;
        foreach (var raw in porcelain.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("worktree ", StringComparison.Ordinal)) path = line["worktree ".Length..];
            else if (path is not null && line == $"branch refs/heads/{branch}") yield return path;
        }
    }

    /// <summary>The commit the tree's work is judged from: its base where git knows it, else the merge-base with the line.</summary>
    private async Task<string?> BaseOfAsync(string checkout, string tree, string tip, string? baseCommit, CancellationToken ct)
    {
        if (baseCommit is { Length: > 0 })
        {
            var (code, resolved, _) = await WorkingTree.GitAsync(
                checkout, ["rev-parse", "--verify", "--quiet", $"{baseCommit}^{{commit}}"], ct).ConfigureAwait(false);
            return code == 0 ? resolved.Trim() : null;
        }

        var (workspace, repository) = OwnerOf(tree);
        var line = (await LineAsync(checkout, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(checkout, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (mergeCode, mergeBase, _) = await WorkingTree.GitAsync(checkout, ["merge-base", against, tip], ct).ConfigureAwait(false);
        return mergeCode == 0 ? mergeBase.Trim() : null;
    }

    /// <summary>The first ref but this machine's local <c>daoris/*</c> that holds <paramref name="commit"/>, named short.</summary>
    private static async Task<string?> HolderAsync(string checkout, string commit, CancellationToken ct)
    {
        var (code, refs, _) = await WorkingTree.GitAsync(
            checkout, ["for-each-ref", "--contains", commit, "--format=%(refname)", "refs/heads", "refs/remotes", "refs/tags"], ct)
            .ConfigureAwait(false);
        if (code != 0) return null;
        return Lines(refs)
            .Where(name => !name.StartsWith("refs/heads/daoris/", StringComparison.Ordinal) && !name.EndsWith("/HEAD", StringComparison.Ordinal))
            .Select(name => name.StartsWith("refs/heads/", StringComparison.Ordinal) ? name["refs/heads/".Length..]
                : name.StartsWith("refs/remotes/", StringComparison.Ordinal) ? name["refs/remotes/".Length..]
                : name.StartsWith("refs/tags/", StringComparison.Ordinal) ? name["refs/tags/".Length..]
                : name)
            .FirstOrDefault();
    }

    private bool IsHeld(string tree)
    {
        try
        {
            return Holds(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static List<string> Lines(string text) =>
        [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
