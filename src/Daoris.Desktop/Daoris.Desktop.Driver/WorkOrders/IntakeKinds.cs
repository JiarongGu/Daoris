namespace Daoris.Driver;

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
            var (otherwise, mapped) = WorkOrdersWorkflows.Current.KindsOf(home, config, repository, workspace, [.. shared.Kinds.Select(kind => kind.Key)]);
            var lines = new List<IntakeKindLine>();
            foreach (var (kind, (name, lowers)) in shared.Kinds.Zip(mapped))
            {
                lines.Add(new IntakeKindLine(kind.Key, kind.Value.Label, kind.Value.Paths, name, lowers));
            }

            read.Add(new IntakeRepositoryKinds(repository, otherwise, lines));
        }

        return read;
    }
}
