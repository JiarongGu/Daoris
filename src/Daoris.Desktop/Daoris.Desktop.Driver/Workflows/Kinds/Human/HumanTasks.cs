namespace Daoris.Driver;

/// <summary>A run waiting on a person's task (NODEKIT1 HUMAN): its <c>human</c> node active, with the presses it waits for.</summary>
/// <param name="Run">The run, as its binding names it: what the press is said on.</param>
/// <param name="Node">The <c>human</c> node's id in the run's graph.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Workspace">The run's workspace.</param>
/// <param name="Acts">The presses it waits for: <c>done</c>, or <c>approve</c> and <c>reject</c>.</param>
/// <param name="Work">The run's newest work quest here: what the row's door reads the run by; null where it has none.</param>
/// <param name="Since">When the node became active: since when the task waits; null on a record written before it was clocked.</param>
public sealed record HumanTask(string Run, string Node, string Repository, string Workspace, IReadOnlyList<string> Acts, string? Work, DateTimeOffset? Since = null);

/// <summary>
/// The runs waiting on a person's task, judged from the runs the runtime last governed and each run's node record read now. **Read, never
/// stored**, as <see cref="RequestHolds"/> are: a node whose press waits to be applied, or that moved on, waits on no one. Pure over its inputs.
/// </summary>
public static class HumanTasks
{
    /// <summary>Each governed run's active <c>human</c> node with no word of the person's pending on it, by run and node.</summary>
    /// <param name="governed">The units the last look governed: a run's carry its scope, and a unit's none, whose task waits ready on no press.</param>
    /// <param name="record">A unit's node record, or null where it does not read.</param>
    public static IReadOnlyList<HumanTask> Of(IEnumerable<GovernedUnit> governed, Func<string, NodeRecord?> record)
    {
        var tasks = new List<HumanTask>();
        foreach (var each in governed)
        {
            if (each.Scope is not { } scope || record(each.Unit.Id) is not { } kept) continue;
            foreach (var node in each.Graph.Nodes.Where(node => NodeKit.Of(node.Kind) is HumanNode))
            {
                var status = WorkPerformer.StatusOf(kept, node.Id);
                if (status.State != NodeStates.Active || kept.Pending.Any(word => word.Node == node.Id)) continue;
                tasks.Add(new HumanTask(
                    each.Run, node.Id, each.Unit.Owner.Repository ?? "", each.Unit.Owner.Workspace, HumanNode.Presses(node), scope.Quests.LastOrDefault()?.Id,
                    status.Since));
            }
        }

        return [.. tasks.OrderBy(task => task.Run, StringComparer.Ordinal).ThenBy(task => task.Node, StringComparer.Ordinal)];
    }
}
