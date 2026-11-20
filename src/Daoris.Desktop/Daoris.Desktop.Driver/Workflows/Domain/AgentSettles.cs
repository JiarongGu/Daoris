namespace Daoris.Driver;

/// <summary>
/// What an agent's channel settles: a step settled by an observation (a check's result, a landing, a go-ahead read on its record, a plugin's
/// act read from its outcome), by the work an agent's session does (the work, the second opinion), or by a session's tool
/// (<see cref="StepSettles"/>): a task's Done or a decision's answer, handed to the run's working session (<see cref="StepHandOffs"/>), and a
/// review's Accept or a task's approval, answered by the agent holding the role through its own session, never the work's makers'
/// (<see cref="SettleDoor.Own"/>). The owner role alone is cast to no agent.
/// </summary>
public static class AgentSettles
{
    /// <summary>A cast or a hand-over of an agent to a step no agent's channel settles.</summary>
    public const string Refused = "agent-cannot-settle";

    /// <summary>
    /// Whether an agent holding the role settles a step standing at the wait <paramref name="why"/> with no row for the person: an observed one,
    /// a plugin's actor (its plugin observes the act) and a go-ahead (observed on its record, who may answer it the grants').
    /// </summary>
    public static bool Wait(string why) => why is PersonWaits.Actor or PersonWaits.GoAhead;

    /// <summary>Whether an agent holding <paramref name="node"/> settles it by a session's tool: a task's Done or decision, a review's Accept, a task's approval.</summary>
    public static bool Tool(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && NodeKit.Of(node.Kind) is IWaitsOnRole waits
        && waits.Waits(node, status) is var why
        && ((node.Kind == NodeKinds.Task && why is PersonWaits.Task or PersonWaits.Approval) || (node.Kind == NodeKinds.Review && why == PersonWaits.Accept));

    /// <summary>Whether <paramref name="node"/> is the run's working session's own work to settle by its tool: a task waiting on its Done or a decision.</summary>
    private static bool Works(NodeDeclaration node, NodeStatus status) =>
        Tool(node, status) && node.Kind == NodeKinds.Task && NodeKit.Of(node.Kind) is IWaitsOnRole waits && waits.Waits(node, status) == PersonWaits.Task;

    /// <summary>Whether the step is handed to the agent holding its own role, settled by its tool or its acts' receipts: the person's one press is <i>Take</i>.</summary>
    public static bool Handed(NodeDeclaration node, NodeStatus status, RoleWait wait) =>
        wait.Holder.Kind != StepHolders.Person && wait.Role == StepRoles.Of(node)
        && (Works(node, status) || (wait.Why == PersonWaits.Actor && StepReceipts.Mapped(node).Count > 0));

    /// <summary>The roles no agent is cast to at a place: the owner's.</summary>
    public static IReadOnlyList<string> Roles { get; } = [StepRoles.Owner];

    /// <summary>Why <paramref name="role"/> is not cast to <paramref name="holder"/>, or null.</summary>
    public static string? Cast(string role, string? holder) =>
        IsAgent(holder) && Roles.Contains(role, StringComparer.Ordinal) ? Said(role) : Unnamed(role, holder);

    /// <summary>Why <paramref name="node"/> is not handed to <paramref name="holder"/>, or null.</summary>
    public static string? Hand(NodeDeclaration node, string? holder) => Unnamed(StepRoles.Of(node), holder);

    /// <summary>The settle door admits every agent's word for the bare <c>agent</c>, the run's own worker's among them.</summary>
    private static string? Unnamed(string role, string? holder) => role == StepRoles.AskReader && holder?.Trim() == CastHolders.Agent
        ? $"no staffing names the agent of `{role}`, and any agent would speak as it: name the agent that reads the dones (`agent:<name>`)"
        : null;

    /// <summary>A place's cast of <paramref name="role"/> as Settings and <c>daoris settings cast</c> make it: refused for an agent no channel settles for, else kept.</summary>
    public static string? CastAt(PlaceCastFiles casts, string place, string role, string? holder, string? why, DateTimeOffset now) =>
        Cast(role, holder) ?? casts.Cast(place, role, holder, why, now);

    /// <summary>
    /// <i>Take</i>, <i>Hand to…</i> or <i>Give back</i> as the window and the terminal make it (<see cref="StepHolding.Act"/>), a hand-over to an
    /// agent no channel settles for refused first.
    /// </summary>
    public static StepHeld Hold(
        NodeRecordFiles records, PlaceCastFiles casts, PlaceRiskBook risk, string run, string node, string act, string? holder, string? why, DateTimeOffset now,
        int? epoch = null, SettleBy? by = null)
    {
        var named = run.Trim().TrimStart('#');
        if (act == CastActs.Hand && new GraphRuns(records).Find(named) is { Version.Graph: { } graph }
            && graph.Nodes.FirstOrDefault(each => each.Id == node) is { } declared && Hand(declared, holder) is { } refused)
        {
            return new StepHeld(named, node, act, Refused) { Detail = refused };
        }

        return StepHolding.Act(records, casts, risk, run, node, act, holder, why, now, epoch, by);
    }

    /// <summary>The terminal's sentence for what a hold came to (<see cref="StepHolding.Said"/>), with this refusal's reason.</summary>
    public static string Said(StepHeld held) => held.Refused == Refused
        ? $"workflow: {held.Node} of #{held.Run} is not handed on: {held.Detail}; take it, or hand it to a person."
        : StepHolding.Said(held);

    private static bool IsAgent(string? holder) => holder?.Trim() is { } named && (named == CastHolders.Agent || named.StartsWith(CastHolders.AgentPrefix, StringComparison.Ordinal));

    private static string Said(string role) => $"the `{role}` role is the person's, the top of every line: no agent is cast to it";
}
