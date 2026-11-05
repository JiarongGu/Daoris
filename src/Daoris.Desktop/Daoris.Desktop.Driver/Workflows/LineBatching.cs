namespace Daoris.Driver;

/// <summary>A line as a look read it: its repository and workspace, the checkout its tip was read in, the line's branch and its tip.</summary>
public sealed record LineHead(string Repository, string Workspace, string Root, string Line, string Tip);

/// <summary>
/// The landed route's batching (the pipeline design §4.3): one run per line at a time, bound at the newest tip once the line
/// moved past its newest run and no run of it is in flight, so every landing since the base is in it and what lands meanwhile is the next
/// batch's. A run, once bound, is never superseded. The line keeps no record of its own: its base, its run in flight and its newest verdict are
/// read from the runs bound on it under <c>lines/</c>. A person's run is never counted.
/// </summary>
public static class LineBatching
{
    /// <summary>Whether any place's landed route is set: a home with none reads no registry and no line for it.</summary>
    public static bool Armed(DriverConfig config) =>
        config.Workflows.Values.Any(choice => choice.Landed is not null) || config.WorkspaceWorkflows.Values.Any(choice => choice.Landed is not null);

    /// <summary>The runs a route bound on <paramref name="line"/> of <paramref name="repository"/>, newest first.</summary>
    public static IReadOnlyList<VerifyRun> On(string home, string repository, string line) =>
    [
        .. VerifyRuns.All(home)
            .Where(run => run.Level != VerifyRuns.Person && string.Equals(run.Repository, repository, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(run.Line, line, StringComparison.Ordinal))
            .OrderByDescending(run => run.At)
            .ThenByDescending(run => run.Run, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Where a run settled (§4.3: each check reached, failed, or stopped by the person): <see cref="LineVerdicts.Failed"/> where one failed,
    /// <see cref="LineVerdicts.Verified"/> where all reached, else <see cref="LineVerdicts.NotNow"/>; null while it is in flight, waiting, running
    /// or held unknown for the person. A run with no record yet, or whose version no longer names its checks, is in flight.
    /// </summary>
    public static string? Settled(NodeRecord? record, IReadOnlyCollection<string> checks)
    {
        if (record is null || checks.Count == 0) return null;
        var states = checks.Select(node => record.Statuses.GetValueOrDefault(node)?.State).ToList();
        if (!states.All(state => state is NodeStates.Reached or NodeStates.Failed or NodeStates.Stopped)) return null;
        if (states.Any(state => state == NodeStates.Failed)) return LineVerdicts.Failed;
        return states.All(state => state == NodeStates.Reached) ? LineVerdicts.Verified : LineVerdicts.NotNow;
    }

    /// <summary>
    /// The run <paramref name="head"/>'s line owes now under <paramref name="route"/>, or null: none where the route names no verification
    /// pipeline, no gates are accepted or no version reads; none while a run of the line is in flight (an unknown holds it for the person), or
    /// where its newest run was bound at this tip, whatever it came to. Its base is the tip of the newest run with a verdict, so a run the person
    /// put off (<i>Not now</i>) is taken into the next batch; none before any verdict, the line's first verification.
    /// </summary>
    public static VerifyRun? Owed(string home, LineHead head, WorkflowLandedSelected route, NodeRecordFiles records, DateTimeOffset now)
    {
        if (route.Workflow == WorkflowSelection.Current || route.Gates is not { } accepted) return null;
        var files = new WorkflowFiles(home);
        var version = files.Load(route.Workflow)?.Read.Versions.LastOrDefault(each => each.Problem is null && each.Graph is not null && each.Digest is not null);
        if (version is null) return null;

        var runs = On(home, head.Repository, head.Line);
        if (runs.Count > 0 && string.Equals(runs[0].Commit, head.Tip, StringComparison.OrdinalIgnoreCase)) return null;
        string? from = null;
        foreach (var run in runs)
        {
            var settled = Settled(records.Read(RunUnits.IdOf(run.Run)), ChecksOf(files, run));
            if (settled is null) return null;
            if (from is null && settled != LineVerdicts.NotNow) from = run.Commit;
        }

        return new VerifyRun(VerifyRuns.NewId(), head.Repository, head.Workspace, head.Root, route.Workflow, version.Version, version.Digest!, head.Tip, route.Level, now)
        {
            Accepted = accepted, Line = head.Line, Base = from,
        };
    }

    /// <summary>
    /// One look's batching: each registered repository with a checkout here whose landed route names a verification pipeline has its line's tip
    /// read (one ref read) and the run it owes bound under <c>lines/</c>. Returns what it bound, as the look's report says it.
    /// </summary>
    public static async Task<IReadOnlyList<string>> BindAsync(
        string home, DriverConfig config, IReadOnlyList<RepoView> registry, NodeRecordFiles records, DateTimeOffset now, CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var repo in registry.Where(each => each.Root is { Length: > 0 }))
        {
            var route = WorkflowSelection.ResolveLanded(config, repo.Repository, repo.Workspace);
            if (route.Workflow == WorkflowSelection.Current || route.Gates is null) continue;
            var line = (await CanonicalLine.ResolveAsync(repo.Root!, repo.Repository, repo.Workspace, config, ct).ConfigureAwait(false)).Branch;
            if (line is not { Length: > 0 } || await TipAsync(repo.Root!, line, ct).ConfigureAwait(false) is not { } tip) continue;
            if (Owed(home, new LineHead(repo.Repository, repo.Workspace, repo.Root!, line, tip), route, records, now) is not { } run) continue;
            VerifyRuns.Bind(home, run);
            said.Add($"work  {repo.Repository}'s line {line}: run {run.Run} verifies it at {TextBounds.ShortCommit(tip, 7)}, "
                     + (run.Base is { } from ? $"its batch since {TextBounds.ShortCommit(from, 7)}." : "its first verification."));
        }

        return said;
    }

    /// <summary>The line's tip in <paramref name="root"/>, or null where its branch names no commit there.</summary>
    public static async Task<string?> TipAsync(string root, string line, CancellationToken ct)
    {
        var (code, sha, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        var tip = sha.Trim();
        return code == 0 && WorkingTree.IsCommitId(tip) ? tip : null;
    }

    /// <summary>The check nodes of the version a run is bound to; none where it no longer reads.</summary>
    private static IReadOnlyList<string> ChecksOf(WorkflowFiles files, VerifyRun run) =>
        files.Load(run.Workflow)?.Read.Versions.FirstOrDefault(each => each.Version == run.Version) is { Graph: { } graph }
            ? [.. graph.Nodes.Where(node => node.Kind == NodeKinds.Check).Select(node => node.Id)]
            : [];
}
