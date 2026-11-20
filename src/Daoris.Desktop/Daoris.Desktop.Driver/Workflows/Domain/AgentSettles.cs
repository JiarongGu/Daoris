namespace Daoris.Driver;

/// <summary>
/// What an agent's channel settles today: a step settled by an observation (a check's result, a landing, a go-ahead read on its record, a
/// plugin's act read from its outcome), by the work an agent's session does (the work, the second opinion), or by the working session's tool
/// (<see cref="StepSettles"/>, <see cref="StepHandOffs"/>). A step its own role's press alone settles (a review's Accept, an approval) has no
/// agent settler: a place's cast and a hand-over refuse an agent for it, and a step an agent holds anyway stays a row of
/// <i>What needs you</i> with <i>Take</i> (<see cref="HeldSteps"/>).
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

    /// <summary>Whether an agent holding <paramref name="node"/> settles it by its working session's tool: a task under way waiting on its Done or a decision.</summary>
    public static bool Tool(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && node.Kind == NodeKinds.Task && NodeKit.Of(node.Kind) is IWaitsOnRole waits
        && waits.Waits(node, status) == PersonWaits.Task;

    /// <summary>Whether the step is handed to the agent holding its own role, settled by its tool or its acts' receipts: the person's one press is <i>Take</i>.</summary>
    public static bool Handed(NodeDeclaration node, NodeStatus status, RoleWait wait) =>
        wait.Holder.Kind != StepHolders.Person && wait.Role == StepRoles.Of(node)
        && (Tool(node, status) || (wait.Why == PersonWaits.Actor && StepReceipts.Mapped(node).Count > 0));

    /// <summary>Whether <paramref name="node"/> settles only by its own role's press where it stands: an Accept or an approval.</summary>
    public static bool Pressed(NodeDeclaration node) =>
        NodeKit.Of(node.Kind) is IWaitsOnRole waits
        && waits.Waits(node, new NodeStatus("", node.Id, NodeStates.Active) { Attempt = 1 }) is PersonWaits.Accept or PersonWaits.Approval;

    /// <summary>The roles no agent is cast to at a place: those the kinds a press alone settles take where a step names none, and the owner's.</summary>
    public static IReadOnlyList<string> Roles =>
        [.. NodeKit.Built.Units.Where(unit => Pressed(new NodeDeclaration(unit.Row.Kind, unit.Row.Kind, 1, unit.Row.Reach))).Select(unit => unit.Role)
            .Append(StepRoles.Owner).Distinct(StringComparer.Ordinal)];

    /// <summary>Why <paramref name="role"/> is not cast to <paramref name="holder"/>, or null.</summary>
    public static string? Cast(string role, string? holder) =>
        IsAgent(holder) && Roles.Contains(role, StringComparer.Ordinal) ? Said(role) : Unnamed(role, holder);

    /// <summary>Why <paramref name="node"/> is not handed to <paramref name="holder"/>, or null.</summary>
    public static string? Hand(NodeDeclaration node, string? holder) =>
        IsAgent(holder) && Pressed(node) ? Said(StepRoles.Of(node)) : Unnamed(StepRoles.Of(node), holder);

    /// <summary>The settle door admits every agent's word for the bare <c>agent</c>, the run's own worker's among them.</summary>
    private static string? Unnamed(string role, string? holder) => role == StepRoles.AskReader && holder?.Trim() == CastHolders.Agent
        ? $"no staffing names the agent of `{role}`, and any agent would speak as it: name the agent that reads the dones (`agent:<name>`)"
        : null;

    /// <summary>A place's cast of <paramref name="role"/> as Settings and <c>daoris settings cast</c> make it: refused for an agent no channel settles for, else kept.</summary>
    public static string? CastAt(PlaceCastFiles casts, string place, string role, string? holder, string? why, DateTimeOffset now) =>
        Cast(role, holder) ?? casts.Cast(place, role, holder, why, now);

    /// <summary>
    /// <i>Take</i>, <i>Hand to…</i> or <i>Give back</i> as the window and the terminal make it (<see cref="StepHolding.Act"/>), a hand-over of a
    /// step a press alone settles to an agent refused first.
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

    private static string Said(string role) =>
        $"no agent's channel settles a `{role}` step yet: only its holder's press does (an Accept or an approval), so an agent holding it would leave it waiting";
}
