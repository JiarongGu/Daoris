using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.LandingSayings;

namespace Daoris.Driver;

/// <summary>The landing itself: the plan taken, the merge or the branch made, the record kept, the plugin told and the tree moved on.</summary>
internal sealed class LandingPress(
    TreeContext context, TreeWorkReads workReads, TreeLandSyncs syncs, TreeLandPlans plans, TreeMerges merges, TreeBranches branches)
{
    /// <summary>
    /// The commit a landing that waited would land: the tip its door judged, where the door pinned one, else the
    /// tree's head as it stands. Read only after a wait; null where git cannot say, which lands nothing as `already`.
    /// </summary>
    /// <summary>
    /// <paramref name="delivery"/> entered for its landing into <paramref name="entry"/> (1b3, 1h): held in this process while it
    /// lands, read from its store, its standing automatic authority judged again, and its preparation written ahead, so each refusal after
    /// withdraws it in its words; one done before a restart is not written again. Else the landing's refusal, and nothing entered.
    /// </summary>
    internal TreeLanding? Enter(DeliveryEntry entry, TreeDelivery delivery, string repository)
    {
        var performing = DeliveryRuns.Hold(context.Home, delivery.Claim.Key);
        if (performing is null)
        {
            return new(false, $"Nothing was landed: its delivery {delivery.Claim.Key[..12]} is being landed or settled here now; accept again once that ends.")
            {
                Refusal = LandingCode.Refused,
            };
        }

        TreeDelivering? read = null;
        var refused = delivery.Store.Delivery(delivery.Claim.Key) is { Settled: DeliverySettlements.Delivered } delivered ? TreeDelivering.Meanwhile(delivered) : null;
        if (refused is null)
        {
            (read, var unread) = TreeDelivering.Read(delivery, context.Home, repository);
            refused = read is null ? new(false, $"Nothing was landed: {unread}.") { Refusal = LandingCode.Refused }
                : read.Unauthorized() is { } revoked ? new(false, $"Nothing was landed: {revoked}; your Accept lands it.") { Refusal = LandingCode.Off }
                : read is { Next: DeliveryPhases.Prepared } && read.Begin(DeliveryPhases.Prepared, read.Preparing) is { } notBegun
                    ? new(false, $"Nothing was landed: {notBegun}.") { Refusal = LandingCode.Refused }
                : null;
        }

        if (refused is not null)
        {
            performing.Dispose();
            return refused;
        }

        (entry.Delivering, entry.Performing) = (read, performing);
        return null;
    }

    internal static async Task<string?> JudgedAsync(string path, string? pinned, CancellationToken ct)
    {
        if (pinned is not null) return pinned;
        var (code, head, _) = await WorkingTree.GitAsync(Path.GetFullPath(path), ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct).ConfigureAwait(false);
        return code == 0 ? head.Trim() : null;
    }

    /// <summary>
    /// What a landing says that waited for another of its tree, which landed this very commit meanwhile (its review): where
    /// it is, who accepted it, and that nothing more landed. Kept as <see cref="LandingCode.Already"/>, as the look keeps a press's landing.
    /// </summary>
    internal static TreeLanding LandedMeanwhile(LandingAttempt before, string session)
    {
        var who = before.AcceptedBy == AcceptedBy.Auto ? "accepted automatically when its quest was done" : "accepted by a press of Accept";
        return new(false,
            $"another landing of this session finished while this one waited for it: its work is on `{before.Branch}` at "
            + $"{TextBounds.ShortCommit(before.Tip, 8)}, {who}. Nothing more was landed.", before.Branch)
        {
            Refusal = LandingCode.Already,
        };
    }

    /// <summary>The landing itself, as <see cref="LandAsync"/> says it.</summary>
    /// <param name="hold">The tree's hold, which keeps what this landing came to for a door waiting behind it.</param>
    /// <param name="uncommitted">The tree's uncommitted paths as the door read them, as <see cref="LandAsync"/> takes them.</param>
    /// <param name="fence">
    /// The delivery it performs, its preparation written ahead; empty under Current, whose door writes one into it here once the
    /// landing is judged, and null lands by the rule.
    /// </param>
    internal async Task<TreeLanding> LandingAsync(
        string path, LandingSubject subject, CancellationToken ct, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse,
        string acceptedBy, IWorkplaceGate? gate, bool joins, bool fetched, LandingHold? hold,
        int? uncommitted = null,
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null, DeliveryEntry? fence = null)
    {
        var delivering = fence?.Delivering;
        // One look: the landing and its tidy ask git's guess at the line once.
        using var look = context.OneLook();
        // And what a commit's id answers, once, in the planning up to the branch made (GitFacts); closed before the tidy.
        using var facts = GitFacts.Within();
        var full = Path.GetFullPath(path);
        // The tree's repository and the branch it is on, read once and handed to the planning's readers (the content proof,
        // the judgement against the line, the merge or the branch made), each of which asked it again. Only of a tree of this home's that
        // is here, as each reader asks; what an act rests on (the branch's commits, the head the tree moves from) is read again before it.
        var here = context.Holds(full) && Directory.Exists(full) && await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false) is { Code: 0 } read
            ? read : null;
        // From that read, before anything is judged or written, that git reads the tree as itself. Bound to another
        // checkout, its status and its files are that checkout's, and a replay or a follow in it would rewrite them: nothing lands.
        if (here is not null && await TreeBinding.RefusalAsync(full, ct, here.TopLevel).ConfigureAwait(false) is { } bound)
        {
            return new(false, $"Nothing was landed: {bound}.") { Refusal = LandingCode.Refused };
        }

        // The head's own judgement, asked first, since a second opinion or a review of work already on the line
        // lands nothing either. A graph version that lands by a `land` judges it as its `content.holds` step, or holds none.
        var work = await workReads.WorkAsync(full, ct, uncommitted, here).ConfigureAwait(false);
        if (work is not null && gate?.Process?.JudgesHolds != false && DiscardOffer.Of(full, work) is { } carried)
        {
            return new(false, carried.NotLanded) { Refusal = LandingCode.Carried };
        }

        // The dirty check below is told what that judgement's `status` counted, and asks git only where it could not say. Nothing
        // of the landing's writes in the tree before it: a replay is made aside, and the tree follows only after the merge or the branch.
        uncommitted = work?.Uncommitted;

        // One gate, in a fixed order: the run's own part, the second opinion, then the
        // person's review where it runs, then the landing step's own. A merge rule waits as a branch rule does, and an advance waits
        // the same way.
        if (gate?.Refusal is { } refused) return refused;
        var review = gate?.ReviewLanding;

        var (workspace, repository) = context.OwnerOf(full);
        // The repository's private patterns live in its checkout, which a tree does not hold: what the work adds is read against them
        // here, before anything is made, whatever the landing's form.
        if (here is not null)
        {
            var checkout = Path.GetDirectoryName(here.CommonDir.Trim())!;
            if ((await context.LineAsync(checkout, repository, workspace, ct).ConfigureAwait(false)).Branch is { } lineName
                && await SensitiveLandings.RefusalAsync(checkout, full, lineName, ct).ConfigureAwait(false) is { } sensitive)
            {
                return new(false, sensitive) { Refusal = LandingCode.Refused };
            }
        }

        // The run's bound version decides how it lands, read with the gate: a version is never edited, so nothing changes under it.
        var process = gate?.Process;
        var named = delivering is null && process is { Named: true } ? process : null;
        var landing = process is { Named: true } ? process.Landing : Landing.Merge;
        // A delivery lands as its intent says.
        if (delivering is not null) landing = delivering.Over(landing);
        // A landing with no press needs a graph with no review before its `land`. A delivery's authority is its authorization's.
        if (delivering is null && acceptedBy == AcceptedBy.Auto && landing is not { AutoAccept: true })
        {
            return new(false, "its workflow does not accept it automatically, so nothing was landed: your Accept lands it.")
            {
                Refusal = LandingCode.Off,
            };
        }

        // A delivery lands into the line its intent names, or nothing; an integrate's is the next node's, its branch made from
        // the tree's own line as any branch is.
        if (delivering is { Intent.Method: not DeliveryMethods.Integrate } && here is not null
            && (await context.LineAsync(Path.GetDirectoryName(here.CommonDir.Trim())!, repository, workspace, ct).ConfigureAwait(false)).Branch is var stands
            && !string.Equals(stands, delivering.Intent.Line, StringComparison.Ordinal))
        {
            return new(false, $"Nothing was landed: its delivery lands into `{delivering.Intent.Line}`, and `{repository}`'s line is "
                + (stands is null ? "none git can name" : $"`{stands}`") + " now.") { Refusal = LandingCode.Refused };
        }

        // The commit the door judged, or the delivery pinned: what lands is that commit or nothing.
        var pinned = delivering?.Intent.Tip ?? gate?.Tip;

        TreeLanding landed;
        // The session tip a squash made the branch's commit from: the tidy's proof of what the landing holds; and the
        // commit the squash made, which binds the branch the tidy reads to this landing.
        string? squashedTip = null;
        string? squashedInto = null;
        var branchForm = landing.Form == LandingForm.Branch;
        // A name entered where nothing is branched is refused, not ignored: the person would think it is used. A delivery's
        // name is its intent's.
        if (delivering is null && !branchForm && subject.EnteredName is not null) return new(false, EnteredMerge);
        // Only what the line lacks lands. The person's press fetches the line once first,
        // a failed fetch said and judged against the last one; the look's automatic landing has no press and fetches nothing.
        var (sync, notFetched) = await syncs.JudgeAsync(full, repository, workspace, branchForm, fetch: acceptedBy != AcceptedBy.Auto && !fetched, ct, here)
            .ConfigureAwait(false);
        // And where the line moved since the tree left it, the work is brought onto its tip first, by the same
        // replay, so what lands descends from the line's tip and a conflict is the session's to resolve, never the merge's.
        var replaying = sync is { Held.Replays: true } ? sync : null;
        if (replaying is { Held.Kept.Count: 0 })
        {
            // The fetch brought what holds all of it: the discard offer's sentence where its proof now finds it, else nothing to land.
            return Fetched(await workReads.DiscardOfferAsync(full, ct).ConfigureAwait(false) is { } heldNow
                ? new(false, heldNow.NotLanded) { Refusal = LandingCode.Carried, LeftOut = replaying.Held.LeftOut }
                : new(false, $"`{replaying.Branch}` holds nothing `{replaying.OntoName}` lacks: the line already holds "
                    + $"{string.Join(", ", replaying.Held.LeftOut.Select(LandingSyncWords.Each))} — nothing to land.")
                { Refusal = LandingCode.Nothing, LeftOut = replaying.Held.LeftOut }, notFetched);
        }

        // Under Current the door writes the delivery here, the landing judged and nothing of it made,
        // and lands through it; its intent is read from the rule as it stood a moment ago. Null where it lands as before, a refusal in words.
        // So does a run bound to a graph version that lands by a `land`, its intent the version's; one that lands by a `handed`, and a
        // steps version, land as before until they end.
        async Task<TreeLanding?> DeliverAsync(string? name)
        {
            var bound = named?.Land(subject);
            if (fence is null || delivering is not null || (named is not null && bound is null) || here is null) return null;
            var root = Path.GetDirectoryName(here.CommonDir.Trim())!;
            var tree = here.Branch.Trim();
            var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
            var tip = pinned ?? TreeTips.Read(full) ?? await CommitOfAsync(root, $"refs/heads/{tree}", ct).ConfigureAwait(false);
            if (line is null || tip is null || tree is "" or "HEAD") return null;
            // The standing automatic authority judged again before the first effect: a version's, which is never edited, its graph holding
            // no review before its `land`; work no version governs has none.
            Func<string?> standing = named is { } version
                ? () => version.Landing.AutoAccept ? null : "its workflow's graph holds a review before its landing"
                : () => "no workflow it follows accepts it automatically";
            var (delivery, refusal) = await CurrentDeliveries.StartAsync(
                context.Home, landing, repository, workspace, root, subject, tip, line, tree, name, acceptedBy, hold?.Waited == true,
                standing, ct, bound).ConfigureAwait(false);
            if (refusal is not null || delivery is null) return refusal;
            if (Enter(fence, delivery, repository) is { } notEntered) return notEntered;
            delivering = fence.Delivering!;
            landing = delivering.Over(landing);
            pinned = delivering.Intent.Tip;
            return null;
        }

        // What a replay made, to move the session's tree to: a merge's here, a branch's from what it made.
        LandReplay? replayed = null;
        var followed = false;
        if (!branchForm)
        {
            if (await DeliverAsync(null).ConfigureAwait(false) is { } undelivered) return Fetched(undelivered, notFetched);
            // Pinned, as a graph run's landing is to the commit its `handed` is bound to, the merge lands
            // that commit or nothing, as a pinned branch landing does: the session's branch must stand there before anything is replayed or
            // merged, and the merge takes the commit itself, so one made after the judgement never lands unseen.
            if (pinned is { } pin && here is not null && await CommitOfAsync(full, $"refs/heads/{here.Branch.Trim()}", ct).ConfigureAwait(false) is { } standsAt
                && !string.Equals(standsAt, pin, StringComparison.OrdinalIgnoreCase))
            {
                return Fetched(Moved(here.Branch.Trim(), standsAt, pin), notFetched);
            }

            if (replaying is not null)
            {
                var (made, notMade) = await syncs.ReplayAsync(replaying, repository, workspace, replaying.Onto, replaying.Held.Kept, ct).ConfigureAwait(false);
                if (notMade is not null) return Fetched(notMade, notFetched);
                replayed = made;
            }

            var merged = await merges.MergeAsync(path, ct, replayed, uncommitted, here, pinned: pinned, beside: beside, delivering: delivering).ConfigureAwait(false);
            landed = new(merged.Merged, merged.Message) { LeftOut = merged.Merged ? replayed?.LeftOut : null, Refusal = merged.Refusal, RootStatus = merged.RootStatus };
            landed = Fetched(landed, notFetched);
        }
        else
        {
            // A squash reads the work's subject whatever the pattern reads: its commit is named by it too.
            var worked = await plans.ForWorkAsync(full, workspace, repository, landing, subject, ct, always: landing.Squash).ConfigureAwait(false);
            var name = delivering?.Intent.Branch ?? BranchNamed(worked, landing, repository);
            // A tree on a branch of its own lands as that branch, which its delivery names, replayed in place or as it stands.
            var own = here?.Branch.Trim() is { } onBranch && onBranch is not ("" or "HEAD") && !onBranch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal)
                ? onBranch : null;
            if (await DeliverAsync(own ?? name).ConfigureAwait(false) is { } undelivered) return Fetched(undelivered, notFetched);
            var branched = await branches.BranchAsync(full, workspace, repository,
                delivering?.Intent.Branch ?? name,
                handedOn: false, ct,
                entered: delivering is not { Door: false } && subject.EnteredName is not null, squash: landing.Squash ? worked : null, sync: replaying,
                join: joins ? sync : null, pinned: pinned, uncommitted: uncommitted, here: here, delivering: delivering).ConfigureAwait(false);
            landed = Fetched(branched.Landing, notFetched);
            replayed = landed.Landed ? branched.Replay : null;
            // A part that joined its entry's branch by a replay follows it as a replay onto the line does.
            if (replayed is not null && replaying is null && joins) replaying = sync;
            followed = branched.Followed;
            squashedTip = landed.Landed ? branched.Squashed : null;
            // Recorded the moment it exists: the branch is Daoris's to judge and to hand on only
            // because this line wrote it down, at the commit of its making.
            var tip = branched.Commits.Count > 0 ? branched.Commits[^1].Sha : null;
            squashedInto = squashedTip is null ? null : tip;
            // A delivery's record is written ahead in its phase, and read back after.
            Task<TreeLanding> RecordedAsync(TreeLanding was, string at, Action write) => delivering is null
                ? Task.FromResult(Remember(was, write))
                : delivering.RecordAsync(was, was.Branch!, at, () => Remember(was, write), () => context.Recorded.Of(repository, was.Branch!));
            if (landed.Landed && tip is not null && landed.AdvancedFrom is { } advancedFrom)
            {
                // Moved on: the entry's tip is the new commit, and the advance is kept with who accepted it.
                var unit = UnitOf(full, repository);
                landed = await RecordedAsync(landed, tip, () => context.Recorded.Advanced(repository, landed.Branch!,
                    new LandedAdvance(advancedFrom, tip, DateTimeOffset.UtcNow, subject.Session)
                    {
                        AcceptedBy = acceptedBy, Review = review, Opinion = gate?.OpinionLanding, Workflow = named?.Record,
                        // Its own acceptance's provenance, as it keeps its own AcceptedBy.
                        Integrate = LandedIntegrate.Of(null, named is not null, named?.IntegrateRequest),
                        // The record moves to the session tip this advance squashed; a fast-forward keeps it.
                        Squashed = branched.Squashed,
                        // Whose work this is, its unit, and the submission it is accepted on.
                        Quest = subject.Served, Unit = unit, Submission = subject.Submission, Brought = subject.Brought,
                    }, unit)).ConfigureAwait(false);
            }
            else if (landed.Landed && tip is not null)
            {
                // Where its work grew from: the session branch's start, so bringing it up to date replays only its own; the line's
                // commit a replay is made onto.
                var from = branched.Replay is not null && replaying is not null ? replaying.Onto
                    : branched.Source is { } source ? context.Grown.Of(repository, source)?.From : null;
                landed = await RecordedAsync(landed, tip, () => context.Recorded.Record(new LandedBranch(
                    repository, workspace, landed.Branch!, branched.Base, tip, subject.Session, subject.Quest, subject.Title,
                    DateTimeOffset.UtcNow)
                {
                    From = from,
                    // Who accepted it, and the rule as it stood: a choice keeps its facts. Where the access
                    // setup turned its switch on, where that is set, so the record says the setup accepted it.
                    AcceptedBy = acceptedBy,
                    Rule = new LandedRule(null, landing.AutoAccept, LandingSource.Default),
                    // That `integrate` stood at this acceptance, its `request` and whose step, so a landing made
                    // before the rule carried it never reads as one made for integration.
                    Integrate = LandedIntegrate.Of(null, named is not null, named?.IntegrateRequest),
                    // The review that let it go: reviewed with its set-up's commit, or skipped.
                    Review = review,
                    // The second opinion that let it go, or why it landed without one.
                    Opinion = gate?.OpinionLanding,
                    // The named version that decided; none under Current.
                    Workflow = named?.Record,
                    // The session tip its one commit is made from: what proves the session landed.
                    Squashed = branched.Squashed,
                    // The branch work unit whose work it carries, where the session's entry has one.
                    Units = UnitOf(full, repository) is { } unit ? [unit] : [],
                    // The quest its session served and the submission it is accepted on.
                    Served = subject.Served,
                    Submission = subject.Submission,
                    // The sessions its combined Accept brought in, in the same write.
                    Brought = subject.Brought,
                })).ConfigureAwait(false);
            }

            // Kept for a door waiting behind this one the moment the work is on its branch, by the session's tip it landed.
            if (landed.Landed && tip is not null) hold?.Keep(new LandingAttempt(landed.Branch!, branched.Squashed ?? tip, acceptedBy));
        }

        // Which named version decided, said where the landing is said, before the tree's
        // sentence a tidy takes out: a merge keeps no landing record, and its conversation's note is its record.
        if (landed.Landed && named is not null)
        {
            var decided = $" It landed as {named.Name} says, {named.Decided}.";
            landed = landed with
            {
                Message = landed.Message.EndsWith(TreeStays, StringComparison.Ordinal)
                    ? landed.Message[..^TreeStays.Length] + decided + TreeStays
                    : landed.Message + decided,
            };
        }

        // The planning's table ends here. What follows moves the tree, judges and removes (the end keeping, the tidy's proof
        // and its delete): each of those reads git as it stands, never what the planning holds.
        GitFacts.Close();

        // The session follows what landed, so its next turn starts in sync with the line; where it
        // cannot move, the landing stands and says why.
        if (landed.Landed && replayed is not null && replaying is not null && !followed)
        {
            var stays = await syncs.MoveTreeAsync(full, replaying, replayed.Now, repository, workspace,
                inUse is null ? null : await inUse(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            landed = landed with { Message = BeforeTreeStays(landed.Message, LandingSyncWords.Followed(stays)) };
        }

        return landed;
    }

    /// <summary>
    /// The branch work unit of the session tree at <paramref name="full"/>: its standing entry's, found by the folder the
    /// layout names for its branch; null for an entry that carries no unit, which no landing can name.
    /// </summary>
    private string? UnitOf(string full, string repository) => context.Grown.All()
        .LastOrDefault(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(context.TreeAt(entry.Workspace, entry.Repository, Path.GetFileName(entry.Branch))),
                Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase))?.Unit;

    /// <summary>A failed fetch's words, said with whatever the landing came to; none where it fetched.</summary>
    private static TreeLanding Fetched(TreeLanding landed, string? fetched) =>
        fetched is null ? landed : landed with { Message = BeforeTreeStays(landed.Message, fetched) };

    /// <summary>
    /// A note the landing appends when the record could not be written: the branch stands, and only its
    /// clean-up and its hand-off are lost — the safe side, said rather than swallowed.
    /// </summary>
    private static TreeLanding Remember(TreeLanding landed, Action write)
    {
        try
        {
            write();
            return landed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return landed with
            {
                Message = landed.Message + $" Daoris could not record the branch as a landing's ({error.Message}), so its clean-up will not judge it.",
            };
        }
    }
}
