using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What work orders ask of Workflows, installed when the driver loads (<see cref="IWorkOrdersWorkflows"/>).</summary>
internal sealed class WorkOrdersWorkflowsOperations : IWorkOrdersWorkflows
{
    [ModuleInitializer]
    internal static void Install() => WorkOrdersWorkflows.Current = new WorkOrdersWorkflowsOperations();

    public IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Obligations(string home, DateTimeOffset now) => WorkflowObligations.Read(home, now);

    public IReadOnlySet<string> Owing(string home) => WorkflowObligations.Owing(home);

    public string? Unusable(string home, string workflow)
    {
        if (WorkflowStore.Load(home, workflow) is not { } found)
        {
            return $"No workflow `{workflow}` is saved here: `daoris driver workflow list` names them. Nothing was kept.";
        }

        if (found.Read.Problem is { } unread) return $"`{workflow}` cannot be read: {unread} Nothing was kept.";
        var newest = found.Read.Versions[^1];
        return newest.Steps is null && newest.Graph is null
            ? $"`{workflow}` v{newest.Version} cannot be read here: {newest.Problem} Nothing was kept."
            : null;
    }

    public (string Otherwise, IReadOnlyList<(string Name, IReadOnlyList<string>? Lowers)> Kinds) KindsOf(
        string home, DriverConfig config, string repository, string workspace, IReadOnlyList<string> kinds)
    {
        var (otherwise, without) = WorkflowProcesses.Otherwise(home, config, repository, workspace);
        var mapped = new List<(string Name, IReadOnlyList<string>? Lowers)>();
        foreach (var kind in kinds)
        {
            var (name, steps) = WorkflowProcesses.Otherwise(home, config, repository, workspace, kind);
            mapped.Add((name, steps is null || without is null ? null : Involvement.Lowers(steps, without)));
        }

        return (otherwise, mapped);
    }
}
