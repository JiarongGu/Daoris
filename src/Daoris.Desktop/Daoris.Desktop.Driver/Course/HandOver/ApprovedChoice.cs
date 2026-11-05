namespace Daoris.Driver;

/// <summary>
/// The choice a quest's hand-over was approved on (STAFFPOLICY1d, the staff policy design §11 point 4): the step an agent's own ask is
/// kept with, taken from the role's arranged order, and the entry the next start takes of it, revalidated against the role's order.
/// </summary>
public static class ApprovedChoice
{
    /// <summary>
    /// The arranged order's step an ask for <paramref name="agent"/> goes on with: the first entry of that agent that is on another tool than
    /// the session's own. Null where the order names none, so the agent's own entry stands as it did.
    /// </summary>
    public static HandOverStep? StepFor(string? own, IReadOnlyList<HandOverDoor> doors, IReadOnlyList<ActorEffective> arranged, string agent) =>
        HandOverOrder.Steps(own, doors, arranged).FirstOrDefault(step =>
            string.Equals(step.Harness, agent, StringComparison.Ordinal) && step.From != "roster" && (step.Model is not null || step.Effort is not null));

    /// <summary>
    /// The entry <paramref name="handed"/> was approved on, as <c>handed</c> from, where <paramref name="order"/> (the role's order) still lists
    /// it; null where the hand-over named no model or effort, or the role no longer lists the choice, and the agent's own entry stands.
    /// </summary>
    public static ActorEffective? Of(HandedQuest? handed, IReadOnlyList<ActorEffective> order) =>
        handed is { Model: null, Effort: null } or null
            ? null
            : order.Any(entry => string.Equals(entry.Agent, handed.Agent, StringComparison.OrdinalIgnoreCase) && entry.Model == handed.Model && entry.Effort == handed.Effort)
                ? new ActorEffective(handed.Agent, handed.Model, handed.Effort, "handed")
                : null;
}
