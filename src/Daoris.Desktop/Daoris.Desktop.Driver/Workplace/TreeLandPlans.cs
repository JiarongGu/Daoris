using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.LandingSayings;

namespace Daoris.Driver;

/// <summary>What a press on a tree would do under its repository's landing rule, said before the press, and the subject named for the work.</summary>
internal sealed class TreeLandPlans(TreeContext context, TreeLandSyncs syncs)
{
    /// <summary>
    /// What a press on this tree would do under its repository's landing rule: merge into
    /// the line, or make the branch the pattern names for this session — said before the press.
    /// </summary>
    /// <remarks>
    /// And what the press would leave out because the line holds it, what it would
    /// land, and whether that conflicts now, judged against the line as last fetched. It reaches no network and writes no ref and no
    /// tree: the conflict's trial is <c>git merge-tree</c>'s, which writes objects only.
    /// </remarks>
    /// <param name="process">
    /// The run's process the door read with its gate: under a named workflow, its version's landing step over the
    /// declared rule. Null reads the rule live, as before.
    /// </param>
    internal async Task<LandingPlan> PlanAsync(string path, LandingSubject subject, CancellationToken ct = default, IWorkplaceProcess? process = null)
    {
        using var once = GitReadOnce.Within();
        var plan = await PlanRuleAsync(path, subject, ct, process).ConfigureAwait(false);
        if (process?.LandingCannot is not null || plan.Target.Length == 0) return plan;
        var full = Path.GetFullPath(path);
        var (workspace, repository) = context.OwnerOf(full);
        // Whether the branch already stands, so the review says the press moves it on rather than promising a new one.
        if (plan is { Form: LandingForm.Branch, Own: false } && Directory.Exists(full)
            && await CommitOfAsync(full, $"refs/heads/{plan.Target}", ct).ConfigureAwait(false) is { } standsAt)
        {
            // And what the press would refuse it for, said before it, where nothing else is said first.
            plan = plan with { Stands = true, Problem = plan.Problem ?? await StandingProblemAsync(full, repository, plan.Target, standsAt, subject, ct).ConfigureAwait(false) };
        }

        var (sync, _) = await syncs.JudgeAsync(full, repository, workspace, plan.Form == LandingForm.Branch, fetch: false, ct).ConfigureAwait(false);
        if (sync is null || !sync.Held.Replays) return plan;
        // Where the line only moved, a standing branch the press moves on grows from its own tip, never rewritten.
        if (sync.Held.HoldsNone && plan.Stands) return plan;
        if (plan.Own && await OnRemoteAsync(sync.Root, sync.Branch, ct).ConfigureAwait(false))
        {
            // A pushed branch of its own is never rewritten: where the line holds none of it, it lands as it stands, as before.
            if (sync.Held.HoldsNone) return plan;
            return plan with { LeftOut = sync.Held.LeftOut, Lands = sync.Held.Kept, Conflict = TreeLandSyncs.OwnOnRemote(sync).Conflict };
        }

        // A file of a line rule the owner accepted for integration is no conflict the press would refuse, as it settles it.
        var lines = await LineRules.AcceptedAsync(context.Config(), sync.Root, repository, sync.Onto, ct).ConfigureAwait(false);
        var conflicts = await LineHolds.TrialAsync(sync.Root, sync.Onto, sync.Held.Kept, ct, lines).ConfigureAwait(false);
        return plan with
        {
            LeftOut = sync.Held.LeftOut,
            Lands = sync.Held.Kept,
            Conflict = conflicts is null ? null : TreeLandSyncs.Conflicted(sync, conflicts).Conflict,
        };
    }

    /// <summary>
    /// The refusal a press would meet at a standing branch, from what is cheap to read, in the press's own words: a name entered that is somebody's branch, or <see cref="LandingAdvance.Standing"/>'s record, tip,
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

    /// <summary>What a press would do under the rule, before the judgement of what the line holds; an entry's Accept names its branch by it.</summary>
    internal async Task<LandingPlan> PlanRuleAsync(string path, LandingSubject subject, CancellationToken ct, IWorkplaceProcess? process)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = context.OwnerOf(full);
        // The rule it lands by, its access setup's landing composed in: the session is told it is accepted automatically
        // exactly where the look would land it.
        var landing = process is { Named: true } named ? named.Landing : Landing.Merge;
        // A named workflow's landing that cannot start says so before the press, as the press would be refused.
        if (process?.LandingCannot is not null)
        {
            return new(landing.Form, "", LandingSource.Default, null, process.LandingSays, landing.AutoAccept);
        }

        if (landing.Form == LandingForm.Branch)
        {
            // A landing names no plugin: a graph's `push` step pushes the branch.
            string? plugin = null;
            // A tree its agent moved onto a branch of its own lands as that branch stands, the one the pull request is from.
            if (Directory.Exists(full) && context.Holds(full) && await TreeFollows.OwnBranchOfAsync(full, ct).ConfigureAwait(false) is { } own)
            {
                return new(LandingForm.Branch, own, LandingSource.Default, plugin, subject.EnteredName is not null ? EnteredOwn(own) : plugin is null ? null : context.Plugins.Problem(plugin),
                    landing.AutoAccept)
                { Own = true };
            }

            // A conversation's for its work, never its opening, as the press names it.
            var name = BranchNamed(await ForWorkAsync(full, workspace, repository, landing, subject, ct).ConfigureAwait(false), landing, repository);
            // A name the press would refuse is said before it, where the plan keeps the sentence a press is refused with.
            var problem = subject.EnteredName is not null && !BranchName.IsValid(name) ? EnteredInvalid(name)
                : plugin is null ? null : context.Plugins.Problem(plugin);
            return new(LandingForm.Branch, name, LandingSource.Default, plugin, problem, landing.AutoAccept) { Squash = landing.Squash };
        }

        var (code, commonDir, _) = Directory.Exists(full)
            ? await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false)
            : (1, "", "");
        var line = code == 0
            ? (await context.LineAsync(Path.GetDirectoryName(commonDir.Trim())!, repository, workspace, ct).ConfigureAwait(false)).Branch
            : TreeGoneLines.Of(context, full, repository, workspace);
        if (line is null) return new(LandingForm.Merge, TreeGoneLines.Unnamed, LandingSource.Default, Problem: TreeGoneLines.Problem(repository), AutoAccept: landing.AutoAccept);
        return new(LandingForm.Merge, line, LandingSource.Default, Problem: subject.EnteredName is not null ? EnteredMerge : null, AutoAccept: landing.AutoAccept);
    }

    /// <summary>
    /// The subject a conversation's branch is named for: <see cref="LandingSubject.ForWork"/>
    /// with the oldest commit subject its work holds beyond the line, as the press's own log reads it. Git is asked only where the rule
    /// would read a conversation's words (<see cref="BranchPatterns.ReadsWords"/>), so a quest's plan, an entered name, an entry's branch
    /// and a pattern of ids alone start none.
    /// </summary>
    /// <param name="full">A tree of the repository the work is in; its HEAD is the work unless <paramref name="tip"/> names it.</param>
    /// <param name="tip">The work's tip where it is not that tree's HEAD: an entry's first part landed from a tree since gone.</param>
    internal async Task<LandingSubject> ForWorkAsync(
        string full, string workspace, string repository, Landing landing, LandingSubject subject, CancellationToken ct, string? tip = null, bool always = false)
    {
        if (!subject.NamedByOpening || subject.EnteredName is not null || subject.EntryBranch is not null || !(always || BranchPatterns.ReadsWords(landing))) return subject;
        var here = Directory.Exists(full) ? await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false) : null;
        if (here is not { Code: 0 }) return subject.ForWork(null);
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(here.CommonDir.Trim()))!;
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        if (line is null) return subject.ForWork(null);
        var against = await ComparableAsync(root, line, ct).ConfigureAwait(false) ?? line;
        var (code, log, _) = await WorkingTree.GitAsync(
            full, ["log", "--reverse", BranchPatterns.SubjectsFormat, $"{against}..{tip ?? "HEAD"}"], ct).ConfigureAwait(false);
        return subject.ForWork(code == 0 ? BranchPatterns.OldestSubject(log) : null);
    }

    /// <summary>
    /// An entry's subject named for its first part's work: read from that part's tree where it is here, else from the tip
    /// its landing recorded, in <paramref name="anyTree"/>, a tree of the same repository; so the press, the plan and each part's own
    /// Accept name the entry's one branch alike, whichever part's tree they are pressed in. Named by the run's <paramref name="process"/>'s
    /// <c>land</c>, as the plan and the press name it.
    /// </summary>
    internal async Task<LandingSubject> EntryForWorkAsync(
        LandingSubject entry, string? leadTree, string anyTree, CancellationToken ct, IWorkplaceProcess? process = null)
    {
        if (!entry.NamedByOpening) return entry;
        var lead = leadTree is { Length: > 0 } && Directory.Exists(leadTree) ? Path.GetFullPath(leadTree) : null;
        var full = lead ?? Path.GetFullPath(anyTree);
        var (workspace, repository) = context.OwnerOf(full);
        var landing = process is { Named: true } named ? named.Landing : Landing.Merge;
        if (lead is not null) return await ForWorkAsync(lead, workspace, repository, landing, entry, ct).ConfigureAwait(false);
        return context.Recorded.Landing(entry.Session) is { } landed
            ? await ForWorkAsync(full, workspace, repository, landing, entry, ct, landed.Tip).ConfigureAwait(false)
            : BranchPatterns.ReadsWords(landing) ? entry.ForWork(null) : entry;
    }
}
