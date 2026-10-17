namespace Daoris.Driver;

/// <summary>
/// The <c>look</c> node observed for the one thing its gate's record decides (FABRIC-WIRE1h; the fabric events design §E4, D173's
/// FABRIC-WORKFLOW2b note, <c>passed-by-rule</c>): the chain's or the ask's choice that turns the look off for this work, as
/// <see cref="ReviewGate"/> read it. Pure: the caller hands the gate's read in, and nothing is judged again or written.
/// </summary>
/// <remarks>
/// <para><b>Only the off choice passes it over, by the record and never in the person's words</b> (<see cref="NodeAuthority.SkipBy"/>): the
/// person's reviewed reaches the look, their skip passes it over with their words, and every other gate state is theirs to answer, so
/// nothing here completes, rejects or fails a look. A read not handed in says nothing, and the look stays pending.</para>
/// </remarks>
public static class LookNode
{
    /// <summary>What the review gate says of <paramref name="node"/> for <paramref name="unit"/>'s work: a skip by the gate's record, or none.</summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, ReviewGateState? gate)
    {
        NodeObservation Seen(string? outcome, string said) => new(unit.Id, node.Id, node.Kind, outcome, said) { Attempt = binding.Attempt };

        if (node.Kind != NodeKinds.Look) return Seen(null, "this kind is not a look") with { Refused = NodeRefusal.NotObserved };
        if (binding.Unit != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version)
        {
            return Seen(null, "its binding names another unit, node or version") with { Refused = NodeRefusal.NotBound };
        }

        if (gate is null) return Seen(null, "its gate was not read for this work");

        // The gate says none where the level's table asks no review (ReviewDecision.Reviews false), and a look waiting on a set-up that cannot
        // start is no choice: both fields say it, so neither is read as off alone.
        return gate.State == ReviewStates.None && !gate.Decision.Reviews
            ? Seen(NodeStates.Skipped, "the choice turns its review off for this work") with { Source = NodeSource.Review }
            : Seen(null, $"its gate says {gate.State}");
    }
}
