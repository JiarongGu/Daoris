namespace Daoris.Driver;

/// <summary>
/// What an agent's channel settles today (D312, amended). No channel speaks a press for an agent yet, so an agent holds a step only where the
/// step settles without one: by an observation (a check's result, a landing, a go-ahead read on its record, a plugin's act read from its
/// outcome) or by the work an agent's session does (the work, the second opinion). A step its own role's press settles (a review's Accept, a
/// task's Done, approval or decision) has no agent settler: a place's cast refuses an agent for the role such a kind takes where it names
/// none, a hand-over refuses an agent for such a step, and a step an agent holds anyway, or any wait on a role an agent holds that only a press
/// settles, stays a row of <i>What needs you</i> with <i>Take</i> (<see cref="HeldSteps"/>).
/// </summary>
public static class AgentSettles
{
    /// <summary>A cast or a hand-over of an agent to a step no agent's channel settles.</summary>
    public const string Refused = "agent-cannot-settle";

    /// <summary>
    /// Whether an agent holding the role settles a step standing at the wait <paramref name="why"/>: only an observed one, a plugin's actor (its
    /// plugin observes the act) and a go-ahead (observed on its record, who may answer it the grants').
    /// </summary>
    public static bool Wait(string why) => why is PersonWaits.Actor or PersonWaits.GoAhead;

    /// <summary>Whether <paramref name="node"/> settles only by its own role's press where it stands: an Accept, a task's Done, an approval or a decision.</summary>
    public static bool Pressed(NodeDeclaration node) =>
        NodeKit.Of(node.Kind) is IWaitsOnRole waits
        && waits.Waits(node, new NodeStatus("", node.Id, NodeStates.Active) { Attempt = 1 }) is PersonWaits.Accept or PersonWaits.Approval or PersonWaits.Task;

    /// <summary>The roles no agent is cast to at a place: those the kinds a press alone settles take where a step names none (an Accept's, a task's).</summary>
    public static IReadOnlyList<string> Roles =>
        [.. NodeKit.Built.Units.Where(unit => Pressed(new NodeDeclaration(unit.Row.Kind, unit.Row.Kind, 1, unit.Row.Reach))).Select(unit => unit.Role).Distinct(StringComparer.Ordinal)];

    /// <summary>Why <paramref name="role"/> is not cast to <paramref name="holder"/>, or null.</summary>
    public static string? Cast(string role, string? holder) => IsAgent(holder) && Roles.Contains(role, StringComparer.Ordinal) ? Said(role) : null;

    /// <summary>Why <paramref name="node"/> is not handed to <paramref name="holder"/>, or null.</summary>
    public static string? Hand(NodeDeclaration node, string? holder) => IsAgent(holder) && Pressed(node) ? Said(StepRoles.Of(node)) : null;

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
        $"no agent's channel settles a `{role}` step yet: only its holder's press does (an Accept, a task's Done, an approval or a decision), so an agent holding it would leave it waiting";
}
