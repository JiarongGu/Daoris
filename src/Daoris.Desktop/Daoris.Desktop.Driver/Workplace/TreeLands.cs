using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.LandingSayings;

namespace Daoris.Driver;

/// <summary>
/// A session tree's work landed as its run's bound <c>land</c> says: the plan said before the press, the merge into the line, the branch form and a chain's advance. Over the
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

    internal Task<LandingSubject> EntryForWorkAsync(
        LandingSubject entry, string? leadTree, string anyTree, CancellationToken ct, IWorkplaceProcess? process = null) =>
        plans.EntryForWorkAsync(entry, leadTree, anyTree, ct, process);

    /// <summary>
    /// The press on a reviewed session: its work lands as its repository's rule says —
    /// merged into the line (<see cref="MergeAsync"/>), or put on a new branch for the person to push, or
    /// for the plugin the rule names to push and open the pull request from. A chain's later
    /// done moves the branch its first landing made on instead, and grows that pull request.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The plugin is spoken to after the branch exists, and never instead of it.</b> A plugin the
    /// press cannot use — gone, off, landing nothing — refuses the press before anything is made; one that
    /// fails once the branch is made leaves the branch standing, and the sentence says its step failed and
    /// how the person does it by hand. Nothing here pushes: the plugin's process does.
    /// <para>🔴 <b>Work the line already holds by content is never landed again</b>: where a squash merge or a
    /// cherry-pick put the tree's commits on the line or a branch of the person's, as the session's head reads it
    /// (<see cref="SessionTrees.DiscardOfferAsync"/>), every door is refused here, before the review's gate and before anything
    /// is made, in the head's clause and with <see cref="LandingCode.Carried"/>: a merge would copy the work onto the line
    /// again, or conflict with it, and a branch would carry it to a second pull request. Its discard is the way.</para>
    /// </remarks>
    /// <param name="inUse">
    /// The trees sessions still running or waiting name, asked when the rule's tidy reaches the other session branches the
    /// landed work holds; null keeps every one of those that still has a tree, since nobody asked.
    /// </param>
    /// <param name="acceptedBy">
    /// Who accepts it, kept on the landing record: the person's press, the default, or the rule's switch at the
    /// quest's done. 🔴 One thing differs for <see cref="AcceptedBy.Auto"/>: a plugin that cannot land work here does not stop
    /// the branch, since nobody is there to fix it, so the branch is made and recorded and the push is not tried.
    /// </param>
    /// <param name="gate">
    /// The landing gate for this tree (the review's <c>SessionTreeGates.GateAsync</c>), read once by the door that lands it: the second opinion
    /// and then the look. The first part that does not let go refuses before
    /// anything is made, merge or branch, an advance among them, with its sentence and its code
    /// (<see cref="LandingCode.Opinion"/>, then <see cref="LandingCode.Unreviewed"/>); what let each go is kept on the
    /// landing record. Null is a door that asked none.
    /// </param>
    /// <param name="joins">
    /// This part joins its entry's one branch: where that branch stands and this
    /// tree's work does not grow from its tip (a part that ran alongside, in a tree of its own), what neither the branch nor the line
    /// holds is replayed onto its tip, which then moves by fast-forward, and the tree follows.
    /// </param>
    /// <param name="fetched">
    /// The entry's Accept fetched the line once before its parts, and said a failed
    /// fetch once, so this part is judged against that fetch and fetches nothing. False: the person's press fetches.
    /// </param>
    /// <param name="judged">
    /// The door's own judgement again, once it holds the tree, told whether it waited for another landing
    /// of the tree: a refusal it answers is the landing's, before anything is made; null lets it go on. Past this point a hold, a reopen or
    /// a gate's word applies to what follows, never to this landing, which is the landing's commitment. Null asks nothing. The
    /// person's press and an entry's part ask theirs of <see cref="PressedResult"/>,
    /// on the result the press was made on.
    /// </param>
    /// <param name="uncommitted">
    /// The tree's uncommitted paths as the door read them where it judged the landing: the look's automatic landing reads
    /// them there, so the landing asks `status` no more. Read again where the landing waited for another of its tree; null reads them.
    /// </param>
    /// <param name="together">The operation bringing sessions together whose combined Accept this is: the one door a tree it binds lands by.</param>
    /// <param name="beside">The ledger's sessions and their trees, read inside the checkout's hold by a merge rule's move; see <see cref="MergeAsync"/>.</param>
    /// <param name="delivery">
    /// The delivery this landing performs, its intent read from its store: the method, the branch,
    /// the line and the pinned tip are its, never the live rule's, and each git effect is written ahead in its phases and its outcome after
    /// (<see cref="TreeDelivering"/>). Who accepted it is its authorization's. Null lands by the rule, as before.
    /// </param>
    internal async Task<TreeLanding> LandAsync(
        string path, LandingSubject subject, CancellationToken ct = default, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null,
        string acceptedBy = AcceptedBy.Person, IWorkplaceGate? gate = null, bool joins = false, bool fetched = false,
        Func<bool, CancellationToken, Task<TreeLanding?>>? judged = null,
        int? uncommitted = null, string? together = null, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null,
        TreeDelivery? delivery = null)
    {
        var repository = context.OwnerOf(Path.GetFullPath(path)).Repository;

        // One landing of one session's tree at a time, whichever door
        // lands it. The second to come waits for the first, and where the first landed the very commit this one would land, says what
        // came of it, its plugin's answer included, and lands nothing more. A later commit lands as any landing does; a landing that
        // waited for nothing is judged as before, and reads nothing more of git.
        // Named by its session and the branch its tree stands on, for a door that waits behind it.
        using var hold = await LandingHolds.TakeAsync(context.Home, path, ct, by: new LandingHolder(subject.Session, TreeTips.Branch(path)))
            .ConfigureAwait(false);
        if (hold is null) return new(false, StillLanding) { Refusal = LandingCode.Refused };

        // A delivery is read under the tree's hold and held in this process while it lands, so a press and a pass of one delivery
        // land it once and a restart's settlement never writes beside it; one delivered while this waited is said so, and nothing more lands.
        // And one Current's door writes below, once the landing is judged, is entered the same way and closed here.
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

        // A tree bound into an operation bringing sessions together lands only by its combined Accept, read under the
        // record lock while this holds the tree, so a press binding it waits for this landing to end.
        if (TogetherGuard.Refusal(context.Home, path, together) is { } bound)
        {
            return Closed(new(false, $"Nothing was landed: {bound}.") { Refusal = LandingCode.Refused });
        }

        // A member a combined Accept brought in lands that work no second time; a later commit is its own.
        if (together is null && await TogetherGuard.BroughtAsync(context.Home, path, pinned, ct).ConfigureAwait(false) is { } brought)
        {
            return Closed(new(false, $"Nothing was landed: {brought}.") { Refusal = LandingCode.Already });
        }

        // What the door judged before it took the hold may have moved while it waited, so it judges again here, under the
        // hold and before anything is made. A refusal now stops the landing whole: no branch, no advance, no push.
        if (judged is not null && await judged(hold.Waited, ct).ConfigureAwait(false) is { } refusedNow) return Closed(refusedNow);

        TreeLanding landed;
        // A landing holds its repository for the due moments, which skip it until it is let go, then ask.
        using (RepositoryHolds.Hold(context.Home, repository))
        {
            landed = Closed(await press.LandingAsync(path, subject, ct, inUse, acceptedBy, gate, joins, fetched, hold,
                hold.Waited ? null : uncommitted, beside, entry).ConfigureAwait(false));
        }

        // Accept is Daoris's own ref move; its repository is judged at once by the ref watch, with its echo (§B4).
        if (landed.Landed) RefMoves.Mine(context.Home, repository);
        return landed;
    }
}
