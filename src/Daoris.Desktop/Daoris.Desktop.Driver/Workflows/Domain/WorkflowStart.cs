namespace Daoris.Driver;

/// <summary>
/// One development route of the start node (FABRIC-PIPELINE1a, the pipeline design §1.3): the default where <paramref name="Kind"/> is
/// null, else a kind the workspace declares by its label and paths; the pipeline it names and the level that chose it.
/// </summary>
/// <param name="Workflow">A workflow's id, or <c>current</c> where nothing names one.</param>
/// <param name="Level">One of <see cref="WorkflowLevels"/>.</param>
/// <param name="Name">The named workflow's name where it reads here; null under Current, or where it does not read.</param>
public sealed record WorkflowRouteRow(string? Kind, string? Label, IReadOnlyList<string> Paths, string Workflow, string Level, string? Name);

/// <summary>
/// The start node as a Workflow tab draws it (FABRIC-PIPELINE1a, D192 point 2): the development routes in the order the choice table
/// keeps them, the landed route, its pipeline's name where it reads here, and the verification pipelines saved here, which Setup's
/// route row offers.
/// </summary>
public sealed record WorkflowStartNode(
    IReadOnlyList<WorkflowRouteRow> Routes, WorkflowLandedSelected Landed, string? LandedName, IReadOnlyList<(string Id, string Name)> Verifications);

/// <summary>
/// The start node read (FABRIC-PIPELINE1a, the pipeline design §1.2, §1.3): not a node of any version's graph, but the choice table D157
/// point 5 keeps, drawn first on the Workflow tab with each route wired to the pipeline it names. Read from the driver's file and the
/// workflows saved here alone; the gates at a line's tip are the caller's to read (<see cref="LandedGates"/>).
/// </summary>
public static class WorkflowStart
{
    /// <param name="repository">The repository, or null for a workspace's own start.</param>
    /// <param name="workspace">The repository's workspace, or null for none, read as the default workspace.</param>
    public static WorkflowStartNode Read(DriverConfig config, string? repository, string? workspace, IReadOnlyList<WorkflowFileFound> saved)
    {
        string? NameOf(string workflow) =>
            saved.FirstOrDefault(each => each.Id == workflow) is { Read.Problem: null } found ? found.Read.Name : null;
        WorkflowRouteRow Row(WorkflowSelected selected, string? kind) =>
            new(kind, selected.Label, selected.Paths, selected.Workflow, selected.Level, selected.Workflow == WorkflowSelection.Current ? null : NameOf(selected.Workflow));

        var routes = new List<WorkflowRouteRow> { Row(WorkflowSelection.Resolve(config, repository, workspace, null), null) };
        if (config.WorkspaceWorkflows.TryGetValue(RemoteTarget.Workspace(workspace), out var shared))
        {
            foreach (var (kind, _) in shared.Kinds)
            {
                routes.Add(Row(WorkflowSelection.Resolve(config, repository, workspace, new WorkflowTaskChoice(kind, null)), kind));
            }
        }

        var landed = WorkflowSelection.ResolveLanded(config, repository, workspace);
        var verifications = saved
            .Where(each => each.Read is { Problem: null, Verifies: true })
            .Select(each => (each.Id, each.Read.Name!))
            .OrderBy(each => each.Id, StringComparer.Ordinal)
            .ToList();
        return new WorkflowStartNode(routes, landed, landed.Workflow == WorkflowSelection.Current ? null : NameOf(landed.Workflow), verifications);
    }
}
