namespace Daoris.Driver;

/// <summary>A conflicted file of a plan, and what would settle it (<see cref="IntegrationPlanSettles"/>).</summary>
public sealed record IntegrationPlanConflict(string Path, string Settled);

/// <summary>How a plan's conflicted file would be settled at the run's resolve (§5.2 step 4), or that it is a conflict of meaning (step 5).</summary>
public static class IntegrationPlanSettles
{
    /// <summary>A declared row: the backlog, a row table or a line rule, settled by the merge base or its writer.</summary>
    public const string Declared = "declared";

    /// <summary>A generated file, which takes the candidate's copy and is written again by its accepted writer.</summary>
    public const string Generated = "generated";

    /// <summary>No rule settles it: the member goes by the line's <c>conflicts</c>.</summary>
    public const string Meaning = "meaning";
}

/// <summary>
/// What <c>daoris integrate &lt;repository&gt; --plan &lt;branch&gt;</c> previews (INTEGRATE1h; the integration design §5.8, §6.3 point 3):
/// the branch merged onto its line's tip in git's objects alone, the commits the line lacks, the files the branch changes, and each conflict
/// with what would settle it. Null fields are what git could not say.
/// </summary>
public sealed record IntegrationPlan(string Repository, string Branch, string Line)
{
    public string? LineTip { get; init; }

    public string? BranchTip { get; init; }

    /// <summary>The branch's commits the line does not hold, newest first, each <c>&lt;short&gt; &lt;subject&gt;</c>, at most <see cref="IntegrationPlans.Most"/>.</summary>
    public IReadOnlyList<string> Commits { get; init; } = [];

    /// <summary>The files the branch changes since it left the line.</summary>
    public IReadOnlyList<string> Changed { get; init; } = [];

    public IReadOnlyList<IntegrationPlanConflict> Conflicts { get; init; } = [];

    /// <summary>The merged tree is the line's own: the line holds the branch's work already.</summary>
    public bool Contained { get; init; }

    /// <summary>Why no preview was made; null where one was.</summary>
    public string? Problem { get; init; }
}

/// <summary>
/// The plan's read (INTEGRATE1h): git's <c>merge-tree --write-tree</c> of the branch onto the line's tip, its objects written into a quarantine
/// folder the caller names and never into the repository (git's own <c>GIT_OBJECT_DIRECTORY</c> over the repository's objects as an alternate),
/// the folder removed after. Nothing else is written: no ref, no index, no working file, no object of the repository's.
/// </summary>
public static class IntegrationPlans
{
    /// <summary>The most commits a plan lists.</summary>
    public const int Most = 20;

    /// <summary>The plan of <paramref name="branch"/> onto <paramref name="line"/> in <paramref name="root"/>, its objects quarantined under <paramref name="scratch"/>.</summary>
    public static async Task<IntegrationPlan> PlanAsync(string repository, string root, string line, string branch, string scratch, CancellationToken ct = default)
    {
        var plan = new IntegrationPlan(repository, branch, line);
        var lineTip = await TreeRefs.CommitOfAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false);
        if (lineTip is null) return plan with { Problem = $"`{repository}` has no branch `{line}` here, so there is no line to preview onto." };
        var branchTip = await TreeRefs.CommitOfAsync(root, branch, ct).ConfigureAwait(false);
        if (branchTip is null) return plan with { LineTip = lineTip, Problem = $"`{branch}` is no branch or commit in `{repository}`." };
        plan = plan with { LineTip = lineTip, BranchTip = branchTip };

        var (logged, log, _) = await WorkingTree.GitAsync(root, ["log", $"--max-count={Most}", "--format=%h %s", $"{lineTip}..{branchTip}"], ct).ConfigureAwait(false);
        var (diffed, diff, _) = await WorkingTree.GitAsync(
            root, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", $"{lineTip}...{branchTip}"], ct).ConfigureAwait(false);
        plan = plan with { Commits = logged == 0 ? Lines(log) : [], Changed = diffed == 0 ? Lines(diff) : [] };

        var (common, commonOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (common != 0 || commonOut.Trim().Length == 0) return plan with { Problem = "git could not name the repository's objects, so no merge was previewed." };
        var quarantine = Path.Combine(scratch, Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(quarantine);
        try
        {
            var objects = Path.Combine(commonOut.Trim(), "objects");
            var quarantined = new Dictionary<string, string> { ["GIT_OBJECT_DIRECTORY"] = quarantine, ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = objects };
            var (merged, output, error) = await WorkingTree.GitAsync(
                root, ["-c", "core.quotePath=false", "merge-tree", "--write-tree", "--name-only", "--no-messages", lineTip, branchTip], quarantined, timeout: null, ct).ConfigureAwait(false);
            if (merged is not (0 or 1)) return plan with { Problem = $"git could not merge `{branch}` onto `{line}` in its objects ({ContentHolds.Said(error)})." };
            var said = Lines(output);
            if (said.Count == 0) return plan with { Problem = "git's merge named no tree." };
            var (treed, lineTree, _) = await WorkingTree.GitAsync(root, ["rev-parse", $"{lineTip}^{{tree}}"], ct).ConfigureAwait(false);
            var conflicted = said.Skip(1).Distinct(StringComparer.Ordinal).ToList();
            return plan with
            {
                Contained = merged == 0 && treed == 0 && string.Equals(lineTree.Trim(), said[0], StringComparison.OrdinalIgnoreCase),
                Conflicts = await SettlesAsync(root, lineTip, conflicted, ct).ConfigureAwait(false),
            };
        }
        finally
        {
            try
            {
                // git writes its objects read-only, which a delete refuses until the mark is lifted; the walk never follows a link out.
                var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
                foreach (var file in Directory.EnumerateFiles(quarantine, "*", walk))
                {
                    var attributes = File.GetAttributes(file);
                    if (attributes.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }

                Directory.Delete(quarantine, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A quarantine left behind holds only objects the repository never names; the next plan's folder is its own.
            }
        }
    }

    /// <summary>The lines that say <paramref name="plan"/>.</summary>
    public static IReadOnlyList<string> Said(IntegrationPlan plan)
    {
        var said = new List<string> { $"integrate: `{plan.Repository}` --plan `{plan.Branch}` onto `{plan.Line}`, in git's objects alone (nothing is written)" };
        if (plan.Problem is { } problem)
        {
            said.Add($"  {problem}");
            return said;
        }

        said.Add($"  {Short(plan.BranchTip)} onto {Short(plan.LineTip)}");
        if (plan.Contained) said.Add("  the line holds this branch's work already: nothing would be integrated.");
        if (plan.Commits.Count > 0)
        {
            said.Add($"  commits the line does not hold ({plan.Commits.Count}{(plan.Commits.Count == Most ? " or more" : "")}):");
            said.AddRange(plan.Commits.Select(commit => $"    {commit}"));
        }

        if (plan.Changed.Count > 0) said.Add($"  files it changes ({plan.Changed.Count}): {string.Join(", ", plan.Changed.Take(Most))}{(plan.Changed.Count > Most ? ", …" : "")}");
        if (plan.Conflicts.Count == 0)
        {
            if (!plan.Contained) said.Add("  it merges with no conflict.");
            return said;
        }

        said.Add("  conflicts:");
        said.AddRange(plan.Conflicts.Select(each => each.Settled switch
        {
            IntegrationPlanSettles.Declared => $"    {each.Path}: a declared row, settled by the merge base or its rule",
            IntegrationPlanSettles.Generated => $"    {each.Path}: a generated file, taking the line's copy, written again where its writer is accepted",
            _ => $"    {each.Path}: a conflict of meaning, which goes by the line's `conflicts`",
        }));
        return said;
    }

    /// <summary>What would settle each conflicted file, by the rules the line's tip declares: never by any the branch brings.</summary>
    private static async Task<IReadOnlyList<IntegrationPlanConflict>> SettlesAsync(string root, string lineTip, IReadOnlyList<string> conflicted, CancellationToken ct)
    {
        if (conflicted.Count == 0) return [];
        var declared = await new BacklogMerge.Stops(lineTip).DeclaredAsync(root, ct).ConfigureAwait(false);
        var marked = await GeneratedPaths.MarkedAsync(root, [.. conflicted.Where(path => !declared.Settles(path))], ct, lineTip).ConfigureAwait(false)
                     ?? new HashSet<string>(StringComparer.Ordinal);
        return
        [
            .. conflicted.Select(path => new IntegrationPlanConflict(
                path, declared.Settles(path) ? IntegrationPlanSettles.Declared : marked.Contains(path) ? IntegrationPlanSettles.Generated : IntegrationPlanSettles.Meaning)),
        ];
    }

    private static List<string> Lines(string output) =>
        [.. output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    private static string Short(string? commit) => commit is { Length: > 7 } ? commit[..7] : commit ?? "?";
}
