using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeLeftovers;
using static Daoris.Driver.TreeWorktrees;

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

/// <summary>
/// A landing's tidy of the other session branches its work holds (LAND3), and the person's door for one session branch
/// (PART10l-ii, out of <c>SessionTrees.Tidy.cs</c>). Over the trees' context, the pull request's asks (the tidy's own occasion),
/// the content proof, the replay's record tidying, the squash's record and the removal. The words beside a kept branch's row,
/// which the terminal and the bridge say, are <see cref="SessionBranchDoors"/>.
/// </summary>
internal sealed class TreeTidies(
    TreeContext context, PullRequestAsks asks, ContentHolds content, BranchReplays replays, TreeSquashes squashes, TreeRemovals removals)
{
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
    internal async Task<IReadOnlyList<TidiedBranch>> TidyHeldAsync(
        string root, string repository, string workspace, string landed, string? pressed,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse, CancellationToken ct)
    {
        // One look (UIFREEZE1, PROC1d-i): the held tidy and the carried one ask git's guess at the line once.
        using var look = context.OneLook();
        var recorded = context.Grown.All()
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Branch)
            .Where(branch => branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal) && !string.Equals(branch, pressed, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The line's forms at their commits, read before any proof (FABRIC-END4a, second review): each delete verifies them, and the ref
        // that holds its commits, in its own transaction.
        var forms = recorded.Count == 0 ? []
            : await ContentHolds.LineRefsAsync(root, (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch, ct)
                .ConfigureAwait(false);
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
            // A list git could not give is never "no tree here" (AUTOTIDY1): each branch is kept, saying why.
            var worktrees = await context.Worktrees.ReadAsync(root, ct).ConfigureAwait(false);
            using var hold = TreeLock.TryReplaying(context.Home, workspace, repository);
            var busy = hold is not null && inUse is not null
                ? new HashSet<string>((await inUse(ct).ConfigureAwait(false)).Select(Normal), StringComparer.OrdinalIgnoreCase)
                : null;
            var unasked = inUse is null
                ? "whether a session still holds its tree was not asked here"
                : "a session was starting in one of the repository's trees";
            foreach (var (branch, tip) in held)
            {
                results.Add(worktrees is null
                    ? new TidiedBranch(branch, false, false, ListUnread)
                    : await TidyOneAsync(root, branch, tip, worktrees.GetValueOrDefault(branch), busy, unasked, forms, ct).ConfigureAwait(false));
            }
        }

        // PLUGHOOK1a: a squash leaves no ancestor, so the branches a completed pull request carried go on its plugin's word,
        // where git confirms its merge commit on the line.
        results.AddRange(await TidyCarriedAsync(root, repository, workspace, held.Select(each => each.Branch).ToList(), pressed, inUse, ct)
            .ConfigureAwait(false));
        await replays.ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        return results;
    }

    /// <summary>
    /// LAND3's tidy on a pull request's word (PLUGHOOK1a, design §2.1 occasion 1): after the containment pass, the repository's
    /// entries are asked about, and each recorded session branch a completed pull request carried goes too, with the tidy's
    /// keeps. Each removal is kept on the entry whose answer allowed it.
    /// </summary>
    private async Task<IReadOnlyList<TidiedBranch>> TidyCarriedAsync(
        string root, string repository, string workspace, IReadOnlyCollection<string> handled, string? pressed,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse, CancellationToken ct)
    {
        await asks.AskStatesAsync([(root, repository, workspace)], landedMayGo: false, ct).ConfigureAwait(false);
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        // Before the carriers are proved on the line (FABRIC-END4a, second review): each delete verifies the commits that proof saw.
        var forms = await ContentHolds.LineRefsAsync(root, line, ct).ConfigureAwait(false);
        var carriers = await asks.CarriersAsync(root, repository, line, ct).ConfigureAwait(false);
        if (carriers.Count == 0) return [];

        var carried = new List<(string Branch, string Tip, PullRequestCarrier Carrier)>();
        foreach (var branch in context.Grown.All()
                     .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
                     .Select(entry => entry.Branch)
                     .Where(branch => branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal)
                                      && !string.Equals(branch, pressed, StringComparison.Ordinal) && !handled.Contains(branch))
                     .Distinct(StringComparer.Ordinal))
        {
            var (code, tip, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
                .ConfigureAwait(false);
            if (code != 0) continue;
            if (await PullRequestAsks.CarrierOfAsync(root, $"refs/heads/{branch}", carriers, ct).ConfigureAwait(false) is { } carrier)
            {
                carried.Add((branch, tip.Trim(), carrier));
            }
        }

        if (carried.Count == 0) return [];
        var results = new List<TidiedBranch>();
        // A list git could not give is never "no tree here" (AUTOTIDY1): each branch is kept, saying why.
        var worktrees = await context.Worktrees.ReadAsync(root, ct).ConfigureAwait(false);
        using var hold = TreeLock.TryReplaying(context.Home, workspace, repository);
        var busy = hold is not null && inUse is not null
            ? new HashSet<string>((await inUse(ct).ConfigureAwait(false)).Select(Normal), StringComparer.OrdinalIgnoreCase)
            : null;
        var unasked = inUse is null
            ? "whether a session still holds its tree was not asked here"
            : "a session was starting in one of the repository's trees";
        foreach (var (branch, tip, carrier) in carried)
        {
            var tidied = (worktrees is null
                    ? new TidiedBranch(branch, false, false, ListUnread)
                    : await TidyOneAsync(root, branch, tip, worktrees.GetValueOrDefault(branch), busy, unasked, forms, ct).ConfigureAwait(false))
                with { CarriedBy = carrier.Entry.Branch };
            results.Add(tidied);
            if (!tidied.Removed) continue;
            try
            {
                context.Recorded.Carried(carrier.Entry, new CarriedBranch(branch, tip, context.Plugins.Now, CarriedBy.Tidy));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The branch went on a confirmed answer; only the note of it on the record is lost.
            }
        }

        return results;
    }

    /// <param name="forms">The line's forms at the commits read before the proof; null where git could not read them, which keeps it.</param>
    private async Task<TidiedBranch> TidyOneAsync(
        string root, string branch, string tip, string? tree, HashSet<string>? busy, string unasked,
        IReadOnlyList<(string Ref, string Commit)>? forms, CancellationToken ct)
    {
        TidiedBranch Kept(string why) => new(branch, false, tree is not null, why);
        if (forms is null) return Kept("Daoris could not read the line its proof rests on");

        if (tree is not null)
        {
            if (!context.Holds(tree)) return Kept($"it is checked out at {tree}, which is not a session tree");
            if (busy is null) return Kept(unasked);
            if (busy.Contains(Normal(tree))) return Kept("a session still running or waiting holds its tree");
            // Everything no commit holds, whatever git's settings hide (SQUASHTIDY1c): what removing the tree would destroy.
            if (Directory.Exists(tree))
            {
                var holds = await ContentHolds.HoldsAsync(tree, root, ct).ConfigureAwait(false);
                if (holds is null) return Kept("git could not say what its tree holds");
                if (holds.Uncommitted.Count > 0) return Kept($"its tree has {holds.Uncommitted.Count} path(s) uncommitted");
                if (holds.OwnSaid is { } own) return Kept($"its tree holds {own}");
            }
        }

        // 🔴 Judged a moment ago; removed only while it is still the commit judged.
        var (code, now, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (code != 0 || now.Trim() != tip) return Kept(ContentHolds.MovedSince);

        if (tree is not null)
        {
            var (removeCode, _, removeErr) = await WorkingTree.GitAsync(root, ["worktree", "remove", tree], ct).ConfigureAwait(false);
            if (removeCode != 0 && !(await LetGoAsync(root, tree, ct).ConfigureAwait(false)).LetGo)
            {
                return Kept($"git would not remove its tree: {FirstLine(removeErr)}");
            }
        }

        // The look above leaves a moment before the delete, so the delete itself compares (SQUASHTIDY1c), in one transaction with what
        // its proof rested on (FABRIC-END4a, second review); git's own -d asks only about the checkout's HEAD.
        var held = await ContentHolds.ProofRefsAsync(root, branch, tip, forms, ContentHolds.AnyRef, ct).ConfigureAwait(false);
        if (held is null) return Kept("Daoris could not read what its proof rests on");
        var stays = await content.DeleteJudgedAsync(root, branch, tip, held, ct).ConfigureAwait(false);
        return stays is null ? new(branch, true, tree is not null) : Kept(stays);
    }

    /// <summary>What the rest of the tidy did, in the landing's words: what went, and what stayed and why.</summary>
    internal static string TidiedSaid(IReadOnlyList<TidiedBranch> tidied, string landed)
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
    /// A session branch removed by the person's door (LAND3): a failed or superseded attempt's, whose commits no branch of
    /// theirs holds, so no landing's tidy or clean-up takes it. With its tree where it still has one, through
    /// <see cref="TreeRemovals.RemoveAsync"/>; without, the branch alone. <b>Only a session branch</b> (<c>daoris/…</c>): a branch of the
    /// person's is theirs to delete.
    /// </summary>
    /// <remarks>
    /// Unforced it is the clean-up's proof (D88): it goes only where a branch of the person's holds every commit, or where its
    /// work is held by content (SQUASHTIDY1, <see cref="ContentHolds.HeldByContentAsync"/>), and the refusal names the commits it would lose.
    /// It deletes the branch only while it is the commit judged, and a content-held one's commits stay on a recovery ref the
    /// sentence names (SQUASHTIDY1c).
    /// <paramref name="force"/> is the person saying it again, meaning it. The record
    /// forgets it once it is gone. <b>It asks no sessions</b>, so forced it would take a tree a live session holds: the
    /// person's doors reach it through <see cref="SessionBranchDiscard"/>, which keeps such a branch (LAND3c).
    /// </remarks>
    internal async Task<TreeRemoval> RemoveBranchAsync(string root, string repository, string branch, bool force = false, CancellationToken ct = default)
    {
        if (!branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal))
        {
            return new(false, $"`{branch}` is not a session branch — only Daoris's own (`{TreeFollows.SessionPrefix}…`) are removed here, and a "
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
            removals.Forget(repository, branch);
            return new(false, $"there is no branch `{branch}` in `{repository}` — it is gone already.");
        }

        // A list git could not give is never "no tree here" (AUTOTIDY1): forced, it would take the branch from under a tree.
        if (await context.Worktrees.ReadAsync(root, ct).ConfigureAwait(false) is not { } worktrees)
        {
            return new(false, $"`{branch}` stays: {ListUnread}, so whether a tree has it checked out is not known.");
        }

        if (worktrees.GetValueOrDefault(branch) is { } tree)
        {
            return context.Holds(tree)
                ? await removals.RemoveAsync(tree, force, ct).ConfigureAwait(false)
                : new(false, $"`{branch}` is checked out at {tree}, which is not a session tree — Daoris removes nothing there.");
        }

        ContentHold? held = null;
        LandedBranch? squashedOn = null;
        string? kept = null;
        if (!force)
        {
            // The commit judged, read once (SQUASHTIDY1c): every question below is asked of it, and the branch goes only while it
            // is still that commit.
            var (tipCode, tipOut, tipErr) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct).ConfigureAwait(false);
            var judged = tipOut.Trim();
            // The line it grew against, its forms at their commits read before any proof (FABRIC-END4a, second review): the delete verifies
            // them, and the branch of the person's holding its commits, in one transaction.
            var grewIn = context.Grown.All().LastOrDefault(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                                                    && string.Equals(entry.Branch, branch, StringComparison.Ordinal))?.Workspace;
            var line = (await context.LineAsync(root, repository, RemoteTarget.Workspace(grewIn), ct).ConfigureAwait(false)).Branch;
            var forms = tipCode != 0 ? [] : await ContentHolds.LineRefsAsync(root, line, ct).ConfigureAwait(false);
            if (forms is null)
            {
                return new(false, $"Daoris cannot read the line `{branch}`'s work is judged against, so it stays. Say it again with --force to discard it.");
            }

            var (logCode, unlanded, logErr) = tipCode != 0 ? (tipCode, "", tipErr) : await TreeFollows.UnlandedLogAsync(root, judged, ct).ConfigureAwait(false);
            if (logCode != 0)
            {
                return new(false, $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(logErr)} "
                    + "The branch stays; say it again with --force to discard it.");
            }

            // SQUASHTIDY1: the same proof by content as a tree's removal, against the line of the workspace it grew in; before it,
            // a squashing landing's record, which vouches as ancestry would and keeps no ref (PROCESS1b-ii point 3).
            if (!string.IsNullOrWhiteSpace(unlanded)
                && (squashedOn = await squashes.SquashedHolderAsync(root, repository, judged, ct).ConfigureAwait(false)) is null)
            {
                var proof = await ContentHolds.HeldByContentAsync(root, judged, line, content.LandedHere(repository), ct).ConfigureAwait(false);
                held = proof.Hold;
                if (proof.Unread is { } unread)
                {
                    // PROOF-UNREAD1: a proof git could not read is not "held nowhere"; the refusal says it could not tell.
                    return new(false, $"Daoris cannot tell whether the work on `{branch}` is held by content: {unread}. "
                        + "The branch stays; say it again with --force to discard it.");
                }

                if (held is null)
                {
                    return new(false, $"`{branch}` holds commits no branch of yours holds:\n{unlanded.Trim()}\n"
                        + "If its work is not wanted — a failed or superseded attempt — say it again with --force to discard them.");
                }

                // SQUASHTIDY1c: the proof compared files, so the commits themselves are kept on a ref of their own first.
                var (reference, why) = await ContentHolds.KeepDiscardedAsync(root, branch, judged, ct).ConfigureAwait(false);
                if (reference is null)
                {
                    return new(false, $"Daoris could not keep the commits on `{branch}` on a ref of their own ({why}), so it stays. "
                        + "Say it again with --force to discard them.");
                }

                kept = reference;
            }

            var proven = await ContentHolds.ProofRefsAsync(root, branch, judged, forms, ContentHolds.PersonsRef, ct).ConfigureAwait(false);
            var stays = proven is null
                ? "Daoris could not read what its proof rests on"
                : await content.DeleteJudgedAsync(root, branch, judged, proven, ct).ConfigureAwait(false);
            if (stays is not null)
            {
                await ContentHolds.DropKeptAsync(root, branch, kept, judged, ct).ConfigureAwait(false);
                return new(false, $"`{branch}` stays: {stays}"
                    + (stays == ContentHolds.MovedSince ? ", so it holds work this removal did not judge." : "."));
            }
        }
        else
        {
            var (deleteCode, _, deleteErr) = await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
            if (deleteCode != 0) return new(false, $"git would not delete `{branch}`: {FirstLine(deleteErr)}");
        }

        removals.Forget(repository, branch);
        return new(true, $"removed the session branch `{branch}` from `{repository}`"
            + (held is not null ? $": {held.Said}." : squashedOn is not null ? $": {TreeSquashes.SquashedSaid(squashedOn)}." : ".")
            + (kept is null ? "" : $" {ContentHold.KeptAt(branch, kept)}"));
    }
}
