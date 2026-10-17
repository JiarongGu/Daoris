using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// What the branch form made, and what a plugin is told of it (D100): where, from which line, which commits — and the session
/// branch it was made from. At file level since PART10l, as the squash's advance (<see cref="TreeSquashes"/>) makes one too.
/// </summary>
internal sealed record Branched(
    TreeLanding Landing, string Root = "", string? Base = null, IReadOnlyList<LandingCommit>? Carried = null, string? Source = null)
{
    public IReadOnlyList<LandingCommit> Commits => Carried ?? [];

    /// <summary>The session's tip squashed into the branch's new commit (PROCESS1b-ii), which the record keeps; null where nothing was squashed.</summary>
    public string? Squashed { get; init; }

    /// <summary>What the landing replayed onto the line, leaving out what it holds (LANDSYNC1); null where it landed the commits as made.</summary>
    internal LandReplay? Replay { get; init; }

    /// <summary>The tree's own branch was replayed in place (TREEFOLLOW1, LANDSYNC1): the tree already stands on what landed.</summary>
    public bool Followed { get; init; }

    public static implicit operator Branched(TreeLanding refused) => new(refused);
}

/// <summary>
/// A session tree's work landed as its repository's rule says (D51 rule 6, WSR1, D87, D100; PART10l-ii, out of
/// <c>SessionTrees.cs</c>): the plan said before the press, the merge into the line, the branch form and a chain's advance, and
/// the rule's tidy after. Over the trees' context, the layout's answer of a tree's branch, the head's own judgement of what the
/// line holds by content (<see cref="TreeWorkReads"/>), the removal and the tidy. The front keeps its public calls.
/// </summary>
internal sealed class TreeLands(
    TreeContext context, TreeFollows follows, TreeWorkReads workReads, TreeRemovals removals, TreeTidies tidies, TreeLandSyncs syncs,
    EndKeeping ends)
{
    /// <summary>
    /// What a press on this tree would do under its repository's landing rule (WSR1, D87): merge into
    /// the line, or make the branch the pattern names for this session — said before the press.
    /// </summary>
    /// <remarks>
    /// LANDSYNC1 (D87's 2026-10-15 amendment point 4): and what the press would leave out because the line holds it, what it would
    /// land, and whether that conflicts now, judged against the line as last fetched. It reaches no network and writes no ref and no
    /// tree: the conflict's trial is <c>git merge-tree</c>'s, which writes objects only.
    /// </remarks>
    /// <param name="process">
    /// The run's process the door read with its gate (WORKFLOW1f): under a named workflow, its version's landing step over the
    /// declared rule. Null reads the rule live, as before.
    /// </param>
    internal async Task<LandingPlan> PlanAsync(string path, LandingSubject subject, CancellationToken ct = default, WorkflowProcess? process = null)
    {
        var plan = await PlanRuleAsync(path, subject, ct, process).ConfigureAwait(false);
        if (process?.LandingCannot is not null || plan.Target.Length == 0) return plan;
        var full = Path.GetFullPath(path);
        var (workspace, repository) = context.OwnerOf(full);
        // ACCEPTALL1g: whether the branch already stands, so the review says the press moves it on rather than promising a new one.
        if (plan is { Form: LandingForm.Branch, Own: false } && Directory.Exists(full)
            && await CommitOfAsync(full, $"refs/heads/{plan.Target}", ct).ConfigureAwait(false) is { } standsAt)
        {
            // STANDSREFUSE1: and what the press would refuse it for, said before it, where nothing else is said first.
            plan = plan with { Stands = true, Problem = plan.Problem ?? await StandingProblemAsync(full, repository, plan.Target, standsAt, subject, ct).ConfigureAwait(false) };
        }

        var (sync, _) = await syncs.JudgeAsync(full, repository, workspace, plan.Form == LandingForm.Branch, fetch: false, ct).ConfigureAwait(false);
        if (sync is null || sync.Held.HoldsNone) return plan;
        if (plan.Own && await OnRemoteAsync(sync.Root, sync.Branch, ct).ConfigureAwait(false))
        {
            return plan with { LeftOut = sync.Held.LeftOut, Lands = sync.Held.Kept, Conflict = TreeLandSyncs.OwnOnRemote(sync).Conflict };
        }

        var conflicts = await LineHolds.TrialAsync(sync.Root, sync.Onto, sync.Held.Kept, ct).ConfigureAwait(false);
        return plan with
        {
            LeftOut = sync.Held.LeftOut,
            Lands = sync.Held.Kept,
            Conflict = conflicts is null ? null : TreeLandSyncs.Conflicted(sync, conflicts).Conflict,
        };
    }

    /// <summary>
    /// The refusal a press would meet at a standing branch, from what is cheap to read (STANDSREFUSE1, D167's ACCEPTALL1g note, D145
    /// §3), in the press's own words: a name entered that is somebody's branch, or <see cref="LandingAdvance.Standing"/>'s record, tip,
    /// checkout and kept pull request word. Null where those allow the move; git's proof that its work is on the line, and how this
    /// work grows from it, stay the press's to judge.
    /// </summary>
    /// <param name="standsAt">The commit the branch stands at, read a moment ago.</param>
    private async Task<string?> StandingProblemAsync(
        string tree, string repository, string name, string standsAt, LandingSubject subject, CancellationToken ct)
    {
        var recorded = context.Recorded.Of(repository, name);
        if (recorded is null && subject.EnteredName is not null) return EnteredStands(name, repository);
        // Where the press would read it: only once the record and the tip allow a move. A list git could not give says nothing here,
        // and the press asks again.
        string? checkedOut = null;
        if (recorded is not null && string.Equals(standsAt, recorded.Tip, StringComparison.OrdinalIgnoreCase)
            && await EntriesAsync(tree, ct).ConfigureAwait(false) is { } listed)
        {
            checkedOut = ByBranch(listed).GetValueOrDefault(name);
        }

        return LandingAdvance.Standing(name, repository, recorded, standsAt, checkedOut)?.Message;
    }

    /// <summary>What a press would do under the rule, before LANDSYNC1's judgement of what the line holds; an entry's Accept names its branch by it.</summary>
    internal async Task<LandingPlan> PlanRuleAsync(string path, LandingSubject subject, CancellationToken ct, WorkflowProcess? process)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = context.OwnerOf(full);
        var landing = process is { Named: true } named ? named.Landing : LandingRules.Choose(context.Config(), repository, workspace);
        // A named workflow's landing that cannot start says so before the press, as the press would be refused (design §3.7).
        if (process?.LandingCannot is not null)
        {
            return new(landing.Rule.Form, "", landing.Source, landing.Rule.Plugin, WorkflowGate.LandingSays(process), landing.Rule.AutoAccept);
        }

        if (landing.Rule.Form == LandingForm.Branch)
        {
            // Who pushes it, said before the press, and what would refuse the press where something would (D100).
            var plugin = landing.Rule.Plugin;
            // TREEFOLLOW1: a tree its agent moved onto a branch of its own lands as that branch stands, the one the pull request is from.
            if (Directory.Exists(full) && context.Holds(full) && await TreeFollows.OwnBranchOfAsync(full, ct).ConfigureAwait(false) is { } own)
            {
                return new(LandingForm.Branch, own, landing.Source, plugin, subject.EnteredName is not null ? EnteredOwn(own) : plugin is null ? null : context.Plugins.Problem(plugin),
                    landing.Rule.AutoAccept)
                { Own = true };
            }

            // A conversation's for its work, never its opening (ACCEPTFOOT2), as the press names it.
            var name = BranchNamed(await ForWorkAsync(full, workspace, repository, landing.Rule, subject, ct).ConfigureAwait(false), landing.Rule, repository);
            // A name the press would refuse is said before it (LANDNAME1a), where the plan keeps the sentence a press is refused with.
            var problem = subject.EnteredName is not null && !BranchName.IsValid(name) ? EnteredInvalid(name)
                : plugin is null ? null : context.Plugins.Problem(plugin);
            return new(LandingForm.Branch, name, landing.Source, plugin, problem, landing.Rule.AutoAccept);
        }

        var (code, commonDir, _) = Directory.Exists(full)
            ? await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false)
            : (1, "", "");
        var line = code == 0
            ? (await context.LineAsync(Path.GetDirectoryName(commonDir.Trim())!, repository, workspace, ct).ConfigureAwait(false)).Branch
            : null;
        return new(LandingForm.Merge, line ?? "", landing.Source, Problem: subject.EnteredName is not null ? EnteredMerge : null);
    }

    /// <summary>
    /// Merge a session tree's commits into the canonical line, in the repository's own root (D51
    /// rule 6, design §5).
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing merges itself.</b> This is only ever the person pressing it — D37 would permit
    /// automating a local, reversible act, and it stays a press because it is <i>where their
    /// verification lands</i>.</para>
    ///
    /// <para>🔴 <b>This is the one place Daoris writes into a checkout it did not create</b>, so every
    /// guard `reaching-in` names is here and each refuses rather than repairing. The tree must be
    /// under the trees home (a checkout is never ours to merge FROM); the root must be <b>clean</b>,
    /// because somebody's work in flight is exactly what that document was written from; the root must
    /// already be <b>on</b> the canonical line, because switching a branch in a checkout we do not own
    /// is the same trespass in a smaller form; and every check is made <b>immediately before</b> the
    /// merge in this one call, because "it was clean when I looked" expires the moment you look away.
    /// Assume another session is working in that repository right now — it very often is.</para>
    ///
    /// <para><b>A conflict aborts and reports</b>, leaving the root exactly as it was. Nothing here
    /// resolves anything, forces anything or removes anything: the tree survives a merge whether it
    /// succeeded or not, and discarding it stays a separate act the person asks for.</para>
    /// </remarks>
    /// <param name="replay">
    /// What the landing replayed onto the line here, leaving out what the line holds (LANDSYNC1, D87's 2026-10-15 amendment point 2):
    /// its tip is merged in place of the session's branch, as D86 merges one. Null merges the branch as made.
    /// </param>
    internal async Task<TreeMerge> MergeAsync(string path, CancellationToken ct = default, LandReplay? replay = null)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(context.TreesRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, $"{path} is not a session tree — this merges only trees Daoris opened, "
                + $"under {context.TreesRoot}.");
        }

        if (!Directory.Exists(full))
        {
            return new(false, $"there is no tree at {path}.");
        }

        // Its repository and its branch, one start (PROC1d-iii).
        var here = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (here.Code != 0)
        {
            return new(false, $"{path} is not a working tree git recognises: {FirstLine(here.Err)}");
        }

        var root = Path.GetDirectoryName(here.CommonDir.Trim())!;
        var branch = here.Branch.Trim();
        if (branch is "" or "HEAD")
        {
            return new(false, $"the tree at {path} is not on a branch, so there is nothing to merge.");
        }

        // Uncommitted work in the SESSION tree would silently not travel. Saying "merged" while
        // leaving it behind is the worst answer available: the person believes the work moved.
        var (_, treeDirty, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain"], ct)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(treeDirty))
        {
            var count = treeDirty.Trim().Split('\n').Length;
            return new(false,
                $"the session's tree has uncommitted work — {count} path(s) — which a merge would "
                + "leave behind. Commit it in the tree first, or decide it is not wanted.");
        }

        var (workspace, repository) = context.OwnerOf(full);
        var line = await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false);
        if (line.Branch is not { } canonical)
        {
            return new(false, "this repository has no canonical line git can name, and none is set, so "
                + "there is nowhere to merge to. Set one with `daoris driver line`.");
        }

        // 🔴 Compared where git has the line, and a comparison git could not make is not "nothing to
        // merge": a line only origin has made `log` fail, and its empty output said the work was there.
        var against = await ComparableAsync(root, canonical, ct).ConfigureAwait(false) ?? canonical;
        var (aheadCode, ahead, aheadErr) = await WorkingTree.GitAsync(
            root, ["log", "--oneline", $"{against}..{branch}"], ct).ConfigureAwait(false);
        if (aheadCode != 0)
        {
            return new(false, $"git cannot compare `{branch}` with `{canonical}` here: {FirstLine(aheadErr)} "
                + "Fetch it, or set another line with `daoris driver line`.");
        }

        if (string.IsNullOrWhiteSpace(ahead))
        {
            // An empty merge commit would say work moved when none did.
            return new(false, $"`{canonical}` already holds everything on `{branch}` — nothing to merge.");
        }

        // 🔴 The root's state, read as late as possible. A tree-state observation is a fact about a
        // moment, and the whole premise here is that somebody else may be working in this repository.
        var (clean, detail) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
        if (!clean)
        {
            return new(false,
                $"the repository's own checkout is not clean ({detail}), and merging into somebody's "
                + "work in flight is exactly what Daoris does not do. Deal with it there, then merge.");
        }

        var (_, onOut, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var on = onOut.Trim();
        if (!string.Equals(on, canonical, StringComparison.Ordinal))
        {
            return new(false,
                $"the repository's checkout is on `{on}`, not {CanonicalLine.Describe(line)}. Daoris does "
                + $"not switch a branch in a checkout it did not create — put it on `{canonical}`, then merge.");
        }

        // `--no-ff` so the merge is one commit a person can read and revert as a unit, and `--no-edit`
        // so nothing opens an editor on a machine nobody is sitting at. A replay's tip is merged under the branch's name.
        var (mergeCode, _, mergeErr) = await WorkingTree.GitAsync(
            root, replay is null ? ["merge", "--no-ff", "--no-edit", branch] : ["merge", "--no-ff", "-m", $"Merge branch '{branch}'", replay.Now], ct)
            .ConfigureAwait(false);
        if (mergeCode != 0)
        {
            // Leave the root as it was found. Nothing here resolves a conflict: the person does that
            // where the context is, and a half-merged checkout is worse than an honest refusal.
            await WorkingTree.GitAsync(root, ["merge", "--abort"], ct).ConfigureAwait(false);
            return new(false,
                $"git would not merge `{branch}` into `{canonical}`: {FirstLine(mergeErr)} "
                + "The merge was aborted and the checkout is as it was.");
        }

        var landed = ahead.Trim().Split('\n').Length;
        // The line moved: the registry follows it at the next look (WSSETUP5, D124 §3.1), whichever door pressed this.
        RegistryFollowing.Moved(context.Home, repository, DateTimeOffset.UtcNow);
        return replay is null
            ? new(true, $"merged `{branch}` into `{canonical}` — {landed} commit(s).{TreeStays}")
            : new(true, $"merged `{branch}` into `{canonical}` — {LandingSyncWords.Replayed(replay.Landed.Count, canonical, replay.LeftOut)}."
                + $"{LandingSyncWords.Emptied(replay.LeftOut)}{TreeStays}");
    }

    /// <summary>What a landing says of the tree it leaves — dropped when a tidy removed it; the squash's advance says it too.</summary>
    internal const string TreeStays =" The tree is still there; discard it when you are done with it.";

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
    /// The landing gate for this tree (<see cref="SessionTrees.GateAsync"/>), read once by the door that lands it: the second opinion
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
    internal async Task<TreeLanding> LandAsync(
        string path, LandingSubject subject, CancellationToken ct = default, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null,
        string acceptedBy = AcceptedBy.Person, LandingGate? gate = null, bool joins = false, bool fetched = false)
    {
        var landed = await LandingAsync(path, subject, ct, inUse, acceptedBy, gate, joins, fetched).ConfigureAwait(false);
        // FABRIC-END3: Accept is Daoris's own ref move; its repository is judged at once by the ref watch, with its echo (§B4).
        if (landed.Landed) RefMoves.Mine(context.Home, context.OwnerOf(Path.GetFullPath(path)).Repository);
        // FABRIC-END6: Daoris pushed, a moment to ask about its pull request (§A4.3), told after the rule's tidy, whose own ask it counts.
        if (landed.Plugin is { Pushed: true, Failed: false }) PullRequestMoments.Pushed(context.Home, context.OwnerOf(Path.GetFullPath(path)).Repository);
        return landed;
    }

    /// <summary>The landing itself, as <see cref="LandAsync"/> says it.</summary>
    private async Task<TreeLanding> LandingAsync(
        string path, LandingSubject subject, CancellationToken ct, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse,
        string acceptedBy, LandingGate? gate, bool joins, bool fetched)
    {
        // One look (UIFREEZE1, PROC1d-i): the landing and its tidy ask git's guess at the line once.
        using var look = context.OneLook();
        var full = Path.GetFullPath(path);
        // SQUASHTIDY1f: the head's own judgement, asked first, since a second opinion or a review of work already on the line
        // lands nothing either.
        if (await workReads.DiscardOfferAsync(full, ct).ConfigureAwait(false) is { } carried)
        {
            return new(false, carried.NotLanded) { Refusal = AutoLandingCode.Carried };
        }

        // One gate, in a fixed order (the second-agent design §7): the run's own part (WORKFLOW1f), the second opinion, then the
        // person's review where it runs, then the landing step's own. A merge rule waits as a branch rule does, and an advance waits
        // the same way.
        if (gate?.Refusal is { } refused) return refused;
        var review = gate?.Review;

        var (workspace, repository) = context.OwnerOf(full);
        // Under a named workflow the run's bound version decides how it lands, read with the gate (WORKFLOW1f): a version is never
        // edited, so nothing changes under it. Under Current the rule is read again here, as before.
        var process = gate?.Process;
        var named = process is { Named: true } ? process : null;
        var landing = named?.Landing ?? LandingRules.Choose(context.Config(), repository, workspace);
        // 🔴 Read again here, not only where the look chose it (LAND2b): a rule changed between the two must never let a landing
        // at done merge into the person's checkout with no press (D145 point 1, D51 rule 6), nor land under a switch now off.
        if (acceptedBy == AcceptedBy.Auto && landing.Rule is not { Form: LandingForm.Branch, AutoAccept: true })
        {
            return new(false, "its repository's rule no longer accepts automatically, so nothing was landed: your Accept lands it.")
            {
                Refusal = AutoLandingCode.Off,
            };
        }

        TreeLanding landed;
        // The session tip a squash made the branch's commit from (PROCESS1b-ii): the tidy's proof of what the landing holds.
        string? squashedTip = null;
        var branchForm = landing.Rule.Form == LandingForm.Branch;
        // A name entered where nothing is branched is refused, not ignored (LANDNAME1a): the person would think it was used.
        if (!branchForm && subject.EnteredName is not null) return new(false, EnteredMerge);
        var plugin = branchForm ? landing.Rule.Plugin : null;
        var unready = plugin is null ? null : context.Plugins.Problem(plugin);
        if (unready is not null && acceptedBy != AcceptedBy.Auto)
        {
            return new(false, $"{unready} Nothing was landed: the rule hands the branch to that plugin, so fix the "
                + "plugin or the rule (`daoris driver landing`), then accept again.");
        }

        // LANDSYNC1 (D87's 2026-10-15 amendment): only what the line lacks lands. The person's press fetches the line once first
        // (point 6), a failed fetch said and judged against the last one; the look's automatic landing has no press and fetches nothing.
        var (sync, notFetched) = await syncs.JudgeAsync(full, repository, workspace, branchForm, fetch: acceptedBy != AcceptedBy.Auto && !fetched, ct)
            .ConfigureAwait(false);
        var replaying = sync is { Held.HoldsNone: false } ? sync : null;
        if (replaying is { Held.Kept.Count: 0 })
        {
            // The fetch brought what holds all of it: SQUASHTIDY1f's sentence where its proof now finds it, else nothing to land.
            return Fetched(await workReads.DiscardOfferAsync(full, ct).ConfigureAwait(false) is { } heldNow
                ? new(false, heldNow.NotLanded) { Refusal = AutoLandingCode.Carried, LeftOut = replaying.Held.LeftOut }
                : new(false, $"`{replaying.Branch}` holds nothing `{replaying.OntoName}` lacks: the line already holds "
                    + $"{string.Join(", ", replaying.Held.LeftOut.Select(LandingSyncWords.Each))} — nothing to land.")
                { Refusal = AutoLandingCode.Nothing, LeftOut = replaying.Held.LeftOut }, notFetched);
        }

        // What a replay made, to move the session's tree to (point 7): a merge's here, a branch's from what it made.
        LandReplay? replayed = null;
        var followed = false;
        if (!branchForm)
        {
            if (replaying is not null)
            {
                var (made, notMade) = await syncs.ReplayAsync(replaying, repository, workspace, replaying.Onto, replaying.Held.Kept, ct).ConfigureAwait(false);
                if (notMade is not null) return Fetched(notMade, notFetched);
                replayed = made;
            }

            var merged = await MergeAsync(path, ct, replayed).ConfigureAwait(false);
            landed = new(merged.Merged, merged.Message) { LeftOut = merged.Merged ? replayed?.LeftOut : null };
            landed = Fetched(landed, notFetched);
        }
        else
        {
            // A squash reads the work's subject whatever the pattern reads (ACCEPTFOOT2b): its commit is named by it too.
            var worked = await ForWorkAsync(full, workspace, repository, landing.Rule, subject, ct, always: landing.Rule.Squash).ConfigureAwait(false);
            var branched = await BranchAsync(full, workspace, repository,
                BranchNamed(worked, landing.Rule, repository),
                handedOn: plugin is not null, ct,
                entered: subject.EnteredName is not null, squash: landing.Rule.Squash ? worked : null, sync: replaying,
                join: joins ? sync : null, pinned: gate?.Tip).ConfigureAwait(false);
            landed = Fetched(branched.Landing, notFetched);
            replayed = landed.Landed ? branched.Replay : null;
            // ACCEPTALL1: a part that joined its entry's branch by a replay follows it as a replay onto the line does (point 7).
            if (replayed is not null && replaying is null && joins) replaying = sync;
            followed = branched.Followed;
            squashedTip = landed.Landed ? branched.Squashed : null;
            // Recorded the moment it exists (WSR5): the branch is Daoris's to judge and to hand on only
            // because this line wrote it down, at the commit it was made at.
            var tip = branched.Commits.Count > 0 ? branched.Commits[^1].Sha : null;
            if (landed.Landed && tip is not null && landed.AdvancedFrom is { } advancedFrom)
            {
                // Moved on (LAND2c): the entry's tip is the new commit, and the advance is kept with who accepted it (D143).
                landed = Remember(landed, () => context.Recorded.Advanced(repository, landed.Branch!,
                    new LandedAdvance(advancedFrom, tip, DateTimeOffset.UtcNow, subject.Session)
                    {
                        AcceptedBy = acceptedBy, Review = review?.Landing, Opinion = gate?.Opinion.Landing, Workflow = named?.Record,
                        // The record moves to the session tip this advance squashed (PROCESS1b-ii point 4); a fast-forward keeps it.
                        Squashed = branched.Squashed,
                    }, UnitOf(full, repository)));
            }
            else if (landed.Landed && tip is not null)
            {
                // Where its work grew from (WSR6): the session branch's start, so bringing it up to date replays only its own; the line's
                // commit a replay was made onto (LANDSYNC1).
                var from = branched.Replay is not null && replaying is not null ? replaying.Onto
                    : branched.Source is { } source ? context.Grown.Of(repository, source)?.From : null;
                landed = Remember(landed, () => context.Recorded.Record(new LandedBranch(
                    repository, workspace, landed.Branch!, branched.Base, tip, subject.Session, subject.Quest, subject.Title,
                    DateTimeOffset.UtcNow)
                {
                    From = from,
                    // Who accepted it, and the rule as it stood (LAND2b, D145 point 6): a choice keeps its facts (D143).
                    AcceptedBy = acceptedBy,
                    Rule = new LandedRule(plugin, landing.Rule.AutoAccept, landing.Source),
                    // The review that let it go (REVIEWENV1c, design §3.5): reviewed with its set-up's commit, or skipped.
                    Review = review?.Landing,
                    // The second opinion that let it go (XAGENT1f, design §8.6), or why it landed without one.
                    Opinion = gate?.Opinion.Landing,
                    // The named version that decided (WORKFLOW1f, the workflow design §5.5); none under Current.
                    Workflow = named?.Record,
                    // The session tip its one commit was made from (PROCESS1b-ii point 3): what proves the session landed.
                    Squashed = branched.Squashed,
                    // The branch work unit whose work it carries (FABRIC-END2, design §D2), where the session's entry has one.
                    Units = UnitOf(full, repository) is { } unit ? [unit] : [],
                }));
            }

            if (landed.Landed && unready is not null)
            {
                // At done (D145 point 2): the branch is the half Daoris owns, so it stands, and the hand-off pushes it later (D102).
                landed = landed with
                {
                    Unready = unready,
                    Message = landed.Message.Replace(TreeStays, "", StringComparison.Ordinal) + $" {Sentence(unready)} So its push was not "
                        + $"tried: the branch stands, and `daoris-driver trees hand {subject.Session}` hands it to `{plugin}` once it can "
                        + "land work here." + TreeStays,
                };
            }
            else if (landed.Landed && plugin is not null)
            {
                // An advance grows the pull request its first landing opened (LAND2c, D149 point 4): the plugin is told it, so it
                // pushes and opens no second one, and is told who accepted the work, so its description says so truly.
                var open = landed.AdvancedFrom is null ? null : context.Recorded.Of(repository, landed.Branch!) is { Pushed: true } entry ? entry.PullRequest : null;
                var said = await context.Plugins.LandAsync(plugin, new LandingFrame(
                    repository, workspace, branched.Root, landed.Branch!, branched.Base, subject.Title, subject.Quest,
                    subject.Session, branched.Commits, open, acceptedBy), ct).ConfigureAwait(false);
                landed = landed with
                {
                    Plugin = said,
                    // The plugin's word goes before the sentence about the tree, which a tidy may take out.
                    Message = landed.Message.Replace(TreeStays, "", StringComparison.Ordinal) + " " + Said(said, landed.Branch!) + TreeStays,
                };
                if (said is { Pushed: true, Failed: false } && tip is not null)
                {
                    landed = Remember(landed, () => context.Recorded.Pushed(repository, landed.Branch!, said, tip));
                }
            }
        }

        // Which named version decided (WORKFLOW1f, the workflow design §5.5), said where the landing is said, before the tree's
        // sentence a tidy takes out: a merge keeps no landing record, and its conversation's note is its record.
        if (landed.Landed && named is not null)
        {
            var decided = $" It landed as {named.Name} says, {WorkflowRunBinding.Why(named.Binding!)}.";
            landed = landed with
            {
                Message = landed.Message.EndsWith(TreeStays, StringComparison.Ordinal)
                    ? landed.Message[..^TreeStays.Length] + decided + TreeStays
                    : landed.Message + decided,
            };
        }

        // LANDSYNC1 (D87's amendment point 7): the session follows what landed, so its next turn starts in sync with the line. Before the
        // rule's tidy, whose proof then finds the tree's branch inside what landed; where it cannot move, the landing stands and says why.
        if (landed.Landed && replayed is not null && replaying is not null && !followed)
        {
            var stays = await syncs.MoveTreeAsync(full, replaying, replayed.Now, repository, workspace,
                inUse is null ? null : await inUse(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            landed = landed with { Message = BeforeTreeStays(landed.Message, LandingSyncWords.Followed(stays)) };
        }

        // The tidy the person's rule asked for (D88): the tree and its branch go once the work lands, behind
        // the same proof an unforced removal makes — never forced.
        if (!landed.Landed || !landing.Rule.Tidy) return landed;
        // Read before the tree goes (LAND3): the repository it belongs to and the branch it was on.
        var (factsCode, commonDir, _, pressedOut, _) = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (factsCode == 0)
        {
            // FABRIC-END2: what the line holds now (a merge's work) is kept before the tidy takes the branch, so its entry outlives it as a
            // trace; work handed to a landing branch stays open on its unit. The end judgement's own writer, in the lease's process only.
            var checkout = Path.GetDirectoryName(commonDir.Trim())!;
            var judged = (await context.LineAsync(checkout, repository, workspace, ct).ConfigureAwait(false)).Branch;
            await ends.KeepAsync(checkout, repository, workspace, judged, ct).ConfigureAwait(false);
        }

        var tidied = await removals.RemoveAsync(path, force: false, ct).ConfigureAwait(false);
        landed = landed with
        {
            // 🔴 The sentence that the tree stays goes when the tidy removed it: the message said both
            // (found landing TK-2202, 2026-09-29).
            Message = tidied.Removed
                ? landed.Message.Replace(TreeStays, "", StringComparison.Ordinal) + $" Cleaned up, as the rule says: {tidied.Message}"
                : landed.Message + $" The rule says to clean up, and the tree stays: {tidied.Message}",
        };
        if (factsCode != 0) return landed with { Tidied = [] };

        // LAND3: every other session branch the landed work holds goes too — a chain's earlier step, whose commits rode
        // into this one, stayed behind on the owner's install. The ref is the branch made, or the line a merge went into.
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var into = landing.Rule.Form == LandingForm.Branch
            ? landed.Branch
            : (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        if (into is null) return landed with { Tidied = [] };
        // A squash holds no session commit, so what it squashed is what it holds (PROCESS1b-ii point 3).
        var held = await tidies.TidyHeldAsync(root, repository, workspace, squashedTip ?? $"refs/heads/{into}", pressedOut.Trim(), inUse, ct)
            .ConfigureAwait(false);
        return landed with { Message = landed.Message + TreeTidies.TidiedSaid(held, into), Tidied = held };
    }

    /// <summary>
    /// The branch work unit of the session tree at <paramref name="full"/> (FABRIC-END2): its standing entry's, found by the folder the
    /// layout names for its branch; null for an entry from before the unit was written, which no landing can name.
    /// </summary>
    private string? UnitOf(string full, string repository) => context.Grown.All()
        .LastOrDefault(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(Path.Combine(context.TreesRoot, entry.Workspace, entry.Repository, Path.GetFileName(entry.Branch))),
                Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase))?.Unit;

    /// <summary>Words said before the sentence that the tree stays, which a tidy takes out; at the end where it is not there.</summary>
    private static string BeforeTreeStays(string message, string words) => message.EndsWith(TreeStays, StringComparison.Ordinal)
        ? message[..^TreeStays.Length] + words + TreeStays
        : message + words;

    /// <summary>A failed fetch's words (LANDSYNC1, D87's amendment point 6), said with whatever the landing came to; none where it fetched.</summary>
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

    /// <summary>
    /// Put a session's work on a new branch, from the session's own branch, which grew from the line.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>It writes nothing to the checkout</b>: it makes one ref in the repository and moves no
    /// checkout, so the root may be dirty and on any branch, and is left exactly so. A branch of that
    /// name already there is refused and never moved — it may be somebody's. Nothing pushes (D87).
    /// </remarks>
    /// <param name="handedOn">A plugin pushes it next (D100), so the sentence does not tell the person to.</param>
    /// <param name="squash">
    /// The session the rule squashes (PROCESS1b-ii), whose title and id the one commit's message says; null where the rule keeps
    /// the session's own commits. A tree on a branch of its own is never squashed.
    /// </param>
    /// <param name="sync">
    /// What the line holds of the tree's commits, where it holds some (LANDSYNC1, D87's 2026-10-15 amendment points 2 and 3): only the
    /// rest is replayed, aside, onto the line's latest, and the branch is made at the replay's tip; a branch the landing advances takes
    /// what neither it nor the line holds, replayed onto its tip; a branch of the tree's own is replayed in place. Null lands the
    /// commits as made.
    /// </param>
    /// <param name="join">
    /// The tree's judgement where it joins its entry's branch (ACCEPTALL1), even where the line holds none of its commits: where the
    /// branch stands and the tree's tip does not grow from it, what neither holds is replayed onto the branch's tip, as
    /// <paramref name="sync"/>'s would be. Null advances only by the session's own tip, as before.
    /// </param>
    /// <param name="pinned">
    /// The tip the door judged (FABRIC-END7's review, <see cref="LandingGate.Tip"/>): the session's branch must stand there when its
    /// commits are read, or nothing lands, and the branch is made or moved to that commit, so one made after the judgement never lands
    /// unseen. Null lands the branch as it stands, as the press does.
    /// </param>
    private async Task<Branched> BranchAsync(
        string full, string workspace, string repository, string name, bool handedOn, CancellationToken ct, bool entered = false,
        LandingSubject? squash = null, LandSync? sync = null, LandSync? join = null, string? pinned = null)
    {
        if (!context.Holds(full))
        {
            return new TreeLanding(false, $"{full} is not a session tree — this lands only trees Daoris opened, under {context.TreesRoot}.");
        }

        if (!Directory.Exists(full))
        {
            return new TreeLanding(false, $"there is no tree at {full}.");
        }

        // Its repository and its branch, one start (PROC1d-iii).
        var here = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
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
        var (_, dirty, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain"], ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(dirty))
        {
            return new TreeLanding(false,
                $"the session's tree has uncommitted work — {dirty.Trim().Split('\n').Length} path(s) — which a "
                + "branch would leave behind. Commit it in the tree first, or decide it is not wanted.")
            { Refusal = AutoLandingCode.Uncommitted };
        }

        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? "HEAD" : await ComparableAsync(root, line, ct).ConfigureAwait(false) ?? line;
        // Oldest first, each commit's id and subject: what a plugin writes a pull request from (D100).
        var (logCode, ahead, logErr) = await WorkingTree.GitAsync(
            root, ["log", "--reverse", "--format=%H%x09%s", $"{against}..{branch}"], ct).ConfigureAwait(false);
        if (logCode != 0)
        {
            return new TreeLanding(false, $"git cannot compare `{branch}` with `{line ?? "HEAD"}` here: {FirstLine(logErr)} "
                + "Fetch it, or set another line with `daoris driver line`.");
        }

        if (string.IsNullOrWhiteSpace(ahead))
        {
            return new TreeLanding(false, $"`{branch}` holds nothing `{line ?? "HEAD"}` does not — nothing to land.")
            { Refusal = AutoLandingCode.Nothing };
        }

        var commits = ahead.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('\t', 2))
            .Select(parts => new LandingCommit(parts[0], parts.Length > 1 ? parts[1] : ""))
            .ToList();

        // FABRIC-END7's review: pinned, the work read here is the work judged, or nothing lands.
        if (pinned is not null && !string.Equals(commits[^1].Sha, pinned, StringComparison.OrdinalIgnoreCase))
        {
            return new TreeLanding(false, $"`{branch}` moved after its landing was judged: it stands at {TextBounds.ShortCommit(commits[^1].Sha, 8)}, "
                + $"not {TextBounds.ShortCommit(pinned, 8)}. Nothing was landed; it is judged again at its new tip.")
            { Refusal = AutoLandingCode.Refused };
        }

        // TREEFOLLOW1 (D164 point 4): the tree its agent moved onto a branch of its own lands as that branch stands: it is already the
        // pull request's branch, named as the repository names its own, so none is made and the rule's pattern is not asked.
        if (!branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal))
        {
            if (entered) return new TreeLanding(false, EnteredOwn(branch));
            var pushOwn = handedOn ? "" : $" Push it and open the pull request from there: `git push -u origin {branch}`.";
            // LANDSYNC1 (point 3): where the line holds some of its commits, the branch is replayed in place and keeps its name; one on its
            // remote is never rewritten (D109), and is refused as a conflict is.
            if (sync is not null)
            {
                if (await OnRemoteAsync(root, branch, ct).ConfigureAwait(false)) return TreeLandSyncs.OwnOnRemote(sync);
                var (made, refused) = await syncs.ReplayAsync(sync, repository, workspace, sync.Onto, sync.Held.Kept, ct).ConfigureAwait(false);
                if (refused is not null) return refused;
                if (await syncs.MoveTreeAsync(full, sync, made!.Now, repository, workspace, inUse: null, ct).ConfigureAwait(false) is { } stays)
                {
                    return new TreeLanding(false, $"Nothing was landed: `{branch}`, the session's own branch, could not be replayed in place ({stays}). "
                        + "The tree and its branch are as they were.") { Refusal = AutoLandingCode.Refused, LeftOut = sync.Held.LeftOut };
                }

                return new Branched(
                    new TreeLanding(true,
                        $"lands `{branch}`, the session's own branch, replayed in place — {LandingSyncWords.Replayed(made.Landed.Count, sync.OntoName, made.LeftOut)}."
                        + $"{LandingSyncWords.Emptied(made.LeftOut)} No other branch was made.{pushOwn} Nothing was merged and the checkout was not touched."
                        + $"{LandingSyncWords.Followed(null)}{TreeStays}",
                        branch) { LeftOut = made.LeftOut },
                    root, line, made.Landed, follows.BranchOfTree(full)?.Branch)
                { Replay = made, Followed = true };
            }

            return new Branched(
                new TreeLanding(true,
                    $"lands `{branch}`, the session's own branch, as it stands — {commits.Count} commit(s) from `{line ?? "HEAD"}`, and no "
                    + $"other branch was made.{pushOwn} Nothing was merged and the checkout was not touched.{TreeStays}",
                    branch),
                root, line, commits, follows.BranchOfTree(full)?.Branch);
        }

        if (!BranchName.IsValid(name))
        {
            return new TreeLanding(false, entered ? EnteredInvalid(name)
                : $"the landing rule names `{name}` for this session, which is not a branch name git "
                + "would take. Change the pattern with `daoris driver landing`.");
        }

        // PROCESS1b-ii (point 1): the squash is a commit Daoris makes, so git's own identity here makes it, asked before anything is.
        if (squash is not null && await TreeSquashes.IdentityProblemAsync(root, repository, ct).ConfigureAwait(false) is { } nobody)
        {
            return new TreeLanding(false, nobody);
        }

        var (exists, standing, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{name}^{{commit}}"], ct).ConfigureAwait(false);
        if (exists == 0 && entered && context.Recorded.Of(repository, name) is null)
        {
            return new TreeLanding(false, EnteredStands(name, repository)) { Refusal = AutoLandingCode.Exists };
        }

        if (exists == 0)
        {
            // A chain's later done moves the chain's branch on (LAND2c, D149): only one Daoris made, as a fast-forward, or under
            // a squash by one more commit (PROCESS1b-ii point 4).
            var standsAt = standing.Trim();
            // Under a squash the branch holds the session tip it squashed by content alone (PROCESS1b-ii point 4): work grown from that
            // tip grows from the branch, and its commits up to it are on the branch already (ACCEPTALL1d).
            var squashedTip = squash is null ? null : context.Recorded.Of(repository, name)?.Squashed;
            var grown = squashedTip ?? standsAt;
            // ACCEPTALL1 (D167's amendment point 2): a part that ran alongside, its work not grown from the entry's branch, joins it by
            // the same replay as what the line partly holds; one grown from it still moves it by its own tip.
            var onto = sync ?? (join is not null && !await IsAncestorAsync(root, grown, join.Tip, ct).ConfigureAwait(false) ? join : null);
            if (onto is null)
            {
                return await AdvanceAsync(root, repository, name, standsAt, branch, line, commits, handedOn, ct, against, squash, pinned: pinned).ConfigureAwait(false);
            }

            // LANDSYNC1 (point 3): what neither the branch nor the line holds, replayed onto the branch's tip, which then moves by D145
            // §3's fast-forward as before.
            var lacking = new List<LandingCommit>();
            foreach (var commit in onto.Held.Kept)
            {
                if (await IsAncestorAsync(root, commit.Sha, standsAt, ct).ConfigureAwait(false)) continue;
                if (squashedTip is not null && await IsAncestorAsync(root, commit.Sha, squashedTip, ct).ConfigureAwait(false)) continue;
                lacking.Add(commit);
            }

            var (onBranch, refusedOn) = await syncs.ReplayAsync(onto, repository, workspace, standsAt, lacking, ct, joining: sync is null ? name : null)
                .ConfigureAwait(false);
            if (refusedOn is not null) return refusedOn;
            var holds = await LineHolds.CommitsAsync(root, $"{against}..{onBranch!.Now}", ct).ConfigureAwait(false) ?? onBranch.Landed;
            var advanced = await AdvanceAsync(root, repository, name, standsAt, branch, line, holds, handedOn, ct, against, squash, onBranch.Now)
                .ConfigureAwait(false);
            // A part that joined with nothing the line holds left nothing out: said as a landing of the commits as made says it.
            var joined = sync is null;
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

        // PROCESS1b-ii (points 1 and 2): one commit of the session's tip's tree, its parent where the session left the line, and the
        // branch made at it; the session's own branch is left as it is, and the record keeps the tip squashed. Pinned, at the tip judged.
        var start = pinned ?? branch;
        string? squashed = null;
        IReadOnlyList<LandingCommit> carried = commits;
        // LANDSYNC1 (point 2): only what the line lacks, replayed aside onto the line's latest; the branch is made at the replay's tip.
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
            var (made, why) = await TreeSquashes.SquashAsync(root, tip, mergeBase.Trim(), SquashMessage.Of(squash.SquashTitle, squashing, squash.Session), ct)
                .ConfigureAwait(false);
            if (made is null) return new TreeLanding(false, $"git would not make the squashed commit for `{name}`: {why} Nothing was landed.");
            start = made;
            squashed = tip;
            carried = [new LandingCommit(made, SquashMessage.Subject(squash.SquashTitle, squashing, squash.Session))];
        }

        var (code, _, err) = await WorkingTree.GitAsync(root, ["branch", name, start], ct).ConfigureAwait(false);
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
                + (squashed is null ? "" : ", squashed into one commit") + $".{(replayed is null ? "" : LandingSyncWords.Emptied(replayed.LeftOut))}"
                + $"{pushIt} Nothing was merged and the checkout was not touched.{TreeStays}",
                name) { LeftOut = replayed?.LeftOut },
            root, line, carried, branch)
        { Squashed = squashed, Replay = replayed };
    }

    /// <summary>
    /// Move a standing branch on to the session's work (LAND2c, D145 §3, D149): the chain's branch, which an earlier done
    /// made, grows by this session's commits, so a ticket has one branch and one pull request.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Only a fast-forward of a branch Daoris made, and never forced.</b> It must be the record's (D102), standing at
    /// the recorded tip, checked out in no working tree, with its work not already on the line, and the session's tip must grow
    /// from it with something new (<see cref="LandingAdvance"/>). Then git moves it by compare-and-swap from the tip judged
    /// (<c>update-ref</c>), so a branch moved in between is refused by git itself. Like the branch form, it writes one ref and
    /// touches no checkout.</para>
    ///
    /// <para><b>A branch whose pull request was merged is not moved on</b> (D149 point 3): D102's proof on the recorded tip, the
    /// same one the clean-up and the hand-off make, finds its work on the line, and commits added now would ride no pull
    /// request.</para>
    /// </remarks>
    /// <param name="standsAt">The commit the branch stands at, read a moment ago.</param>
    /// <param name="branch">The session's own branch.</param>
    /// <param name="commits">What the branch would hold beyond the line once moved: what a plugin is told, as at a first landing.</param>
    /// <param name="against">The line's form the commits were counted from, which a squash's branch is counted from again.</param>
    /// <param name="squash">
    /// The session the rule squashes (PROCESS1b-ii point 4): the advance is one more commit on the branch, its tree the session's
    /// tip's, its parent the branch's tip, its message only what was added since the recorded tip; null is the fast-forward.
    /// </param>
    /// <param name="replayed">
    /// The tip of what the line lacks replayed onto the branch's (LANDSYNC1), which the branch moves to in place of the session's tip;
    /// null moves it to the session's tip, as before.
    /// </param>
    /// <param name="pinned">The session's tip the door judged (FABRIC-END7's review), which the branch moves to in place of the tip read now.</param>
    private async Task<Branched> AdvanceAsync(
        string root, string repository, string name, string standsAt, string branch, string? line,
        IReadOnlyList<LandingCommit> commits, bool handedOn, CancellationToken ct, string? against = null, LandingSubject? squash = null,
        string? replayed = null, string? pinned = null)
    {
        var recorded = context.Recorded.Of(repository, name);
        var (_, headOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct)
            .ConfigureAwait(false);
        var head = replayed ?? pinned ?? headOut.Trim();
        // What the session's work must grow from: the tip the record says was squashed where it says one (point 4), else the tip
        // the branch was made or last moved at, its commits the session's own; a replay onto that tip grows from it (ACCEPTALL1d).
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
            return await TreeSquashes.AdvanceSquashedAsync(root, name, standsAt, branch, line, head, grewFrom!, handedOn, against, squash, ct)
                .ConfigureAwait(false);
        }

        // Compare-and-swap from the tip judged: git refuses where the branch moved since, and nothing is forced.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: {branch} moves {name} on", $"refs/heads/{name}", head, standsAt], ct).ConfigureAwait(false);
        if (moved != 0)
        {
            return new TreeLanding(false, $"git would not move `{name}` on from {TextBounds.ShortCommit(standsAt, 8)}: "
                + $"{FirstLine(moveErr)} It may have moved meanwhile; nothing was changed.")
            { Refusal = AutoLandingCode.Exists };
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

    /// <summary>What a pattern is expanded from: the quest, or the session where there is none; the quest's name's words (LANDNAME1).</summary>
    private static string EnteredInvalid(string name) =>
        $"the branch name you entered, `{name}`, is not a branch name git would take. Enter another, or leave it blank for the rule's.";

    /// <summary>An entered name that is already a branch Daoris did not make (LANDNAME1a): the press's refusal, which the plan says first.</summary>
    private static string EnteredStands(string name, string repository) =>
        $"the branch name you entered, `{name}`, is already a branch in `{repository}`, and Daoris does "
        + "not move a branch it did not make. Enter another name, or leave it blank for the rule's.";

    /// <summary>A name entered where the tree's own branch lands as it stands (TREEFOLLOW1): refused, not ignored, as a merge's is.</summary>
    private static string EnteredOwn(string own) =>
        $"this session's tree is on its own branch, `{own}`, which lands as it stands and is not renamed, so there is no branch name "
        + "to enter. Leave it blank.";

    private const string EnteredMerge = "the rule for this repository merges the work into its line and makes no branch, so there is no "
        + "branch name to enter. Leave it blank, or change the rule with `daoris driver landing`.";

    /// <summary>
    /// What names the branch: the person's entered name where there is one (LANDNAME1a), else the rule's pattern expanded, its
    /// <c>{ticket}</c> found by the rule's key (PROCESS1b-i). The plan and the press both ask it, so they name one branch.
    /// </summary>
    private static string BranchNamed(LandingSubject subject, LandingRule rule, string repository) =>
        subject.EnteredName ?? subject.EntryBranch ?? LandingRules.Branch(subject, rule, repository);

    /// <summary>
    /// The subject a conversation's branch is named for (ACCEPTFOOT2, D164's 2026-10-16 note): <see cref="LandingSubject.ForWork"/>
    /// with the oldest commit subject its work holds beyond the line, as the press's own log reads it. Git is asked only where the rule
    /// would read a conversation's words (<see cref="LandingRules.ReadsWords"/>), so a quest's plan, an entered name, an entry's branch
    /// and a pattern of ids alone start none.
    /// </summary>
    /// <param name="full">A tree of the repository the work is in; its HEAD is the work unless <paramref name="tip"/> names it.</param>
    /// <param name="tip">The work's tip where it is not that tree's HEAD: an entry's first part landed from a tree since gone.</param>
    internal async Task<LandingSubject> ForWorkAsync(
        string full, string workspace, string repository, LandingRule rule, LandingSubject subject, CancellationToken ct, string? tip = null, bool always = false)
    {
        if (!subject.NamedByOpening || subject.EnteredName is not null || subject.EntryBranch is not null || !(always || LandingRules.ReadsWords(rule))) return subject;
        var here = Directory.Exists(full) ? await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false) : null;
        if (here is not { Code: 0 }) return subject.ForWork(null);
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(here.CommonDir.Trim()))!;
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        if (line is null) return subject.ForWork(null);
        var against = await ComparableAsync(root, line, ct).ConfigureAwait(false) ?? line;
        var (code, log, _) = await WorkingTree.GitAsync(
            full, ["log", "--reverse", LandingRules.SubjectsFormat, $"{against}..{tip ?? "HEAD"}"], ct).ConfigureAwait(false);
        return subject.ForWork(code == 0 ? LandingRules.OldestSubject(log) : null);
    }

    /// <summary>
    /// An entry's subject named for its first part's work (ACCEPTFOOT2): read from that part's tree where it is here, else from the tip
    /// its landing recorded, in <paramref name="anyTree"/>, a tree of the same repository; so the press, the plan and each part's own
    /// Accept name the entry's one branch alike, whichever part's tree they are pressed in.
    /// </summary>
    internal async Task<LandingSubject> EntryForWorkAsync(LandingSubject entry, string? leadTree, string anyTree, CancellationToken ct)
    {
        if (!entry.NamedByOpening) return entry;
        var lead = leadTree is { Length: > 0 } && Directory.Exists(leadTree) ? Path.GetFullPath(leadTree) : null;
        var full = lead ?? Path.GetFullPath(anyTree);
        var (workspace, repository) = context.OwnerOf(full);
        var landing = LandingRules.Choose(context.Config(), repository, workspace);
        if (lead is not null) return await ForWorkAsync(lead, workspace, repository, landing.Rule, entry, ct).ConfigureAwait(false);
        return context.Recorded.Landing(entry.Session) is { } landed
            ? await ForWorkAsync(full, workspace, repository, landing.Rule, entry, ct, landed.Tip).ConfigureAwait(false)
            : LandingRules.ReadsWords(landing.Rule) ? entry.ForWork(null) : entry;
    }
}
