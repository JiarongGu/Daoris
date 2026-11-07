using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An act as its door hands it over, judged: its kind, the press, the repository and the checkout, the plan it showed, its effect steps
/// (<see cref="ActSteps"/>), the index entries whose blobs its own ref holds, and the fresh reads it re-judges under the lock.
/// </summary>
public sealed record ActPlan(string Kind, string Press, string Repository, string Checkout, IReadOnlyList<ActStepPlan> Steps)
{
    /// <summary>The act's id; null draws one.</summary>
    public string? Id { get; init; }

    /// <summary>What the plan showed, as JSON: the refs and ids, the touched entries and their digests, the hooks, the switch.</summary>
    public string Plan { get; init; } = "{}";

    /// <summary>The press's Confirm, as its door names it.</summary>
    public string? Confirmed { get; init; }

    /// <summary>The index entries whose blobs the act's own ref holds until its record expires.</summary>
    public IReadOnlyList<ActEntry> Keep { get; init; } = [];

    /// <summary>The fresh reads under the lock, before any effect: why the act is refused, or null where they equal the plan.</summary>
    public Func<CancellationToken, Task<string?>>? Judge { get; init; }

    /// <summary>
    /// The objects an act writes before its record (a history edit's commits), made under the lock once
    /// <see cref="Judge"/> holds: the steps to journal in place of <see cref="Steps"/>, or why the act is refused with only objects written.
    /// </summary>
    public Func<CancellationToken, Task<(IReadOnlyList<ActStepPlan>? Steps, string? Refusal)>>? Make { get; init; }

    /// <summary>
    /// Read under the lock after each effect step ended, with its ordinal: the words that stop the act there, or null to go on. It has put right
    /// by its own rule whatever it stops for (a deleted branch a tree checked out meanwhile is made again), and runs under no cancellation.
    /// </summary>
    public Func<int, Task<string?>>? After { get; init; }
}

/// <summary>What an act's run answered: the act and its outcome (<see cref="ActOutcomes"/>), or that it was refused before anything was written, in words.</summary>
public sealed record ActRun(string? Act, string? Outcome, string Said, bool Refused)
{
    /// <summary>What the act's door adds in plain words beside the record's sentence (a commit's hook's rewrite, a hook's refusal); null where it has none.</summary>
    public string? Notes { get; init; }
}

/// <summary>The boundaries a test's seam is called at, by the step's ordinal (none for <see cref="Prepared"/>).</summary>
public static class ActSeams
{
    /// <summary>The recovery written and the job made, before the first step's start.</summary>
    public const string Prepared = "prepared";

    /// <summary>A step's start written with its deadline, before its process or git's lock.</summary>
    public const string BeforeLaunch = "before-launch";

    /// <summary>A step's git made, suspended and held in the job, before its id is written.</summary>
    public const string BeforePid = "before-pid";

    /// <summary>A step's git let go, before its exit is awaited.</summary>
    public const string AfterLaunch = "after-launch";
}

/// <summary>What a run's launches are told: its seam, how its job's processes are ended past a bound, and how long their end is awaited.</summary>
internal sealed record ActRunsLimits(Func<string, int, Task>? Seam, Action<ProcessJob>? Ending, TimeSpan ExitBound);

/// <summary>
/// An act run: judged, prepared, each effect step started and ended, the postconditions read and the
/// record closed, all under the checkout lock held alone.
/// </summary>
/// <remarks>
/// <para><b>Under the lock, before any effect</b>: the checkout's open acts reconciled first (<see cref="ActReconciler"/>), git's lock files read
/// and refused where they stand, the plan's own fresh reads. Then the record is written <i>judged</i>; the act ref's commit (objects only) is
/// made before it, so its id is journaled before the ref is.</para>
/// <para><b>Contained, bounded.</b> Each effect step's start is written with its deadline (its start plus <see cref="GitBounds"/>); its git is
/// started suspended and joined to the act's job, named for the act (<see cref="ActJournal.JobOf"/>), its id written, and only then let go.
/// Past its bound the job's processes are ended and awaited empty, else said unended, the act left open for a reconciler. No exit code proves
/// an effect: the postconditions do.</para>
/// <para><b>Cancellation stops an act only before its first effect step</b>: an act cancelled there closes <i>not done</i>; once a step has
/// begun, each runs under no cancellation and its bound, so a closed terminal or a Ctrl-C ends only the watch. An effect that throws (a crash
/// is one) leaves what it wrote, as a dead process would, for the reconciler.</para>
/// </remarks>
public sealed class ActRuns(ActJournal journal)
{
    /// <summary>How long an act's own ref holds its index's blobs after its record closes (thirty days, provisional).</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromDays(30);

    // The acts this process runs now: its own run's reconciler never takes one up while it runs.
    private static readonly ConcurrentDictionary<string, byte> s_running = new(StringComparer.Ordinal);

    /// <summary>The test seam: called at each boundary (<see cref="ActSeams"/>) with the step's ordinal. Null in production.</summary>
    internal Func<string, int, Task>? Seam { get; set; }

    /// <summary>The test seam for a step's bound, by its kind; null for its kind's own (<see cref="ActSteps.BoundOf"/>).</summary>
    internal Func<string, TimeSpan>? Bound { get; set; }

    /// <summary>How long a step's processes are awaited once ended past its bound, before they are said unended.</summary>
    internal TimeSpan ExitBound { get; set; } = ChildProcess.ExitBound;

    /// <summary>The test seam for ending a job past a step's bound; null ends it (<see cref="ProcessJob.Terminate"/>).</summary>
    internal Action<ProcessJob>? Ending { get; set; }

    /// <summary>The journal this run writes.</summary>
    internal ActJournal Journal => journal;

    /// <summary>The act this run holds now, while it runs.</summary>
    internal string? Running { get; private set; }

    /// <summary>Whether this process runs <paramref name="act"/> now.</summary>
    internal static bool RunsHere(string act) => s_running.ContainsKey(act);

    /// <summary>The checkout held alone for an act or its reconciliation, or null where another holds it.</summary>
    internal static TreeLock? Hold(string home, string checkout, string repository) => TreeLock.TryWriting(home, checkout, repository, TreeLock.Acting);

    /// <summary>Run <paramref name="plan"/> to its close, or refuse it before anything is written.</summary>
    /// <param name="ct">The watch: it cancels the act before its first effect step, and after that only the watch.</param>
    public async Task<ActRun> RunAsync(ActPlan plan, CancellationToken ct)
    {
        // A flow that holds the checkout alone already (a tidy, a removal) runs its act under that hold, since a second open in
        // one process is refused; any other takes it here.
        var outer = TreeLock.HeldHere(journal.Home, plan.Checkout);
        using var taken = outer is null ? Hold(journal.Home, plan.Checkout, plan.Repository) : null;
        var held = outer ?? taken;
        if (held is null) return Refused($"{TreeLock.Held(plan.Repository, TreeLock.HolderOf(journal.Home, plan.Checkout))}: nothing was changed.");

        var reconciler = new ActReconciler(journal) { ExitBound = ExitBound, Ending = Ending };
        if (await reconciler.UnderLockAsync(plan.Checkout, ct).ConfigureAwait(false) is { } open) return Refused($"{open.Said}: nothing was changed.");
        if (await LayoutAsync(plan.Checkout).ConfigureAwait(false) is not { } layout) return Refused($"git could not read `{plan.Repository}`'s checkout, so nothing was changed.");
        if (ActLocks.Standing(layout.GitDir, layout.CommonDir, plan.Steps.SelectMany(RefsOf)) is [var standing, ..]) return Refused(ActLocks.Stale(standing, plan.Repository));
        if (plan.Judge is { } judge && await judge(ct).ConfigureAwait(false) is { } refusal) return Refused(refusal);
        if (plan.Make is { } make)
        {
            var (steps, unmade) = await make(ct).ConfigureAwait(false);
            if (steps is null) return Refused(unmade ?? "its objects were not made, so nothing was changed.");
            plan = plan with { Steps = steps };
            if (ActLocks.Standing(layout.GitDir, layout.CommonDir, steps.SelectMany(RefsOf)) is [var late, ..]) return Refused(ActLocks.Stale(late, plan.Repository));
        }

        ct.ThrowIfCancellationRequested();

        var id = plan.Id ?? Guid.NewGuid().ToString("N");
        string? kept = null;
        if (plan.Keep.Count > 0 && (kept = await KeepAsync(plan.Checkout, id, plan.Keep).ConfigureAwait(false)) is null)
        {
            return Refused($"git could not keep the index entries' blobs for act {id}, so nothing was changed.");
        }

        ActClaim claim;
        try
        {
            claim = journal.Open(new ActOpening(id, plan.Kind, plan.Press, plan.Repository, plan.Checkout, TreeLock.CommonDirOf(plan.Checkout), plan.Plan, plan.Steps)
            {
                Confirmed = plan.Confirmed, Recovery = Recovery(plan, id, kept),
            });
        }
        catch (ActRefusedException refused)
        {
            return Refused($"{refused.Why}: nothing was changed.");
        }

        s_running[id] = 0;
        Running = id;
        var (job, _) = ProcessJob.CreateNamed(ActJournal.JobOf(id));
        try
        {
            if (job is null) return await CloseAsync(reconciler, claim, plan, "no job could hold its steps' processes, so none ran").ConfigureAwait(false);
            if (kept is not null)
            {
                var made = await new RefTransaction().Create(ActRefs.Of(id), kept).RunAsync(plan.Checkout, CancellationToken.None).ConfigureAwait(false);
                if (!made.Done) return await CloseAsync(reconciler, claim, plan, $"its own ref was not made ({made.Said}), so no step ran").ConfigureAwait(false);
            }

            journal.Prepared(claim);
            if (Seam is { } prepared) await prepared(ActSeams.Prepared, -1).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return await CloseAsync(reconciler, claim, plan, "it was cancelled before its first effect step").ConfigureAwait(false);

            var limits = new ActRunsLimits(Seam, Ending, ExitBound);
            var launch = new ActLaunch(journal, claim, job, plan.Checkout, plan.Repository, limits);
            string? stoppedWhy = null;
            foreach (var step in journal.Steps(id))
            {
                var bound = Bound?.Invoke(step.Kind) ?? ActSteps.BoundOf(step.Kind);
                var begun = journal.Begin(claim, step.Ordinal, journal.Clock.GetUtcNow() + bound);
                if (Seam is { } beforeLaunch) await beforeLaunch(ActSeams.BeforeLaunch, step.Ordinal).ConfigureAwait(false);
                var end = await ActSteps.EffectAsync(held, launch, begun).ConfigureAwait(false);
                var ended = journal.End(claim, step.Ordinal, end.Exit, end.Unended, end.Said);
                if (end.Unended)
                {
                    return new ActRun(id, ActOutcomes.Unknown,
                        $"act {id}'s step {step.Ordinal} did not end when stopped past its bound (pid {ended.Pid}): Daoris could not tell whether this finished; "
                        + "nothing was retried, and the act stays open until its processes are gone", false);
                }

                if (end.Stops) break;
                if (plan.After is { } after && await after(step.Ordinal).ConfigureAwait(false) is { } stopped)
                {
                    stoppedWhy = stopped;
                    break;
                }
            }

            return await CloseAsync(reconciler, claim, plan, stoppedWhy).ConfigureAwait(false);
        }
        finally
        {
            job?.Dispose();
            s_running.TryRemove(id, out _);
            Running = null;
        }
    }

    /// <summary>The postconditions read and the record closed, or held open where git could not say.</summary>
    private static async Task<ActRun> CloseAsync(ActReconciler reconciler, ActClaim claim, ActPlan plan, string? why)
    {
        var (outcome, said) = await reconciler.CloseAsync(claim, plan.Checkout, why).ConfigureAwait(false);
        return new ActRun(claim.Act, outcome, said, false);
    }

    private static ActRun Refused(string said) => new(null, null, said, true);

    /// <summary>The checkout's git directory and common directory, read fresh; null where git could not say.</summary>
    private static async Task<(string GitDir, string CommonDir)?> LayoutAsync(string checkout)
    {
        var (code, said, _) = await WorkingTree.GitAsync(
            checkout, ["rev-parse", "--absolute-git-dir", "--git-common-dir"], environment: null, GitBounds.Question, CancellationToken.None).ConfigureAwait(false);
        var lines = said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (code != 0 || lines.Length < 2) return null;
        return (Path.GetFullPath(lines[0]), Path.GetFullPath(Path.Combine(checkout, lines[1])));
    }

    /// <summary>The refs a step moves, whose lock files are read before it.</summary>
    private static IEnumerable<string> RefsOf(ActStepPlan step)
    {
        // A commit moves the branch HEAD names (none when detached).
        if (step.Kind == ActStepKinds.Commit)
        {
            return JsonNode.Parse(step.Postconditions) is JsonObject read && read.TryGetPropertyValue("branch", out var named) && named is JsonValue value
                && value.TryGetValue<string>(out var full) ? [full] : [];
        }

        // A stash Save or Drop moves the stash ref every linked tree shares; an Apply only reads it.
        if (step.Kind == ActStepKinds.Stash) return JsonNode.Parse(step.Postconditions)?["mode"]?.GetValue<string>() == "apply" ? [] : ["refs/stash"];
        return step.Kind != ActStepKinds.Ref || JsonNode.Parse(step.Postconditions)?["refs"] is not JsonArray refs
            ? []
            : refs.Select(each => each?["ref"]?.GetValue<string>()).OfType<string>();
    }

    /// <summary>
    /// A commit, objects only, whose tree holds <paramref name="entries"/>' blobs, each by its place, for the act's own ref to hold; null where
    /// git could not make it.
    /// </summary>
    private static async Task<string?> KeepAsync(string root, string act, IReadOnlyList<ActEntry> entries)
    {
        var input = new StringBuilder();
        var at = 0;
        foreach (var entry in entries.Where(entry => entry.Mode is "100644" or "100755" or "120000")) input.Append($"{entry.Mode} blob {entry.Id}\t{at++}\n");
        var (code, tree, _) = await WorkingTree.GitAsync(root, ["mktree"], input.ToString(), CancellationToken.None).ConfigureAwait(false);
        if (code != 0) return null;
        var who = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = "Daoris", ["GIT_AUTHOR_EMAIL"] = "daoris@localhost", ["GIT_COMMITTER_NAME"] = "Daoris", ["GIT_COMMITTER_EMAIL"] = "daoris@localhost",
        };
        var (made, commit, _) = await WorkingTree.GitAsync(
            root, ["commit-tree", tree.Trim(), "-m", $"Daoris act {act}: the index entries it touched, kept until its record expires"], who, GitBounds.Question,
            CancellationToken.None).ConfigureAwait(false);
        return made == 0 && WorkingTree.IsCommitId(commit.Trim()) ? commit.Trim() : null;
    }

    /// <summary>The recovery as the journal keeps it: the act's own ref at its id, the refs as they stood, the sections and the entries as read.</summary>
    private static string Recovery(ActPlan plan, string act, string? kept)
    {
        var recovery = new JsonObject
        {
            ["actRef"] = kept is null ? null : new JsonObject { ["ref"] = ActRefs.Of(act), ["id"] = kept },
            ["steps"] = new JsonArray([.. plan.Steps.Select(step => (JsonNode)new JsonObject { ["kind"] = step.Kind, ["as"] = JsonNode.Parse(step.Postconditions) })]),
        };
        return recovery.ToJsonString();
    }
}
