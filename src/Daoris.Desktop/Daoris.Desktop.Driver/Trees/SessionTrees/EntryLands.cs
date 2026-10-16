using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// One part an entry's Accept lands (ACCEPTALL1, D167's 2026-10-15 amendment point 1): its record's id and its tree on this machine.
/// An entry's parts are handed oldest first, in the order <see cref="SessionEntries.Read"/> lists them (their records' <c>created</c>,
/// then id), which is the order they land in.
/// </summary>
/// <param name="Session">The part's record.</param>
/// <param name="Tree">Its tree here, which the record names.</param>
public sealed record EntryLandingPart(string Session, string Tree)
{
    /// <summary>The landing gate its door read for this part (<see cref="SessionTrees.GateAsync"/>); null is a door that asked none.</summary>
    public LandingGate? Gate { get; init; }

    /// <summary>
    /// What the part is named for on its own (TREEFOLLOWNAME1): its own quest and title, which name its landing where its tree is on a
    /// branch of its own (TREEFOLLOW1) and so lands as that branch. Null where the door gave none: the entry's subject names it.
    /// </summary>
    public LandingSubject? Own { get; init; }
}

/// <summary>
/// A part a later part of the same entry redid (ACCEPTALL1, D167's amendment point 3): its commits, replayed after that part's,
/// conflict with them or add nothing. The plan names it and offers to leave it out; only the person's choice leaves it out.
/// </summary>
/// <param name="By">The later part that redid it.</param>
/// <param name="Says">The files: git's <c>CONFLICT</c> lines where it conflicts, else the files it changes, which the later part already holds.</param>
/// <param name="Conflicts">Whether it conflicts after the later part's work, rather than adding nothing.</param>
public sealed record PartSuperseded(string By, string Says, bool Conflicts);

/// <summary>What an entry's Accept would do with one of its parts (ACCEPTALL1): its own plan, as its review's Accept would say it.</summary>
/// <param name="Plan">The part's landing plan, its branch the entry's; null for a part whose tree an earlier part's landing carries.</param>
public sealed record EntryPartPlan(string Session, LandingPlan? Plan)
{
    /// <summary>The earlier part whose tree it shares, whose landing carries its commits once (point 1); null for a tree of its own.</summary>
    public string? SameTreeAs { get; init; }

    /// <summary>A later part that redid its work (point 3); null where none did.</summary>
    public PartSuperseded? Superseded { get; init; }
}

/// <summary>
/// What an entry's Accept would do (ACCEPTALL1, D167's amendment point 1): the rule's form, where the parts land (the entry's one
/// branch, or the line a merge goes into), and each part, oldest first. A part on a branch of its own (TREEFOLLOW1) lands as that
/// branch, its plan's target.
/// </summary>
public sealed record EntryPlan(string Form, string Target, IReadOnlyList<EntryPartPlan> Parts);

/// <summary>What became of one part at an entry's Accept (ACCEPTALL1), one of these words.</summary>
public static class EntryPartOutcome
{
    /// <summary>Its work landed, as <see cref="EntryPartLanding.Landing"/> says.</summary>
    public const string Landed = "landed";

    /// <summary>It was refused, as <see cref="EntryPartLanding.Landing"/> says; the parts after it were not tried.</summary>
    public const string Refused = "refused";

    /// <summary>The person chose to leave it out (point 3): nothing of it was landed, and its tree stays.</summary>
    public const string LeftOut = "left-out";

    /// <summary>Its tree is an earlier part's, whose landing carried its commits.</summary>
    public const string SameTree = "same-tree";

    /// <summary>An earlier part was refused, so it was not tried: the order is the ledger's, and a later part may build on it.</summary>
    public const string NotTried = "not-tried";
}

/// <summary>One part's outcome at an entry's Accept, with its landing's answer where it was tried.</summary>
public sealed record EntryPartLanding(string Session, string Outcome, TreeLanding? Landing = null);

/// <summary>
/// What came of an entry's Accept (ACCEPTALL1): whether every part it tried landed, its sentence, the branch (or line) the parts
/// landed on, and each part. A part that landed before a later one was refused stays landed: each is its own landing, recorded.
/// </summary>
public sealed record EntryLanding(bool Landed, string Message, string? Branch, IReadOnlyList<EntryPartLanding> Parts);

/// <summary>One step of an entry's Accept, decided before git is asked (<see cref="EntryLandings.Sequence"/>).</summary>
/// <param name="SameTreeAs">The earlier part whose tree it shares, or null.</param>
/// <param name="LeftOut">The person chose to leave it out.</param>
public sealed record EntryStep(string Session, string? SameTreeAs, bool LeftOut)
{
    /// <summary>Whether the press lands it: a tree of its own the person kept.</summary>
    public bool Lands => SameTreeAs is null && !LeftOut;
}

/// <summary>
/// An entry's Accept as decisions made before git is asked (ACCEPTALL1): the order and the person's choice, the branch's name after
/// a merged one, and a superseded part's verdict. Pure, so its table holds them without git.
/// </summary>
public static class EntryLandings
{
    /// <summary>How many names a merged branch's successors are tried under: <c>-2</c> to <c>-20</c>, which no entry reaches.</summary>
    public const int Names = 20;

    /// <summary>
    /// Each part's step, in the order handed (the ledger's): a part whose tree an earlier part already has lands with it, once; a
    /// part the person chose to leave out does not land. Nothing else leaves a part out: a superseded part the person kept lands.
    /// </summary>
    public static IReadOnlyList<EntryStep> Sequence(IReadOnlyList<EntryLandingPart> parts, IReadOnlySet<string> leaveOut)
    {
        var trees = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var steps = new List<EntryStep>(parts.Count);
        foreach (var part in parts)
        {
            var tree = Normal(part.Tree);
            var same = trees.GetValueOrDefault(tree);
            if (same is null) trees[tree] = part.Session;
            steps.Add(new EntryStep(part.Session, same, same is null && leaveOut.Contains(part.Session)));
        }

        return steps;
    }

    /// <summary>
    /// The name tried <paramref name="n"/>th for an entry's branch: the rule's own first, then <c>&lt;name&gt;-2</c> and on, since a
    /// merged pull request takes nothing more and the entry's next work makes a new branch, still named for its first quest.
    /// </summary>
    public static string Candidate(string name, int n) => n <= 1 ? name : $"{name}-{n}";

    /// <summary>
    /// Whether <paramref name="earlier"/>'s work is superseded by a later part's (point 3), from two trials over trees: the later
    /// part's work replayed onto the line (<paramref name="later"/>'s tree), then the earlier's after it. A conflict, or a tree no
    /// different from the later part's alone, is superseded; a trial git could not make says nothing.
    /// </summary>
    /// <param name="files">The files the earlier part changes, said where it adds nothing.</param>
    public static PartSuperseded? Verdict(
        string by, string? later, (string? Tree, string? Conflicts) after, IReadOnlyList<string> files)
    {
        if (later is null) return null;
        if (after.Conflicts is { } conflicts) return new PartSuperseded(by, conflicts, Conflicts: true);
        if (after.Tree is null || !string.Equals(after.Tree, later, StringComparison.OrdinalIgnoreCase)) return null;
        return new PartSuperseded(by, Files(files), Conflicts: false);
    }

    /// <summary>Up to three files by name, and how many more.</summary>
    public static string Files(IReadOnlyList<string> files) =>
        string.Join(", ", files.Take(3)) + (files.Count > 3 ? $" and {files.Count - 3} more" : "");

    private static string Normal(string tree) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
}

/// <summary>
/// An entry's Accept (ACCEPTALL1, D167's 2026-10-15 amendment points 1 to 3): its parts' work lands on one branch, in ledger order,
/// each part by its own landing (<see cref="TreeLands.LandAsync"/>: its gate, the line's judgement, its record and its plugin), the
/// parts after the first joining the branch the first made or moved on. A part the person chose to leave out is not landed; one a
/// later part redid is named in the plan and still lands unless they chose. Over the trees' context, the landing and its judgement.
/// </summary>
internal sealed class EntryLands(TreeContext context, TreeLands lands, TreeLandSyncs syncs)
{
    /// <summary>
    /// What an entry's Accept would do (point 1), said before the press: the branch its parts land on, each part's own plan, and a
    /// part a later part superseded (point 3), tried over trees alone (<see cref="LineHolds.TrialTreeAsync"/>). It reaches no network
    /// and writes no ref and no tree.
    /// </summary>
    /// <param name="subject">What the entry is named for: its first part's, the chain's first quest walked up as a chain's is.</param>
    internal async Task<EntryPlan> PlanAsync(IReadOnlyList<EntryLandingPart> parts, LandingSubject subject, CancellationToken ct = default)
    {
        var (form, target) = await TargetAsync(parts, subject, ct).ConfigureAwait(false);
        var steps = EntryLandings.Sequence(parts, new HashSet<string>(StringComparer.Ordinal));
        var planned = new List<EntryPartPlan>(parts.Count);
        var judged = new List<(int At, LandSync Sync)>();
        for (var at = 0; at < parts.Count; at++)
        {
            var part = parts[at];
            if (steps[at].SameTreeAs is { } same)
            {
                planned.Add(new EntryPartPlan(part.Session, null) { SameTreeAs = same });
                continue;
            }

            planned.Add(new EntryPartPlan(part.Session,
                await lands.PlanAsync(part.Tree, Joining(await NamedAsync(part, subject, form, ct).ConfigureAwait(false), part.Session, form, target), ct,
                    part.Gate?.Process).ConfigureAwait(false)));
            var full = Path.GetFullPath(part.Tree);
            var (workspace, repository) = context.OwnerOf(full);
            var (sync, _) = await syncs.JudgeAsync(full, repository, workspace, form == LandingForm.Branch, fetch: false, ct).ConfigureAwait(false);
            if (sync is not null) judged.Add((at, sync));
        }

        // Point 3: an earlier part whose kept commits, replayed after a later part's, conflict with them or add nothing.
        for (var i = 0; i < judged.Count; i++)
        {
            var (at, earlier) = judged[i];
            if (earlier.Held.Kept.Count == 0) continue;
            var own = earlier.Held.Kept.Select(commit => commit.Sha).ToHashSet(StringComparer.Ordinal);
            for (var j = i + 1; j < judged.Count; j++)
            {
                var later = judged[j].Sync;
                // A later part grown from this one's commits carries them on, which supersedes nothing.
                if (later.Held.Commits.Any(commit => own.Contains(commit.Sha))) continue;
                var laterKept = later.Held.Kept;
                if (laterKept.Count == 0) continue;
                var (laterTree, _) = await LineHolds.TrialTreeAsync(later.Root, later.Onto, laterKept, ct).ConfigureAwait(false);
                if (laterTree is null) continue;
                var after = await LineHolds.TrialTreeAsync(earlier.Root, laterTree, earlier.Held.Kept, ct).ConfigureAwait(false);
                var files = await LandedProofs.NamesAsync(earlier.Root, $"{earlier.Held.Kept[0].Sha}^", earlier.Held.Kept[^1].Sha, ct).ConfigureAwait(false) ?? [];
                if (EntryLandings.Verdict(parts[judged[j].At].Session, laterTree, after, files) is not { } superseded) continue;
                planned[at] = planned[at] with { Superseded = superseded };
                break;
            }
        }

        return new EntryPlan(form, target, planned);
    }

    /// <summary>
    /// The press (points 1 to 3): each part the person kept, in order, landed on the entry's one branch, or merged into the line
    /// where the rule merges. 🔴 A gate that holds any part holds the whole entry, in that gate's words, before anything lands
    /// (point 4). A refusal stops the parts after it, since the order is the ledger's and a later part may build on an earlier
    /// one; what landed before it stands. A part with nothing left to land after the others does not stop them.
    /// </summary>
    /// <param name="leaveOut">The parts the person chose to leave out (point 3); a superseded part not in it lands.</param>
    /// <param name="inUse">The trees sessions still running or waiting hold, asked by each part's follow and tidy.</param>
    internal async Task<EntryLanding> LandAsync(
        IReadOnlyList<EntryLandingPart> parts, LandingSubject subject, IReadOnlySet<string> leaveOut, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null)
    {
        var steps = EntryLandings.Sequence(parts, leaveOut);
        for (var at = 0; at < parts.Count; at++)
        {
            if (!steps[at].Lands || parts[at].Gate?.Refusal is not { } held) continue;
            return new EntryLanding(false, EntryLandWords.Held(parts[at].Session, held.Message), null,
                [.. parts.Select((part, each) => each == at ? new EntryPartLanding(part.Session, EntryPartOutcome.Refused, held)
                    : new EntryPartLanding(part.Session, steps[each].Lands ? EntryPartOutcome.NotTried : Outcome(steps[each])))]);
        }

        var (form, target) = await TargetAsync(parts, subject, ct).ConfigureAwait(false);
        // ACCEPTALL1d (D87's 2026-10-15 amendment point 6): the press fetches the entry's line once, its parts all in one repository,
        // and each part is judged against that fetch; a failed one is said once, in the entry's answer.
        var notFetched = await FetchOnceAsync(parts, steps, ct).ConfigureAwait(false);
        var outcomes = new List<EntryPartLanding>(parts.Count);
        var landedOn = new List<string>();
        string? stopped = null;
        for (var at = 0; at < parts.Count; at++)
        {
            var (part, step) = (parts[at], steps[at]);
            if (!step.Lands)
            {
                outcomes.Add(new EntryPartLanding(part.Session, Outcome(step)));
                continue;
            }

            if (stopped is not null)
            {
                outcomes.Add(new EntryPartLanding(part.Session, EntryPartOutcome.NotTried));
                continue;
            }

            var landed = await lands.LandAsync(part.Tree, Joining(await NamedAsync(part, subject, form, ct).ConfigureAwait(false), part.Session, form, target),
                ct, inUse, AcceptedBy.Person, part.Gate,
                joins: form == LandingForm.Branch, fetched: true).ConfigureAwait(false);
            if (landed.Landed)
            {
                landedOn.Add(part.Session);
                outcomes.Add(new EntryPartLanding(part.Session, EntryPartOutcome.Landed, landed));
                continue;
            }

            // Kept, a conflict with the entry's earlier work names both parts (point 3), and its rebase words go to this, the later one.
            if (landed.Conflict is not null && landedOn.Count > 0) landed = landed with { Message = EntryLandWords.After(part.Session, landedOn) + landed.Message };
            outcomes.Add(new EntryPartLanding(part.Session, EntryPartOutcome.Refused, landed));
            if (landed.Refusal != AutoLandingCode.Nothing) stopped = part.Session;
        }

        var branch = outcomes.Select(outcome => outcome.Landing?.Branch).FirstOrDefault(each => each is not null) ?? target;
        var tried = outcomes.Where(outcome => outcome.Outcome is EntryPartOutcome.Landed or EntryPartOutcome.Refused).ToList();
        var allLanded = tried.Count > 0 && tried.All(outcome => outcome.Outcome == EntryPartOutcome.Landed);
        return new EntryLanding(allLanded, EntryLandWords.Said(form, branch, outcomes) + notFetched, branch, outcomes);
    }

    /// <summary>The press's one fetch (point 6), from the first part it lands whose tree is here; its words where it failed, else null.</summary>
    private async Task<string?> FetchOnceAsync(IReadOnlyList<EntryLandingPart> parts, IReadOnlyList<EntryStep> steps, CancellationToken ct)
    {
        var first = parts.Where((part, at) => steps[at].Lands && Directory.Exists(part.Tree)).FirstOrDefault();
        if (first is null) return null;
        var full = Path.GetFullPath(first.Tree);
        var (workspace, repository) = context.OwnerOf(full);
        return await syncs.FetchLineAsync(full, repository, workspace, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A part's own Accept, in its review (ACCEPTALL1b, D167's 2026-10-15 amendment point 2): the entry's Accept for that part alone, so
    /// it lands as this entry's press would land it, under the entry's subject, on the entry's one branch (<see cref="BranchAsync"/>),
    /// joining it where it ran alongside. Under a merge rule there is no branch to join, and it merges into the line as before.
    /// </summary>
    /// <param name="session">The part pressed: its landing records its own session.</param>
    /// <param name="entry">What the entry is named for: its first part's subject, as <see cref="PlanAsync"/> is handed it.</param>
    internal async Task<LandingSubject> JoinAsync(string tree, string session, LandingSubject entry, CancellationToken ct, WorkflowProcess? process)
    {
        var branch = await BranchAsync(tree, process, entry, ct).ConfigureAwait(false);
        return Joining(entry, session, branch is null ? LandingForm.Merge : LandingForm.Branch, branch ?? "");
    }

    /// <summary>
    /// Where the entry's parts land (point 1): under a branch rule, its one branch (<see cref="BranchAsync"/>); under a merge rule,
    /// the line.
    /// </summary>
    private async Task<(string Form, string Target)> TargetAsync(IReadOnlyList<EntryLandingPart> parts, LandingSubject subject, CancellationToken ct)
    {
        var first = parts.FirstOrDefault(part => Directory.Exists(part.Tree)) ?? parts[0];
        var process = first.Gate?.Process;
        if (await BranchAsync(first.Tree, process, subject, ct).ConfigureAwait(false) is { } branch) return (LandingForm.Branch, branch);
        return (LandingForm.Merge, (await lands.PlanRuleAsync(first.Tree, subject, ct, process).ConfigureAwait(false)).Target);
    }

    /// <summary>
    /// The entry's one branch (point 1), named by the rule for the entry's first part (the chain's first quest, as a chain's is), or,
    /// where that branch's pull request was merged or closed, the first of its successors (<see cref="EntryLandings.Candidate"/>) that
    /// is free or stands open. Null under a merge rule, which makes no branch.
    /// </summary>
    private async Task<string?> BranchAsync(string tree, WorkflowProcess? process, LandingSubject subject, CancellationToken ct)
    {
        var full = Path.GetFullPath(tree);
        var (workspace, repository) = context.OwnerOf(full);
        var landing = process is { Named: true } named ? named.Landing : LandingRules.Choose(context.Config(), repository, workspace);
        if (landing.Rule.Form != LandingForm.Branch) return null;

        // A conversation's entry comes named for its first part's work (ACCEPTFOOT2, `TreeLands.EntryForWorkAsync`), whichever part's
        // tree this is; its opening never names it.
        var name = LandingRules.Branch(subject.NamedByOpening ? subject.ForWork(null) : subject, landing.Rule, repository);
        var here = Directory.Exists(full) ? await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false) : null;
        if (here is not { Code: 0 }) return name;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(here.CommonDir.Trim()))!;
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        for (var n = 1; n <= EntryLandings.Names; n++)
        {
            var candidate = EntryLandings.Candidate(name, n);
            // Free: the press makes it. Not Daoris's, or moved since it was landed on: the press refuses it, said, as a chain's is.
            if (await CommitOfAsync(root, $"refs/heads/{candidate}", ct).ConfigureAwait(false) is not { } stands) return candidate;
            var recorded = context.Recorded.Of(repository, candidate);
            if (recorded is null || !string.Equals(stands, recorded.Tip, StringComparison.OrdinalIgnoreCase)) return candidate;
            if (!await CompletedAsync(root, recorded, line, ct).ConfigureAwait(false)) return candidate;
        }

        return name;
    }

    /// <summary>Whether a recorded branch's pull request was merged or closed: the platform's kept word, else D102's proof on its tip.</summary>
    private static async Task<bool> CompletedAsync(string root, LandedBranch recorded, string? line, CancellationToken ct)
    {
        if (recorded.PullRequestState is { State: PullRequestStates.Completed or PullRequestStates.Abandoned }) return true;
        var proof = await LandedProofs.ProveAsync(root, recorded.Tip, line, await LandedProofs.LineFormsAsync(root, line, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
        return proof.Kind is LandedKind.Merged or LandedKind.OnLine;
    }

    /// <summary>
    /// What one part's landing is named for (TREEFOLLOWNAME1): the entry's subject, unless the tree is on a branch of its own under a
    /// branch rule (TREEFOLLOW1), which lands as that branch and so carries the part's own quest and title.
    /// </summary>
    internal async Task<LandingSubject> NamedAsync(EntryLandingPart part, LandingSubject entry, string form, CancellationToken ct) =>
        form == LandingForm.Branch && part.Own is { } own && await FollowsOwnAsync(part.Tree, ct).ConfigureAwait(false) ? entry.NamedFor(own) : entry;

    /// <summary>Whether a tree is on a branch of its own (TREEFOLLOW1), as <see cref="TreeLands.PlanRuleAsync"/> reads it.</summary>
    internal async Task<bool> FollowsOwnAsync(string tree, CancellationToken ct)
    {
        var full = Path.GetFullPath(tree);
        return Directory.Exists(full) && context.Holds(full) && await TreeFollows.OwnBranchOfAsync(full, ct).ConfigureAwait(false) is not null;
    }

    /// <summary>The subject one part lands as: the entry's, for its own session, on the entry's branch.</summary>
    private static LandingSubject Joining(LandingSubject subject, string session, string form, string target) =>
        subject with { Session = session, EntryBranch = form == LandingForm.Branch ? target : null, Entered = null };

    private static string Outcome(EntryStep step) => step.SameTreeAs is not null ? EntryPartOutcome.SameTree : EntryPartOutcome.LeftOut;
}

/// <summary>An entry's Accept in words (ACCEPTALL1): a part joining the entry's branch, a conflict with its earlier work, and the answer.</summary>
public static class EntryLandWords
{
    /// <summary>A part that ran alongside, its commits replayed onto the entry's branch (point 2), said after the advance's sentence.</summary>
    public static string Joined(string branch, int commits) =>
        $" Its {commits} commit(s) were replayed onto `{branch}`, since its work ran alongside its entry's earlier parts.";

    /// <summary>
    /// A part's replay onto its entry's branch that conflicts with the entry's earlier work there (point 3): nothing made, git's lines,
    /// and the words its review offers to say to its session, asking it to replay its own commits onto that branch.
    /// </summary>
    public static TreeLanding Conflicted(string branch, string conflicts) =>
        new(false, $"Nothing was landed: replaying its own commits onto `{branch}`, its entry's branch, conflicts with the entry's earlier "
            + $"work there: {conflicts} Nothing was made, and the tree and its branch are as they were. Ask its session to replay its own "
            + $"commits onto `{branch}`, then accept again.")
        {
            Refusal = AutoLandingCode.Refused,
            Conflict = new LandingConflict(conflicts, Rebase(branch, conflicts)),
        };

    /// <summary>The words said to the later part's session where its work conflicts with its entry's earlier work (point 3).</summary>
    public static string Rebase(string branch, string conflicts) =>
        $"Rebase your work onto `{branch}`: its entry's earlier work landed there, and landing yours after it conflicts with it: {conflicts} "
        + $"Replay only your own commits onto `{branch}`, keep what that earlier work does not already do, resolve any conflict, and check "
        + "that the work still holds. It is accepted again once your tree has changed.";

    /// <summary>The two parts a conflict is between, said before the refusal: this part's, after the ones that landed before it.</summary>
    public static string After(string part, IReadOnlyList<string> earlier) =>
        $"`{part}`'s work conflicts with what {string.Join(", ", earlier.Select(each => $"`{each}`"))} landed before it. ";

    /// <summary>A gate that holds one part holds its entry's landing (point 4), in that gate's words.</summary>
    public static string Held(string part, string says) =>
        $"Nothing was landed: a gate holds `{part}`, and what holds one part of an entry holds its landing. {says}";

    /// <summary>The answer's sentence: what landed where, what the person left out, what was refused and what was not tried.</summary>
    public static string Said(string form, string? branch, IReadOnlyList<EntryPartLanding> outcomes)
    {
        string Count(string outcome) => outcomes.Count(each => each.Outcome == outcome).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var landed = outcomes.Count(each => each.Outcome == EntryPartOutcome.Landed);
        var said = new List<string>
        {
            landed == 0 ? "Nothing was landed."
                : form == LandingForm.Merge ? $"Merged {landed} part(s) into `{branch}`, in the order they ran."
                : $"Landed {landed} part(s) on `{branch}`, in the order they ran.",
        };
        if (outcomes.Any(each => each.Outcome == EntryPartOutcome.LeftOut)) said.Add($"Left out {Count(EntryPartOutcome.LeftOut)}, as you chose.");
        if (outcomes.FirstOrDefault(each => each.Outcome == EntryPartOutcome.Refused) is { Landing: { } refused } at)
        {
            said.Add($"`{at.Session}` was refused: {refused.Message}");
        }

        if (outcomes.Any(each => each.Outcome == EntryPartOutcome.NotTried))
        {
            said.Add($"{Count(EntryPartOutcome.NotTried)} after it not tried, since a later part may build on it.");
        }

        return string.Join(" ", said);
    }
}
