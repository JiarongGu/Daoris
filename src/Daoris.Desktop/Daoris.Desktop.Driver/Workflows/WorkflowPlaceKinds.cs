using static Daoris.Driver.WorkflowOperations;

namespace Daoris.Driver;

/// <summary>
/// Choosing a workflow for departments whose places are of different kinds: a choice is refused where a department it reaches cannot run one
/// of its steps (<see cref="PlaceKindUnserved"/>), so a folder or a system never follows a graph that lands, pushes or rebases.
/// </summary>
internal static class WorkflowPlaceKinds
{
    /// <summary>
    /// Why choosing <paramref name="chosen"/> for <paramref name="repository"/> or the workspace <paramref name="workspace"/> is refused, each
    /// department it reaches that cannot run a step said with that step and its place; null where every one can, or where the registry is not read.
    /// </summary>
    public static async Task<string?> RefusalAsync(
        DriverConfig choices, string home, RegistryRead registry, string? chosen, string? repository, string? workspace, CancellationToken ct)
    {
        if (chosen is null || chosen == WorkflowSelection.Current || !WorkflowNamed.IsId(chosen)) return null;
        if (WorkflowPorts.Store(home).Load(chosen)?.Read.Versions is not [.., { Graph: { } graph } newest]) return null;
        var read = await registry(ct).ConfigureAwait(false);
        if (read.Checkouts is not { } checkouts) return null;

        IEnumerable<string> reached = repository is not null
            ? [repository.Trim()]
            : WorkflowUsing.InWorkspace(choices, checkouts, workspace!, except: null).Select(each => each.Repository);
        var refused = reached
            .Select(department => (Department: department, Problem: PlaceKindUnserved.Of(graph, read.KindOf(department), chosen, newest.Version)))
            .Where(each => each.Problem is not null)
            .Select(each => $"`{each.Department}` cannot follow it: {each.Problem!.Said}")
            .ToList();
        return refused.Count == 0 ? null
            : $"{string.Join(' ', refused)} The shipped *Work in place* runs in any department: `daoris place <department> --preset in-place`. Nothing was written.";
    }
}
