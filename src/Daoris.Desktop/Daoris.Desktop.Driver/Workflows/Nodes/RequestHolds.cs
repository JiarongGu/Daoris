namespace Daoris.Driver;

/// <summary>
/// A run held at a declined request (INTEGRATE1o part o3; the integration design §17.3): its <c>work.requests</c> node is active on a quest its
/// sessions published that the receiver declined, and only the person's pass over it lets the flow go on.
/// </summary>
/// <param name="Run">The run, as its binding names it: what the person's word is said on.</param>
/// <param name="Node">The <c>work.requests</c> node's id in the run's graph.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Workspace">The run's workspace.</param>
/// <param name="Quest">The declined quest the run is held at: the first among its sessions' published ones.</param>
/// <param name="To">The repository the declined quest was addressed to.</param>
/// <param name="Work">The run's newest work quest here: what the row's door reads the run by; null where it has none.</param>
/// <param name="Since">When the declined quest last moved: its decline.</param>
public sealed record RequestHold(
    string Run, string Node, string Repository, string Workspace, string Quest, string To, string? Work, DateTimeOffset? Since);

/// <summary>
/// The runs held at a declined request, judged from what the runtime last read and each run's node record (INTEGRATE1o part o3). **Read, never
/// stored** (the o2 note): the decline is the quests' record at the last look, and a node the person passed over, or whose word waits to be applied,
/// is no longer held. Pure over its inputs.
/// </summary>
public static class RequestHolds
{
    /// <summary>
    /// Each governed run holding a <c>work.requests</c> node that is active while a quest its sessions published was declined, oldest decline
    /// first. A node the person's word is pending on, passed over, stopped or reached holds nothing.
    /// </summary>
    /// <param name="governed">The runs the last look governed (the graph runs carry a scope; a unit Current's graphs govern carries none).</param>
    /// <param name="record">A unit's node record, or null where it does not read.</param>
    public static IReadOnlyList<RequestHold> Of(IEnumerable<GovernedUnit> governed, Func<string, NodeRecord?> record)
    {
        var holds = new List<RequestHold>();
        foreach (var each in governed)
        {
            if (each.Scope is not { } scope || WorkRequestsNode.Declined(scope.Requests) is not { } declined) continue;
            if (record(each.Unit.Id) is not { } kept) continue;
            foreach (var node in each.Graph.Nodes.Where(node => node.Kind == NodeKinds.WorkRequests))
            {
                if (WorkPerformer.StatusOf(kept, node.Id).State != NodeStates.Active || kept.Pending.Any(word => word.Node == node.Id)) continue;
                holds.Add(new RequestHold(
                    each.Run, node.Id, each.Unit.Owner.Repository ?? "", each.Unit.Owner.Workspace, declined.Id, declined.To, scope.Quests.LastOrDefault()?.Id,
                    declined.Updated));
            }
        }

        return [.. holds.OrderBy(hold => hold.Since ?? DateTimeOffset.MaxValue).ThenBy(hold => hold.Run, StringComparer.Ordinal)];
    }
}
