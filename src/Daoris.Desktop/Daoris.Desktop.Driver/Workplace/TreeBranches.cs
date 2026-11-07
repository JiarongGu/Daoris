using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.LandingSayings;

namespace Daoris.Driver;

/// <summary>The branch form of a landing: a new branch made from the session's commits, or a recorded one advanced.</summary>
internal sealed class TreeBranches(TreeContext context, TreeFollows follows, TreeLandSyncs syncs)
{
    /// <summary>
    /// Put a session's work on a new branch, from the session's own branch, which grew from the line.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>It writes nothing to the checkout</b>: it makes one ref in the repository and moves no
    /// checkout, so the root may be dirty and on any branch, and is left exactly so. A branch of that
    /// name already there is refused and never moved — it may be somebody's. Nothing pushes.
    /// </remarks>
    /// <param name="handedOn">A plugin pushes it next, so the sentence does not tell the person to.</param>
    /// <param name="squash">
    /// The session the rule squashes, whose title and id the one commit's message says; null where the rule keeps
    /// the session's own commits. A tree on a branch of its own is never squashed.
    /// </param>
    /// <param name="sync">
    /// What the line holds of the tree's commits, where it holds some, or where it
    /// only moved since the tree left it: only the rest is replayed, aside, onto the line's latest, and the branch is made at
    /// the replay's tip; a branch the landing advances takes what neither it nor the line holds, replayed onto its tip, and nothing where
    /// the line only moved; a branch of the tree's own is replayed in place, unless it is on its remote and the line only moved. Null
    /// lands the commits as made.
    /// </param>
    /// <param name="join">
    /// The tree's judgement where it joins its entry's branch, even where the line holds none of its commits: where the
    /// branch stands and the tree's tip does not grow from it, what neither holds is replayed onto the branch's tip, as
    /// <paramref name="sync"/>'s would be. Null advances only by the session's own tip.
    /// </param>
    /// <param name="pinned">
    /// The tip the door judged (<see cref="LandingGate.Tip"/>): the session's branch must stand there when its
    /// commits are read, or nothing lands, and the branch is made or moved to that commit, so one made after the judgement never lands
    /// unseen. Null lands the branch as it stands, as the press does.
    /// </param>
    /// <param name="uncommitted">
    /// The tree's uncommitted paths as the landing's judgement counted them a moment before; null asks git.
    /// </param>
    /// <param name="here">The tree's repository and branch as its landing read them a moment ago; null reads them here.</param>
    /// <param name="delivering">
    /// The delivery it performs: <paramref name="name"/> is its intent's, and the branch's ref is written by its one transaction under
    /// the checkout's lock, each in its phase, the tree's own branch verified standing at the tip or, replayed in place, moved from it while its
    /// tree is detached. Null writes the ref.
    /// </param>
    internal async Task<Branched> BranchAsync(
        string full, string workspace, string repository, string name, bool handedOn, CancellationToken ct, bool entered = false,
        LandingSubject? squash = null, LandSync? sync = null, LandSync? join = null, string? pinned = null, int? uncommitted = null,
        CommonDirAndBranch? here = null, TreeDelivering? delivering = null)
    {
        if (!context.Holds(full))
        {
            return new TreeLanding(false, $"{full} is not a session tree — this lands only trees Daoris opened, under {context.TreesRoot}.");
        }

        if (!Directory.Exists(full))
        {
            return new TreeLanding(false, $"there is no tree at {full}.");
        }

        // Its repository and its branch, one start, or the landing's own read of them a moment ago: what it lands
        // is read from that branch below, as it stands.
        here ??= await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (here.Code != 0)
        {
            return new TreeLanding(false, $"{full} is not a working tree git recognises: {FirstLine(here.Err)}");
        }

        var root = Path.GetDirectoryName(here.CommonDir.Trim())!;
        var branch = here.Branch.Trim();
        if (branch is "" or "HEAD")
        {
            return new TreeLanding(false, $"the tree at {full} is not on a branch, so there is nothing to land.");
        }

        // Uncommitted work would not travel with the branch — the same reason the merge door gives.
        if ((uncommitted ?? await TreeMerges.DirtyAsync(full, ct).ConfigureAwait(false)) is > 0 and var dirty)
        {
            return new TreeLanding(false,
                $"the session's tree has uncommitted work — {dirty} path(s) — which a "
                + "branch would leave behind. Commit it in the tree first, or decide it is not wanted.")
            { Refusal = LandingCode.Uncommitted };
        }

        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? "HEAD" : await ComparableAsync(root, line, ct).ConfigureAwait(false) ?? line;
        // Oldest first, each commit's id and subject: what a plugin writes a pull request from.
        var (logCode, ahead, logErr) = await WorkingTree.GitAsync(
            root, ["log", "--reverse", "--format=%H%x09%s", $"{against}..{branch}"], ct).ConfigureAwait(false);
        if (logCode != 0)
        {
            return new TreeLanding(false, $"git cannot compare `{branch}` with `{line ?? "HEAD"}` here: {FirstLine(logErr)} "
                + "Fetch it, or set another line with `daoris place line`.");
        }

        if (string.IsNullOrWhiteSpace(ahead))
        {
            return new TreeLanding(false, $"`{branch}` holds nothing `{line ?? "HEAD"}` does not — nothing to land.")
            { Refusal = LandingCode.Nothing };
        }

        var commits = ahead.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('\t', 2))
            .Select(parts => new LandingCommit(parts[0], parts.Length > 1 ? parts[1] : ""))
            .ToList();

        // Pinned, the work read here is the work judged, or nothing lands.
        if (pinned is not null && !string.Equals(commits[^1].Sha, pinned, StringComparison.OrdinalIgnoreCase)) return Moved(branch, commits[^1].Sha, pinned);

        // The tree its agent moved onto a branch of its own lands as that branch stands: it is already the
        // pull request's branch, named as the repository names its own, so none is made and the rule's pattern is not asked.
        if (!branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal))
        {
            if (entered) return new TreeLanding(false, EnteredOwn(branch));
            // A delivery lands the branch it names; the tree's own is landed only where it is that one.
            if (delivering is not null && !string.Equals(branch, name, StringComparison.Ordinal))
            {
                return new TreeLanding(false, $"Nothing was landed: this session's tree is on its own branch, `{branch}`, and its delivery lands `{name}`.")
                { Refusal = LandingCode.Refused };
            }

            var pushOwn = handedOn ? "" : $" Push it and open the pull request from there: `git push -u origin {branch}`.";
            // Where the line holds some of its commits, the branch is replayed in place and keeps its name; one on its
            // remote is never rewritten, and is refused as a conflict is. So is one whose line only moved, unless it is
            // on its remote, where it lands as it stands, its pull request the place it meets the line.
            var pushed = sync is not null && await OnRemoteAsync(root, branch, ct).ConfigureAwait(false);
            if (sync is not null && !(pushed && sync.Held.HoldsNone))
            {
                if (pushed) return TreeLandSyncs.OwnOnRemote(sync);
                var (made, refused) = await syncs.ReplayAsync(sync, repository, workspace, sync.Onto, sync.Held.Kept, ct).ConfigureAwait(false);
                if (refused is not null) return refused;
                // A delivery's ref is the branch its tree has out, moved from the tip judged by its one transaction.
                if (await syncs.MoveTreeAsync(full, sync, made!.Now, repository, workspace, inUse: null, ct, delivering).ConfigureAwait(false) is { } stays)
                {
                    return new TreeLanding(false, $"Nothing was landed: `{branch}`, the session's own branch, could not be replayed in place ({stays}). "
                        + "The tree and its branch are as they were.") { Refusal = LandingCode.Refused, LeftOut = sync.Held.LeftOut };
                }

                return new Branched(
                    new TreeLanding(true,
                        $"lands `{branch}`, the session's own branch, replayed in place — {LandingSyncWords.Replayed(made.Landed.Count, sync.OntoName, made.LeftOut)}."
                        + $"{LandingSyncWords.Emptied(made.LeftOut)}{LandingSyncWords.Settled(made)} No other branch was made.{pushOwn} "
                        + "Nothing was merged and the checkout was not touched."
                        + $"{LandingSyncWords.Followed(null)}{TreeStays}",
                        branch) { LeftOut = made.LeftOut },
                    root, line, made.Landed, follows.BranchOfTree(full)?.Branch)
                { Replay = made, Followed = true };
            }

            // The delivery's ref is the tree's own branch, verified standing at the tip it pinned.
            if (delivering is not null && await delivering.WriteAsync(root, $"refs/heads/{branch}", commits[^1].Sha, commits[^1].Sha, ct).ConfigureAwait(false)
                    is { Code: not 0 } unverified)
            {
                return new TreeLanding(false, $"Nothing was landed: `{branch}` was not read standing at {TextBounds.ShortCommit(commits[^1].Sha, 8)}: "
                    + $"{FirstLine(unverified.Stderr)}") { Refusal = LandingCode.Refused };
            }

            return new Branched(
                new TreeLanding(true,
                    $"lands `{branch}`, the session's own branch, as it stands — {commits.Count} commit(s) from `{line ?? "HEAD"}`, and no "
                    + $"other branch was made.{pushOwn} Nothing was merged and the checkout was not touched.{TreeStays}",
                    branch),
                root, line, commits, follows.BranchOfTree(full)?.Branch);
        }

        if (!BranchName.IsValid(name)) return new TreeLanding(false, Untaken(name, entered));

        // The squash is a commit Daoris makes, so git's own identity here makes it, asked before anything is.
        if (squash is not null && await TreeSquashes.IdentityProblemAsync(context.Home, root, repository, ct).ConfigureAwait(false) is { } nobody)
        {
            return new TreeLanding(false, nobody);
        }

        var (exists, standing, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{name}^{{commit}}"], ct).ConfigureAwait(false);
        if (exists == 0 && entered && context.Recorded.Of(repository, name) is null)
        {
            return new TreeLanding(false, EnteredStands(name, repository)) { Refusal = LandingCode.Exists };
        }

        // The branch this delivery wrote and read back before Daoris stopped goes on to its record:
        // never judged as a branch another made, never written twice; an advance from where the record had it.
        if (exists == 0 && delivering?.Wrote(name) is { } wrote && string.Equals(wrote, standing.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var was = context.Recorded.Of(repository, name)?.Tip;
            var from = was is not null && !string.Equals(was, wrote, StringComparison.OrdinalIgnoreCase) ? was : null;
            var holds = await LineHolds.CommitsAsync(root, $"{against}..{wrote}", ct).ConfigureAwait(false) ?? commits;
            var pushOn = handedOn ? "" : $" Push it and open the pull request from there: `git push -u origin {name}`.";
            return new Branched(
                new TreeLanding(true,
                    $"put the work on `{name}` — {holds.Count} commit(s) from `{line ?? "HEAD"}`, the branch its delivery wrote before Daoris stopped, "
                    + $"recorded now and not written again.{pushOn} Nothing was merged and the checkout was not touched.{TreeStays}",
                    name) { AdvancedFrom = from },
                root, line, holds, branch)
            { Squashed = squash is null ? null : pinned ?? commits[^1].Sha };
        }

        if (exists == 0)
        {
            // A chain's later done moves the chain's branch on: only one Daoris made, as a fast-forward, or under
            // a squash by one more commit.
            var standsAt = standing.Trim();
            // Under a squash the branch holds the session tip it squashed by content alone: work grown from that
            // tip grows from the branch, and its commits up to it are on the branch already.
            var squashedTip = squash is null ? null : context.Recorded.Of(repository, name)?.Squashed;
            var grown = squashedTip ?? standsAt;
            // A part that ran alongside, its work not grown from the entry's branch, joins it by
            // the same replay as what the line partly holds; one grown from it still moves it by its own tip. A line that only
            // moved replays nothing onto a standing branch, which grows from its own tip and is never rewritten.
            var holding = sync is { Held.HoldsNone: false } ? sync : null;
            var onto = holding ?? (join is not null && !await IsAncestorAsync(root, grown, join.Tip, ct).ConfigureAwait(false) ? join : null);
            if (onto is null)
            {
                return await AdvanceAsync(root, repository, name, standsAt, branch, line, commits, handedOn, ct, against, squash, pinned: pinned, delivering: delivering)
                    .ConfigureAwait(false);
            }

            // What neither the branch nor the line holds, replayed onto the branch's tip, which then moves by
            // fast-forward.
            var lacking = new List<LandingCommit>();
            foreach (var commit in onto.Held.Kept)
            {
                if (await IsAncestorAsync(root, commit.Sha, standsAt, ct).ConfigureAwait(false)) continue;
                if (squashedTip is not null && await IsAncestorAsync(root, commit.Sha, squashedTip, ct).ConfigureAwait(false)) continue;
                lacking.Add(commit);
            }

            var (onBranch, refusedOn) = await syncs.ReplayAsync(onto, repository, workspace, standsAt, lacking, ct, joining: holding is null ? name : null)
                .ConfigureAwait(false);
            if (refusedOn is not null) return refusedOn;
            var holds = await LineHolds.CommitsAsync(root, $"{against}..{onBranch!.Now}", ct).ConfigureAwait(false) ?? onBranch.Landed;
            var advanced = await AdvanceAsync(root, repository, name, standsAt, branch, line, holds, handedOn, ct, against, squash, onBranch.Now, delivering: delivering)
                .ConfigureAwait(false);
            // A part that joined with nothing the line holds left nothing out: said as a landing of the commits as made says it.
            var joined = holding is null;
            return !advanced.Landing.Landed ? advanced : advanced with
            {
                Landing = advanced.Landing with
                {
                    LeftOut = joined && onBranch.LeftOut.Count == 0 ? null : onBranch.LeftOut,
                    Message = BeforeTreeStays(advanced.Landing.Message,
                        (joined ? EntryLandWords.Joined(name, onBranch.Landed.Count) : "") + LandingSyncWords.LeftOutLanded(onBranch.LeftOut)),
                },
                Replay = onBranch,
            };
        }

        // One commit of the session's tip's tree, its parent where the session left the line, and the
        // branch made at it; the session's own branch is left as it is, and the record keeps the tip squashed. Pinned, at the tip judged.
        var start = pinned ?? branch;
        string? squashed = null;
        IReadOnlyList<LandingCommit> carried = commits;
        // Only what the line lacks, replayed aside onto the line's latest; the branch is made at the replay's tip.
        LandReplay? replayed = null;
        if (sync is not null)
        {
            var (made, refused) = await syncs.ReplayAsync(sync, repository, workspace, sync.Onto, sync.Held.Kept, ct).ConfigureAwait(false);
            if (refused is not null) return refused;
            replayed = made!;
            start = replayed.Now;
            carried = replayed.Landed;
        }

        if (squash is not null)
        {
            var tip = replayed?.Now ?? commits[^1].Sha;
            var (baseCode, mergeBase, baseErr) = replayed is not null && sync is not null
                ? (0, sync.Onto, "")
                : await WorkingTree.GitAsync(root, ["merge-base", against, tip], ct).ConfigureAwait(false);
            if (baseCode != 0 || mergeBase.Trim().Length == 0)
            {
                return new TreeLanding(false, $"git cannot say where `{branch}` left `{line ?? "HEAD"}`, so its work could not be squashed: "
                    + $"{FirstLine(baseErr)} Nothing was landed.");
            }

            var squashing = replayed?.Landed ?? commits;
            // A delivery's message, frozen in its intent, is the commit's opening; under Current none is named.
            var said = delivering?.Template;
            var (made, why) = await TreeSquashes.SquashAsync(context.Home, root, tip, mergeBase.Trim(), SquashMessage.Of(squash.SquashTitle, squashing, squash.Session, said), ct)
                .ConfigureAwait(false);
            if (made is null) return new TreeLanding(false, $"git would not make the squashed commit for `{name}`: {why} Nothing was landed.");
            start = made;
            squashed = tip;
            carried = [new LandingCommit(made, SquashMessage.Subject(squash.SquashTitle, squashing, squash.Session, said))];
        }

        var (code, _, err) = delivering is null
            ? await WorkingTree.GitAsync(root, ["branch", name, start], ct).ConfigureAwait(false)
            : await delivering.WriteAsync(root, $"refs/heads/{name}", null, start, ct).ConfigureAwait(false);
        if (code != 0)
        {
            return new TreeLanding(false, $"git would not make `{name}`: {FirstLine(err)}");
        }

        var pushIt = handedOn ? "" : $" Push it and open the pull request from there: `git push -u origin {name}`.";
        var what = replayed is not null && sync is not null
            ? LandingSyncWords.Replayed(replayed.Landed.Count, sync.OntoName, replayed.LeftOut)
            : $"{commits.Count} commit(s) from `{line ?? "HEAD"}`";
        return new Branched(
            new TreeLanding(true,
                $"put the work on `{name}` — {what}"
                + (squashed is null ? "" : ", squashed into one commit")
                + $".{(replayed is null ? "" : LandingSyncWords.Emptied(replayed.LeftOut) + LandingSyncWords.Settled(replayed))}"
                + $"{pushIt} Nothing was merged and the checkout was not touched.{TreeStays}",
                name) { LeftOut = replayed?.LeftOut },
            root, line, carried, branch)
        { Squashed = squashed, Replay = replayed };
    }

    /// <summary>
    /// Move a standing branch on to the session's work: the chain's branch, which an earlier done
    /// made, grows by this session's commits, so a ticket has one branch and one pull request.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Only a fast-forward of a branch Daoris made, and never forced.</b> It must be the record's, standing at
    /// the recorded tip, checked out in no working tree, with its work not already on the line, and the session's tip must grow
    /// from it with something new (<see cref="LandingAdvance"/>). Then git moves it by compare-and-swap from the tip judged
    /// (<c>update-ref</c>), so a branch moved in between is refused by git itself. Like the branch form, it writes one ref and
    /// touches no checkout.</para>
    ///
    /// <para><b>A branch whose pull request is merged is not moved on</b>: the content proof on the recorded tip, the
    /// same one the clean-up and the hand-off make, finds its work on the line, and commits added now would ride no pull
    /// request.</para>
    /// </remarks>
    /// <param name="standsAt">The commit the branch stands at, read a moment ago.</param>
    /// <param name="branch">The session's own branch.</param>
    /// <param name="commits">What the branch would hold beyond the line once moved: what a plugin is told, as at a first landing.</param>
    /// <param name="against">The line's form the commits were counted from, which a squash's branch is counted from again.</param>
    /// <param name="squash">
    /// The session the rule squashes: the advance is one more commit on the branch, its tree the session's
    /// tip's, its parent the branch's tip, its message only what was added since the recorded tip; null is the fast-forward.
    /// </param>
    /// <param name="replayed">
    /// The tip of what the line lacks replayed onto the branch's, which the branch moves to in place of the session's tip;
    /// null moves it to the session's tip.
    /// </param>
    /// <param name="pinned">The session's tip the door judged, which the branch moves to in place of the tip read now.</param>
    /// <param name="delivering">The delivery it performs, whose transaction moves the branch in its phase; null moves it outside any delivery.</param>
    private async Task<Branched> AdvanceAsync(
        string root, string repository, string name, string standsAt, string branch, string? line,
        IReadOnlyList<LandingCommit> commits, bool handedOn, CancellationToken ct, string? against = null, LandingSubject? squash = null,
        string? replayed = null, string? pinned = null, TreeDelivering? delivering = null)
    {
        var recorded = context.Recorded.Of(repository, name);
        var (_, headOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct)
            .ConfigureAwait(false);
        var head = replayed ?? pinned ?? headOut.Trim();
        // What the session's work must grow from: the tip the record gives as squashed where it gives one, else the tip
        // the branch got when made or last moved, its commits the session's own; a replay onto that tip grows from it.
        var grewFrom = LandingAdvance.GrewFrom(recorded, squash is not null, replayed is not null);

        // Each fact read only where the one before it allowed an advance, in the order the refusals are said.
        string? checkedOut = null;
        string? completed = null;
        var descends = false;
        var newer = 0;
        if (recorded is not null && string.Equals(standsAt, recorded.Tip, StringComparison.OrdinalIgnoreCase))
        {
            checkedOut = (await WorktreesAsync(root, ct).ConfigureAwait(false)).GetValueOrDefault(name);
            if (checkedOut is null)
            {
                var proof = await LandedProofs.ProveAsync(root, recorded.Tip, line, await LandedProofs.LineFormsAsync(root, line, ct).ConfigureAwait(false), ct)
                    .ConfigureAwait(false);
                completed = proof.Kind is LandedKind.Merged or LandedKind.OnLine ? proof.Where : null;
            }

            if (checkedOut is null && completed is null && head.Length > 0)
            {
                var (ancestor, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", grewFrom!, head], ct)
                    .ConfigureAwait(false);
                descends = ancestor == 0;
                if (descends)
                {
                    var (_, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", $"{grewFrom}..{head}"], ct)
                        .ConfigureAwait(false);
                    newer = int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
                }
            }
        }

        if (LandingAdvance.Refusal(name, repository, branch, new AdvanceFacts(recorded, standsAt, checkedOut, descends, newer, completed))
            is { } refused)
        {
            return refused;
        }

        if (squash is not null)
        {
            return await TreeSquashes.AdvanceSquashedAsync(context.Home, root, name, standsAt, branch, line, head, grewFrom!, handedOn, against, squash, ct, delivering)
                .ConfigureAwait(false);
        }

        // Compare-and-swap from the tip judged: git refuses where the branch moved since, and nothing is forced.
        var (moved, _, moveErr) = delivering is null
            ? await WorkingTree.GitAsync(
                root, ["update-ref", "-m", $"daoris: {branch} moves {name} on", $"refs/heads/{name}", head, standsAt], ct).ConfigureAwait(false)
            : await delivering.WriteAsync(root, $"refs/heads/{name}", standsAt, head, ct).ConfigureAwait(false);
        if (moved != 0)
        {
            return new TreeLanding(false, $"git would not move `{name}` on from {TextBounds.ShortCommit(standsAt, 8)}: "
                + $"{FirstLine(moveErr)} It may have moved meanwhile; nothing was changed.")
            { Refusal = LandingCode.Exists };
        }

        var pushIt = handedOn ? "" : $" Push it to grow its pull request: `git push origin {name}`.";
        return new Branched(
            new TreeLanding(true,
                $"moved `{name}` on from {TextBounds.ShortCommit(standsAt, 8)} to this session's work — {newer} more commit(s), "
                + $"{commits.Count} from `{line ?? "HEAD"}` in all.{pushIt} Nothing was merged and the checkout was not touched.{TreeStays}",
                name)
            { AdvancedFrom = standsAt },
            root, line, commits, branch);
    }
}
