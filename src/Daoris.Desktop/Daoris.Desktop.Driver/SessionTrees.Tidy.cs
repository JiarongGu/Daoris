namespace Daoris.Driver;

/// <summary>
/// What a landing's tidy did with one more session branch its work holds (LAND3): removed, with its tree where it had one,
/// or kept and why.
/// </summary>
/// <param name="Tree">Whether it had a tree here, which went with it where it was removed.</param>
/// <param name="Why">Kept: why, in a clause the landing's sentence carries.</param>
public sealed record TidiedBranch(string Branch, bool Removed, bool Tree, string? Why = null)
{
    /// <summary>
    /// The landed branch whose completed pull request carried it (PLUGHOOK1a, D148 point 4), where the platform's word took it
    /// rather than the landed ref's containment; null for the latter.
    /// </summary>
    public string? CarriedBy { get; init; }
}

public sealed partial class SessionTrees
{
    /// <summary>The prefix every session branch Daoris makes carries (<see cref="OpenAsync"/>), and the only kind this removes.</summary>
    internal const string SessionPrefix = "daoris/";

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

        // PLUGHOOK1a: a squash leaves no ancestor, so the branches a completed pull request carried go on its plugin's word,
        // where git confirms its merge commit on the line.
        results.AddRange(await TidyCarriedAsync(root, repository, workspace, held.Select(each => each.Branch).ToList(), pressed, inUse, ct)
            .ConfigureAwait(false));
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
            // Everything no commit holds, whatever git's settings hide (SQUASHTIDY1c): what removing the tree would destroy.
            if (Directory.Exists(tree))
            {
                var holds = await HoldsAsync(tree, root, ct).ConfigureAwait(false);
                if (holds is null) return Kept("git could not say what its tree holds");
                if (holds.Uncommitted.Count > 0) return Kept($"its tree has {holds.Uncommitted.Count} path(s) uncommitted");
                if (holds.Ignored.Count > 0) return Kept($"its tree holds {holds.IgnoredSaid}");
            }
        }

        // 🔴 Judged a moment ago; removed only while it is still the commit judged.
        var (code, now, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (code != 0 || now.Trim() != tip) return Kept(MovedSince);

        if (tree is not null)
        {
            var (removeCode, _, removeErr) = await WorkingTree.GitAsync(root, ["worktree", "remove", tree], ct).ConfigureAwait(false);
            if (removeCode != 0 && !(await LetGoAsync(root, tree, ct).ConfigureAwait(false)).LetGo)
            {
                return Kept($"git would not remove its tree: {FirstLine(removeErr)}");
            }
        }

        // The look above leaves a moment before the delete, so the delete itself compares (SQUASHTIDY1c); git's own -d asks
        // only about the checkout's HEAD.
        var stays = await DeleteJudgedAsync(root, branch, tip, ct).ConfigureAwait(false);
        return stays is null ? new(branch, true, tree is not null) : Kept(stays);
    }

    /// <summary>What the rest of the tidy did, in the landing's words: what went, and what stayed and why.</summary>
    private static string TidiedSaid(IReadOnlyList<TidiedBranch> tidied, string landed)
    {
        var said = "";
        var removed = tidied.Where(each => each.Removed && each.CarriedBy is null).Select(Named).ToArray();
        if (removed.Length > 0) said += $" Also removed {Listed(removed)}, whose commits `{landed}` holds.";

        foreach (var kept in tidied.Where(each => !each.Removed && each.CarriedBy is null))
        {
            said += $" Kept `{kept.Branch}`, whose commits `{landed}` holds: {kept.Why}.";
        }

        // PLUGHOOK1a: each landed branch's completed pull request, and what it carried.
        foreach (var by in tidied.Where(each => each.CarriedBy is not null).GroupBy(each => each.CarriedBy!, StringComparer.Ordinal))
        {
            var went = by.Where(each => each.Removed).Select(Named).ToArray();
            if (went.Length > 0) said += $" Also removed {Listed(went)}, whose work `{by.Key}`'s completed pull request carried.";
            foreach (var kept in by.Where(each => !each.Removed))
            {
                said += $" Kept `{kept.Branch}`, whose work `{by.Key}`'s completed pull request carried: {kept.Why}.";
            }
        }

        return said;

        static string Named(TidiedBranch each) => $"`{each.Branch}`" + (each.Tree ? " (with its tree)" : "");
        static string Listed(string[] names) => names.Length == 1 ? names[0] : $"{string.Join(", ", names[..^1])} and {names[^1]}";
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
    /// The door offered beside a kept branch whose tree is still here and holds commits no branch of the person's holds (LAND4):
    /// its session's landing, the review's Accept from a terminal, whatever that session's ending. The landing checks
    /// everything at its press. Null for a branch whose tree is gone (its discard is the door), with no commits, that no
    /// record of this machine's names, or that a session still holds.
    /// </summary>
    /// <param name="session">The session the branch's tree is (<see cref="SessionOfTree"/>), or null.</param>
    public static string? LandingOffered(SweepItem item, SessionRecord? session) =>
        item is { Kind: SweepKind.Unlanded, Commits: > 0, Tree: not null } && session is { Live: false, Teammate: false }
            ? $"if its work is wanted: `daoris-driver trees land {session.Id}` accepts it (session {session.Id}, {session.State}, its tree here)"
            : null;

    /// <summary>
    /// The records a clean-up's list names each kept branch's session from (LAND4), or none where the service does not answer
    /// them: the landing line beside a row is the list's convenience, and the list stands without it.
    /// </summary>
    public static async Task<IReadOnlyList<SessionRecord>> RecordsOrNoneAsync(ServiceClient service, CancellationToken ct = default)
    {
        try
        {
            return await service.SessionRecordsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The session a tree is (LAND4): the newest record of this machine's naming it, separators and case aside, as the reader's
    /// <c>ReviewableTree</c> lets the newest stand for a tree. Null for no tree, or one no record of this machine's names.
    /// </summary>
    public static SessionRecord? SessionOfTree(IEnumerable<SessionRecord> records, string? tree) =>
        tree is null
            ? null
            : records
                .Where(record => !record.Teammate && record.Tree is { } named
                                 && string.Equals(SessionGroups.Normal(named), SessionGroups.Normal(tree), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.Created).ThenByDescending(record => record.Id, StringComparer.Ordinal)
                .FirstOrDefault();

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
    /// Unforced it is the clean-up's proof (D88): it goes only where a branch of the person's holds every commit, or where its
    /// work is held by content (SQUASHTIDY1, <see cref="HeldByContentAsync"/>), and the refusal names the commits it would lose.
    /// It deletes the branch only while it is the commit judged, and a content-held one's commits stay on a recovery ref the
    /// sentence names (SQUASHTIDY1c).
    /// <paramref name="force"/> is the person saying it again, meaning it. The record
    /// forgets it once it is gone. <b>It asks no sessions</b>, so forced it would take a tree a live session holds: the
    /// person's doors reach it through <see cref="SessionBranchDiscard"/>, which keeps such a branch (LAND3c).
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

        ContentHold? held = null;
        string? kept = null;
        if (!force)
        {
            // The commit judged, read once (SQUASHTIDY1c): every question below is asked of it, and the branch goes only while it
            // is still that commit.
            var (tipCode, tipOut, tipErr) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct).ConfigureAwait(false);
            var judged = tipOut.Trim();
            var (logCode, unlanded, logErr) = tipCode != 0 ? (tipCode, "", tipErr) : await UnlandedLogAsync(root, judged, ct).ConfigureAwait(false);
            if (logCode != 0)
            {
                return new(false, $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(logErr)} "
                    + "The branch stays; say it again with --force to discard it.");
            }

            // SQUASHTIDY1: the same proof by content as a tree's removal, against the line of the workspace it grew in.
            if (!string.IsNullOrWhiteSpace(unlanded))
            {
                var grewIn = Grown.All().LastOrDefault(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                                                && string.Equals(entry.Branch, branch, StringComparison.Ordinal))?.Workspace;
                var line = (await LineAsync(root, repository, RemoteTarget.Workspace(grewIn), ct).ConfigureAwait(false)).Branch;
                held = await HeldByContentAsync(root, judged, line, LandedHere(repository), ct).ConfigureAwait(false);
                if (held is null)
                {
                    return new(false, $"`{branch}` holds commits no branch of yours holds:\n{unlanded.Trim()}\n"
                        + "If its work is not wanted — a failed or superseded attempt — say it again with --force to discard them.");
                }

                // SQUASHTIDY1c: the proof compared files, so the commits themselves are kept on a ref of their own first.
                var (reference, why) = await KeepDiscardedAsync(root, branch, judged, ct).ConfigureAwait(false);
                if (reference is null)
                {
                    return new(false, $"Daoris could not keep the commits on `{branch}` on a ref of their own ({why}), so it stays. "
                        + "Say it again with --force to discard them.");
                }

                kept = reference;
            }

            var stays = await DeleteJudgedAsync(root, branch, judged, ct).ConfigureAwait(false);
            if (stays is not null)
            {
                await DropKeptAsync(root, branch, kept, judged, ct).ConfigureAwait(false);
                return new(false, $"`{branch}` stays: {stays}"
                    + (stays == MovedSince ? ", so it holds work this removal did not judge." : "."));
            }
        }
        else
        {
            var (deleteCode, _, deleteErr) = await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
            if (deleteCode != 0) return new(false, $"git would not delete `{branch}`: {FirstLine(deleteErr)}");
        }

        Forget(repository, branch);
        return new(true, $"removed the session branch `{branch}` from `{repository}`" + (held is null ? "." : $": {held.Said}.")
            + (kept is null ? "" : $" {ContentHold.KeptAt(branch, kept)}"));
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
