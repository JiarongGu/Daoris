using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.LandingSayings;

namespace Daoris.Driver;

/// <summary>
/// A session tree's work landed as its run's bound <c>land</c> says (D51 rule 6, WSR1, D87, D100; PART10l-ii, out of
/// <c>SessionTrees.cs</c>): the plan said before the press, the merge into the line, the branch form and a chain's advance. Over the
/// trees' context, the layout's answer of a tree's branch and the head's own judgement of what the line holds by content
/// (<see cref="TreeWorkReads"/>). The front keeps its public calls.
/// </summary>
internal sealed class TreeLands
{
    private readonly TreeContext context;
    private readonly TreeLandPlans plans;
    private readonly TreeMerges merges;
    private readonly LandingPress press;

    internal TreeLands(TreeContext context, TreeFollows follows, TreeWorkReads workReads, TreeLandSyncs syncs)
    {
        this.context = context;
        plans = new(context, syncs);
        merges = new(context);
        press = new(context, workReads, syncs, plans, merges, new(context, follows, syncs));
    }

    internal Task<LandingPlan> PlanAsync(string path, LandingSubject subject, CancellationToken ct = default, IWorkplaceProcess? process = null) =>
        plans.PlanAsync(path, subject, ct, process);

    internal Task<LandingPlan> PlanRuleAsync(string path, LandingSubject subject, CancellationToken ct, IWorkplaceProcess? process) =>
        plans.PlanRuleAsync(path, subject, ct, process);

    internal Task<TreeMerge> MergeAsync(
        string path, CancellationToken ct = default, LandReplay? replay = null, int? uncommitted = null, CommonDirAndBranch? here = null,
        string? pinned = null, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null, TreeDelivering? delivering = null) =>
        merges.MergeAsync(path, ct, replay, uncommitted, here, pinned, beside, delivering);

    internal Task<LandingSubject> EntryForWorkAsync(LandingSubject entry, string? leadTree, string anyTree, CancellationToken ct) =>
        plans.EntryForWorkAsync(entry, leadTree, anyTree, ct);

    /// <summary>
    /// The press on a reviewed session (WSR1, D87): its work lands as its repository's rule says —
    /// merged into the line (<see cref="MergeAsync"/>), or put on a new branch for the person to push, or
    /// for the plugin the rule names to push and open the pull request from (WSR4, D100). A chain's later
    /// done moves the branch its first landing made on instead, and grows that pull request (LAND2c, D149).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The plugin is spoken to after the branch exists, and never instead of it.</b> A plugin the
    /// press cannot use — gone, off, landing nothing — refuses the press before anything is made; one that
    /// fails once the branch is made leaves the branch standing, and the sentence says its step failed and
    /// how the person does it by hand. Nothing here pushes: the plugin's process does.
    /// <para>🔴 <b>Work the line already holds by content is never landed again</b> (SQUASHTIDY1f): where a squash merge or a
    /// cherry-pick put the tree's commits on the line or a branch of the person's, as the session's head reads it
    /// (<see cref="SessionTrees.DiscardOfferAsync"/>, SQUASHTIDY1b), every door is refused here, before the review's gate and before anything
    /// is made, in the head's clause and with <see cref="AutoLandingCode.Carried"/>: a merge would copy the work onto the line
    /// again, or conflict with it, and a branch would carry it to a second pull request. Its discard is the way.</para>
    /// </remarks>
    /// <param name="inUse">
    /// The trees sessions still running or waiting name, asked when the rule's tidy reaches the other session branches the
    /// landed work holds (LAND3); null keeps every one of those that still has a tree, since nobody asked.
    /// </param>
    /// <param name="acceptedBy">
    /// Who accepts it (LAND2b, D145), kept on the landing record: the person's press, the default, or the rule's switch at the
    /// quest's done. 🔴 One thing differs for <see cref="AcceptedBy.Auto"/>: a plugin that cannot land work here does not stop
    /// the branch, since nobody is there to fix it, so the branch is made and recorded and the push is not tried.
    /// </param>
    /// <param name="gate">
    /// The landing gate for this tree (the review's <c>SessionTreeGates.GateAsync</c>), read once by the door that lands it: the second opinion
    /// (XAGENT1f, D155 point 9) and then D154's look (REVIEWENV1c, D154 point 7). The first part that does not let go refuses before
    /// anything is made, merge or branch, an advance among them, with its sentence and its code
    /// (<see cref="AutoLandingCode.Opinion"/>, then <see cref="AutoLandingCode.Unreviewed"/>); what let each go is kept on the
    /// landing record. Null is a door that asked none.
    /// </param>
    /// <param name="joins">
    /// This part joins its entry's one branch (ACCEPTALL1, D167's 2026-10-15 amendment point 2): where that branch stands and this
    /// tree's work does not grow from its tip (a part that ran alongside, in a tree of its own), what neither the branch nor the line
    /// holds is replayed onto its tip, which then moves by D145 §3's fast-forward, and the tree follows. False lands as before.
    /// </param>
    /// <param name="fetched">
    /// The entry's Accept fetched the line once before its parts (ACCEPTALL1d, D87's 2026-10-15 amendment point 6), and said a failed
    /// fetch once, so this part is judged against that fetch and fetches nothing. False: the person's press fetches, as before.
    /// </param>
    /// <param name="judged">
    /// The door's own judgement again, once it holds the tree (FABRIC-CLOSE1h, D173's note), told whether it waited for another landing
    /// of the tree: a refusal it answers is the landing's, before anything is made; null lets it go on. Past this point a hold, a reopen or
    /// a gate's word applies to what follows, never to this landing, which is the landing's commitment. Null asks nothing, as before. The
    /// automatic landing's is <see cref="AutoLander"/>'s; the person's press and an entry's part ask theirs of <see cref="PressedResult"/>
    /// (FABRIC-CLOSE1h3), on the result the press was made on.
    /// </param>
    /// <param name="beforePush">
    /// The door's judgement once more, just before the rule's plugin is told (FABRIC-CLOSE1h2, D173's note), told the branch's record as
    /// it reads under the branch's hold (null where none was recorded), so the work of every session the push carries
    /// (FABRIC-CLOSE1h11), and whether the push waited behind another push of its branch: a sentence it answers holds the push back
    /// (<see cref="TreeLanding.Withheld"/>), the branch made and recorded and no plugin spoken to; null lets the push begin, past which
    /// nothing stops it. Null asks nothing.
    /// </param>
    /// <param name="uncommitted">
    /// The tree's uncommitted paths as the door read them where it judged the landing (LANDGIT1c): the look's automatic landing reads
    /// them there, so the landing asks `status` no more. Read again where the landing waited for another of its tree; null reads them.
    /// </param>
    /// <param name="together">The operation bringing sessions together whose combined Accept this is: the one door a tree it binds lands by.</param>
    /// <param name="beside">The ledger's sessions and their trees (GITLOCK2), read inside the checkout's hold by a merge rule's move; see <see cref="MergeAsync"/>.</param>
    /// <param name="delivery">
    /// The delivery this landing performs (LANDNODE1b2; the land node design §3 step 4), its intent read from its store: the method, the branch,
    /// the line and the pinned tip are its, never the live rule's, and each git effect is written ahead in its phases and its outcome after
    /// (<see cref="TreeDelivering"/>). Who accepted it is its authorization's. Null lands by the rule, as before.
    /// </param>
    internal async Task<TreeLanding> LandAsync(
        string path, LandingSubject subject, CancellationToken ct = default, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null,
        string acceptedBy = AcceptedBy.Person, IWorkplaceGate? gate = null, bool joins = false, bool fetched = false,
        Func<bool, CancellationToken, Task<TreeLanding?>>? judged = null, Func<LandedBranch?, bool, CancellationToken, Task<string?>>? beforePush = null,
        int? uncommitted = null, string? together = null, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null,
        TreeDelivery? delivery = null)
    {
        var repository = context.OwnerOf(Path.GetFullPath(path)).Repository;

        // LANDRACE1 (D145 point 2, D173's FABRIC-END7 note and its review): one landing of one session's tree at a time, whichever door
        // lands it. The second to come waits for the first, and where the first landed the very commit this one would land, says what
        // came of it, its plugin's answer included, and lands nothing more. A later commit lands as any landing does; a landing that
        // waited for nothing is judged as before, and reads nothing more of git.
        // LANDHOLD3: named by its session and the branch its tree stands on, for a door that waits behind it.
        using var hold = await LandingHolds.TakeAsync(context.Home, path, ct, by: new LandingHolder(subject.Session, TreeTips.Branch(path)))
            .ConfigureAwait(false);
        if (hold is null) return new(false, StillLanding) { Refusal = AutoLandingCode.Refused };

        // LANDNODE1b3: a delivery is read under the tree's hold and held in this process while it lands, so a press and a pass of one delivery
        // land it once and a restart's settlement never writes beside it; one delivered while this waited is said so, and nothing more lands.
        // LANDNODE1c3: and one Current's door writes below, once the landing is judged, is entered the same way and closed here.
        using var entry = new DeliveryEntry();
        if (delivery is not null)
        {
            if (press.Enter(entry, delivery, repository) is { } notEntered) return notEntered;
            acceptedBy = entry.Delivering!.Accepted;
        }

        var delivering = entry.Delivering;
        TreeLanding Closed(TreeLanding landing) => entry.Delivering?.Close(landing) ?? landing;
        var pinned = delivering?.Intent.Tip ?? gate?.Tip;
        if (hold.Before is { } before && await LandingPress.JudgedAsync(path, pinned, ct).ConfigureAwait(false) is { } asked
            && string.Equals(asked, before.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return Closed(LandingPress.LandedMeanwhile(before, subject.Session));
        }

        // LINESTOGETHER1d: a tree bound into an operation bringing sessions together lands only by its combined Accept, read under the
        // record lock while this holds the tree, so a press binding it waits for this landing to end.
        if (TogetherGuard.Refusal(context.Home, path, together) is { } bound)
        {
            return Closed(new(false, $"Nothing was landed: {bound}.") { Refusal = AutoLandingCode.Refused });
        }

        // A member a combined Accept brought in lands that work no second time; a later commit is its own.
        if (together is null && await TogetherGuard.BroughtAsync(context.Home, path, pinned, ct).ConfigureAwait(false) is { } brought)
        {
            return Closed(new(false, $"Nothing was landed: {brought}.") { Refusal = AutoLandingCode.Already });
        }

        // FABRIC-CLOSE1h: what the door judged before it took the hold may have moved while it waited, so it judges again here, under the
        // hold and before anything is made. A refusal now stops the landing whole: no branch, no advance, no push.
        if (judged is not null && await judged(hold.Waited, ct).ConfigureAwait(false) is { } refusedNow) return Closed(refusedNow);

        TreeLanding landed;
        // FABRIC-END6c: a landing holds its repository for the due moments, which skip it until it is let go, then ask.
        using (RepositoryHolds.Hold(context.Home, repository))
        {
            landed = Closed(await press.LandingAsync(path, subject, ct, inUse, acceptedBy, gate, joins, fetched, hold, beforePush,
                hold.Waited ? null : uncommitted, beside, entry).ConfigureAwait(false));
        }

        // FABRIC-END3: Accept is Daoris's own ref move; its repository is judged at once by the ref watch, with its echo (§B4).
        if (landed.Landed) RefMoves.Mine(context.Home, repository);
        // FABRIC-END6: Daoris pushed, a moment to ask about its pull request (§A4.3), told after the rule's tidy, whose own ask it counts.
        if (landed.Plugin is { Pushed: true, Failed: false }) PullRequestMoments.Pushed(context.Home, repository);
        return landed;
    }
}
