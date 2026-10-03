namespace Daoris.Driver;

/// <summary>
/// What a landing's tidy did with one more session branch its work holds (LAND3): removed, with its tree where it had one,
/// or kept and why.
/// </summary>
/// <param name="Tree">Whether it had a tree here, which went with it where it was removed.</param>
/// <param name="Why">Kept: why, in a clause the landing's sentence carries.</param>
public sealed record TidiedBranch(string Branch, bool Removed, bool Tree, string? Why = null);

public sealed partial class SessionTrees
{
    /// <summary>The prefix every session branch Daoris makes carries (<see cref="OpenAsync"/>), and the only kind this removes.</summary>
    private const string SessionPrefix = "daoris/";

    /// <summary>
    /// The rest of a landing's tidy (LAND3, D102's note): every session branch recorded for the repository whose tip the
    /// landed ref contains goes, with its tree, so a chain's earlier step whose commits rode into the accepted one does not
    /// stay behind. Then the record forgets every branch of the repository that is gone.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Containment is git's own answer</b> (<c>merge-base --is-ancestor</c>): every commit the branch holds is in
    /// what the person just accepted, so removing it loses nothing. A branch holding a commit the landing does not, a failed
    /// attempt's, is no part of it and is left for its own door (<see cref="RemoveBranchAsync"/>).</para>
    ///
    /// <para><b>A tree goes only where nothing is in flight</b>: not one a session still running or waiting holds, asked
    /// inside the repository's hold so a start cannot choose it meanwhile (<see cref="TreeLock"/>, LEFT2's window); not one
    /// with uncommitted work; not one a person checked the branch out in. Where nobody asked which sessions run, or a start
    /// holds the repository, a branch with a tree is kept and said. A branch with no tree holds no session.</para>
    /// </remarks>
    /// <param name="landed">The ref the work landed on: the branch the landing made, or the line a merge went into.</param>
    /// <param name="pressed">The pressed session's own branch, which the tidy before this one handled.</param>
    private async Task<IReadOnlyList<TidiedBranch>> TidyHeldAsync(
        string root, string repository, string workspace, string landed, string? pressed,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse, CancellationToken ct)
    {
        var recorded = Grown.All()
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Branch)
            .Where(branch => branch.StartsWith(SessionPrefix, StringComparison.Ordinal) && !string.Equals(branch, pressed, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Each the landed ref holds, at the tip it was judged at; one that is gone is the record's to forget below.
        var held = new List<(string Branch, string Tip)>();
        foreach (var branch in recorded)
        {
            var (code, tip, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
                .ConfigureAwait(false);
            if (code != 0) continue;
            var (ancestor, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", $"refs/heads/{branch}", landed], ct)
                .ConfigureAwait(false);
            if (ancestor == 0) held.Add((branch, tip.Trim()));
        }

        var results = new List<TidiedBranch>();
        if (held.Count > 0)
        {
            var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
            using var hold = TreeLock.TryReplaying(home, workspace, repository);
            var busy = hold is not null && inUse is not null
                ? new HashSet<string>((await inUse(ct).ConfigureAwait(false)).Select(Normal), StringComparer.OrdinalIgnoreCase)
                : null;
            var unasked = inUse is null
                ? "whether a session still holds its tree was not asked here"
                : "a session was starting in one of the repository's trees";
            foreach (var (branch, tip) in held)
            {
                results.Add(await TidyOneAsync(root, branch, tip, worktrees.GetValueOrDefault(branch), busy, unasked, ct).ConfigureAwait(false));
            }
        }

        await ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        return results;
    }

    private async Task<TidiedBranch> TidyOneAsync(
        string root, string branch, string tip, string? tree, HashSet<string>? busy, string unasked, CancellationToken ct)
    {
        TidiedBranch Kept(string why) => new(branch, false, tree is not null, why);

        if (tree is not null)
        {
            if (!Holds(tree)) return Kept($"it is checked out at {tree}, which is not a session tree");
            if (busy is null) return Kept(unasked);
            if (busy.Contains(Normal(tree))) return Kept("a session still running or waiting holds its tree");
            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dirty)) return Kept($"its tree has {dirty.Trim().Split('\n').Length} path(s) uncommitted");
        }

        // 🔴 Judged a moment ago; removed only while it is still the commit judged.
        var (code, now, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (code != 0 || now.Trim() != tip) return Kept("it moved since it was judged");

        if (tree is not null)
        {
            var (removeCode, _, removeErr) = await WorkingTree.GitAsync(root, ["worktree", "remove", tree], ct).ConfigureAwait(false);
            if (removeCode != 0 && !(await LetGoAsync(root, tree, ct).ConfigureAwait(false)).LetGo)
            {
                return Kept($"git would not remove its tree: {FirstLine(removeErr)}");
            }
        }

        // -D, because the proof was made in this call; git's own -d asks only about the checkout's HEAD.
        var (deleteCode, _, deleteErr) = await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
        return deleteCode == 0 ? new(branch, true, tree is not null) : Kept($"git would not delete it: {FirstLine(deleteErr)}");
    }

    /// <summary>What the rest of the tidy did, in the landing's words: what went, and what stayed and why.</summary>
    private static string TidiedSaid(IReadOnlyList<TidiedBranch> tidied, string landed)
    {
        var said = "";
        var removed = tidied.Where(each => each.Removed).Select(each => $"`{each.Branch}`" + (each.Tree ? " (with its tree)" : "")).ToArray();
        if (removed.Length > 0)
        {
            var list = removed.Length == 1 ? removed[0] : $"{string.Join(", ", removed[..^1])} and {removed[^1]}";
            said += $" Also removed {list}, whose commits `{landed}` holds.";
        }

        foreach (var kept in tidied.Where(each => !each.Removed))
        {
            said += $" Kept `{kept.Branch}`, whose commits `{landed}` holds: {kept.Why}.";
        }

        return said;
    }

    /// <summary>
    /// The door offered beside a kept branch whose commits no branch of the person's holds (LAND3): a failed or superseded
    /// attempt's, which no landing's tidy and no clean-up takes, is discarded only by the person saying so. Null for every
    /// other row, and for one git could not judge.
    /// </summary>
    public static string? RemovalOffered(SweepItem item) => item.Kind == SweepKind.Unlanded && item.Commits > 0
        ? $"if its work is not wanted (a failed or superseded attempt): `daoris-driver trees remove {item.Branch} --repository {item.Repository} --force`"
        : null;

    /// <summary>
    /// Whether a removal names a tree by its path, as `trees remove` always took one: rooted, or holding a separator. A
    /// session's branch (<c>daoris/…</c>), its tree's name (<c>s-…</c>) and a session's id are not paths (LAND3).
    /// </summary>
    public static bool NamesAPath(string named) =>
        !named.StartsWith(SessionPrefix, StringComparison.Ordinal)
        && (Path.IsPathRooted(named) || named.Contains('/') || named.Contains('\\'));

    /// <summary>The session branches the record holds by that name, its whole name (<c>daoris/s-…</c>) or its tree's (<c>s-…</c>).</summary>
    public IReadOnlyList<GrownBranch> FindBranches(string named, string? repository) =>
        [.. Grown.All()
            .Where(entry => string.Equals(entry.Branch, named, StringComparison.Ordinal)
                            || string.Equals(entry.Branch, SessionPrefix + named, StringComparison.Ordinal))
            .Where(entry => repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// The session branch a tree this home opened was on, from the layout Daoris chose: the tree's name under its repository
    /// is the branch's after <c>daoris/</c>. What finds a session's branch once its tree is gone; null for a path that is no
    /// tree of this home's.
    /// </summary>
    public (string Workspace, string Repository, string Branch)? BranchOfTree(string tree)
    {
        var full = Path.GetFullPath(tree);
        if (!Holds(full)) return null;
        var (workspace, repository) = OwnerOf(full);
        return repository.Length == 0 ? null : (workspace, repository, SessionPrefix + Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar)));
    }

    /// <summary>
    /// A session branch removed by the person's door (LAND3): a failed or superseded attempt's, whose commits no branch of
    /// theirs holds, so no landing's tidy or clean-up takes it. With its tree where it still has one, through
    /// <see cref="RemoveAsync"/>; without, the branch alone. <b>Only a session branch</b> (<c>daoris/…</c>): a branch of the
    /// person's is theirs to delete.
    /// </summary>
    /// <remarks>
    /// Unforced it is the clean-up's proof (D88): it goes only where a branch of the person's holds every commit, and the
    /// refusal names the commits it would lose. <paramref name="force"/> is the person saying it again, meaning it. The record
    /// forgets it once it is gone.
    /// </remarks>
    public async Task<TreeRemoval> RemoveBranchAsync(string root, string repository, string branch, bool force = false, CancellationToken ct = default)
    {
        if (!branch.StartsWith(SessionPrefix, StringComparison.Ordinal))
        {
            return new(false, $"`{branch}` is not a session branch — only Daoris's own (`{SessionPrefix}…`) are removed here, and a "
                + "branch of yours is yours to delete.");
        }

        // The root must BE the top of its own repository: git walks UP, and would answer for the one above it (FIX-LOG).
        var (topCode, toplevel, topErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0 || !SamePath(toplevel.Trim(), root))
        {
            return new(false, $"`{repository}`'s checkout at {root} is not the top of a repository of its own"
                + (topCode != 0 ? $" ({FirstLine(topErr)})" : "") + ", so nothing there is removed.");
        }

        var (exists, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (exists != 0)
        {
            Forget(repository, branch);
            return new(false, $"there is no branch `{branch}` in `{repository}` — it is gone already.");
        }

        var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
        if (worktrees.GetValueOrDefault(branch) is { } tree)
        {
            return Holds(tree)
                ? await RemoveAsync(tree, force, ct).ConfigureAwait(false)
                : new(false, $"`{branch}` is checked out at {tree}, which is not a session tree — Daoris removes nothing there.");
        }

        if (!force)
        {
            var (logCode, unlanded, logErr) = await UnlandedLogAsync(root, branch, ct).ConfigureAwait(false);
            if (logCode != 0)
            {
                return new(false, $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(logErr)} "
                    + "The branch stays; say it again with --force to discard it.");
            }

            if (!string.IsNullOrWhiteSpace(unlanded))
            {
                return new(false, $"`{branch}` holds commits no branch of yours holds:\n{unlanded.Trim()}\n"
                    + "If its work is not wanted — a failed or superseded attempt — say it again with --force to discard them.");
            }
        }

        var (deleteCode, _, deleteErr) = await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
        if (deleteCode != 0) return new(false, $"git would not delete `{branch}`: {FirstLine(deleteErr)}");
        Forget(repository, branch);
        return new(true, $"removed the session branch `{branch}` from `{repository}`.");
    }

    /// <summary>The record forgets one branch; a record that could not be written only keeps a name no branch has.</summary>
    private void Forget(string repository, string branch)
    {
        try
        {
            Grown.Forget(repository, [branch]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The next landing's tidy, or the clean-up, forgets it again.
        }
    }
}
