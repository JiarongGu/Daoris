namespace Daoris.Driver;

/// <summary>
/// A repository's verification runs as its line's read draws them (FABRIC-PIPELINE1d; the pipeline design §4.4): each run bound under the home's
/// <c>workflows/lines/</c>, newest first, drawn from its record as any graph run is, with the commit it judged, the branch it was asked of and its
/// verdict, each gate's evidence from its checks' newest results. Reads only; the service is never asked, since nothing of a line is a quest's.
/// </summary>
public static class LineRunReader
{
    /// <summary>How many of a repository's runs are read, newest first.</summary>
    public const int Shown = 10;

    /// <summary>The newest of <paramref name="repository"/>'s verification runs, or none with the sentence that says how one is asked.</summary>
    /// <param name="workspaceOf">The workspace the registry holds a repository in, or null where it holds none.</param>
    public static WorkflowRunRead Read(
        string repository, string home, DriverConfig config, Func<string, string?> workspaceOf, IWorkflowRunRecords records)
    {
        var lines = VerifyRuns.All(home)
            .Where(line => string.Equals(line.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(line => line.At)
            .ThenByDescending(line => line.Run, StringComparer.Ordinal)
            .Take(Shown)
            .ToList();
        if (lines.Count == 0)
        {
            return new WorkflowRunRead([], $"`{repository}` has no verification here yet: verify a branch or a commit on its Workflow tab, or `daoris gate verify {repository}`.")
            {
                Id = repository,
            };
        }

        var store = WorkflowPorts.Store(home);
        return new WorkflowRunRead([.. lines.Select(line => RunOf(line, home, config, workspaceOf(line.Repository), store, records))]);
    }

    /// <summary>One verification run, drawn from its record under the version it was bound to; a version that no longer reads is its problem.</summary>
    /// <param name="registered">The workspace the registry holds its repository in, or null.</param>
    public static WorkflowRun RunOf(VerifyRun line, string home, DriverConfig config, string? registered, IWorkflowStore store, IWorkflowRunRecords records)
    {
        var workspace = RemoteTarget.Workspace(registered);
        var process = WorkflowProcess.Declared(config, line.Repository, registered) with { Run = line.Run, Binding = line.Binding, Opinion = null };
        var current = new CurrentWorkflow([], [], line.Digest);
        var version = store.Load(line.Workflow)?.Read.Versions.FirstOrDefault(each => each.Version == line.Version);
        if (version is not { Graph: { } graph, Digest: { } digest } || !string.Equals(digest, line.Digest, StringComparison.Ordinal))
        {
            var problem = $"`{line.Workflow}` v{line.Version}, the version the verification {line.Run} was bound to, cannot be read here.";
            return new WorkflowRun(line.Repository, workspace, current, [], [])
            {
                Process = process with { Problem = problem }, Problem = problem, Line = Said(line, LineVerdicts.Unknown),
            };
        }

        var record = records.Record(RunUnits.IdOf(line.Run));
        var facts = new WorkflowRunFacts(line.Repository, current) { Process = process with { Graph = graph } };
        var run = WorkflowGraphRuns.Derive(
            new WorkflowGraphRunFacts(facts, line.Run, graph, record) { Failures = CheckResults.Failures(home, line.Run, graph, record) }, workspace);
        var checks = run.Steps.Where(step => step.Node == NodeKinds.Check).ToList();
        var verdict = LineVerdicts.Of(run.Steps);
        return run with
        {
            Line = Said(line, verdict) with
            {
                Gate = verdict == LineVerdicts.Failed ? checks.Select(step => step.Gate).FirstOrDefault(gate => gate is not null) : null,
                Why = verdict == LineVerdicts.Unknown ? checks.Select(step => step.Code).FirstOrDefault(code => code is not null) : null,
                Gates = [.. graph.Nodes.Where(node => node.Kind == NodeKinds.Check)
                    .Select(node => CheckResults.Newest(home, line.Run, record, node.Id)).OfType<VerifyResult>().SelectMany(result => result.Gates)],
            },
        };
    }

    private static WorkflowRunLine Said(VerifyRun line, string verdict) =>
        new(line.Run, line.Commit, line.Level, line.At, verdict) { Branch = line.Branch, Line = line.Line, Base = line.Base };
}
