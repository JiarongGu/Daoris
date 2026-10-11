namespace Daoris.Driver;

/// <summary>
/// One kind of work a workspace declares, as a repository in it follows it (WORKFLOW1i-b; the workflow design §4.3).
/// </summary>
/// <param name="Workflow">What a task of the kind follows there, as said: a workflow's id and version, or Current.</param>
/// <param name="Lowers">
/// What it asks less of the person than the repository's workflow with no kind does, each said as what it does
/// (<see cref="Involvement.Lowers"/>); empty where it lowers nothing, and null where its steps could not be read here.
/// </param>
public sealed record IntakeKindLine(string Kind, string? Label, IReadOnlyList<string> Paths, string Workflow, IReadOnlyList<string>? Lowers);

/// <summary>A repository's kinds: the workflow it follows with none, and each declared kind's against it.</summary>
public sealed record IntakeRepositoryKinds(string Repository, string Otherwise, IReadOnlyList<IntakeKindLine> Kinds);

/// <summary>
/// The kinds an intake may propose (WORKFLOW1i-b, the workflow design §4.3): read from the workspace's declarations and the
/// workflows each repository's choices map them to, so the room says, per repository, which kind lowers the person's part
/// there. The reading is the same table the gate uses (<see cref="Involvement"/>), never a second one; an intake only proposes,
/// and nothing here sets a kind (WORKFLOW1i2).
/// </summary>
public static class IntakeKinds
{
    /// <summary>
    /// Each repository's view of the workspace's kinds, in the repositories' order; empty where the workspace declares none.
    /// </summary>
    public static IReadOnlyList<IntakeRepositoryKinds> Read(
        string home, DriverConfig config, string workspace, IEnumerable<string> repositories)
    {
        if (!config.WorkspaceWorkflows.TryGetValue(RemoteTarget.Workspace(workspace), out var shared) || shared.Kinds.Count == 0) return [];

        var read = new List<IntakeRepositoryKinds>();
        foreach (var repository in repositories)
        {
            var (otherwise, without) = WorkflowProcesses.Otherwise(home, config, repository, workspace);
            var lines = new List<IntakeKindLine>();
            foreach (var kind in shared.Kinds)
            {
                var (name, steps) = WorkflowProcesses.Otherwise(home, config, repository, workspace, kind.Key);
                IReadOnlyList<string>? lowers = steps is null || without is null ? null : Involvement.Lowers(steps, without);
                lines.Add(new IntakeKindLine(kind.Key, kind.Value.Label, kind.Value.Paths, name, lowers));
            }

            read.Add(new IntakeRepositoryKinds(repository, otherwise, lines));
        }

        return read;
    }
}
