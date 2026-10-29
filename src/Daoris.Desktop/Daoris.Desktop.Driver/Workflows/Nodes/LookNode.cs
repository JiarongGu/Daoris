namespace Daoris.Driver;

/// <summary>
/// The <c>look</c> node observed from its gate's records (FABRIC-WIRE1h, FABRIC-NODE1b; the fabric events design §E4, §E2): the person's
/// verdict as <see cref="ReviewGate"/> read it, or the review a recorded landing kept, and the chain's or the ask's choice that turns the look
/// off for this work. Pure: the caller hands the gate's read in, and nothing is judged again or written.
/// </summary>
/// <remarks>
/// <para><b>The person's word, as their gate keeps it.</b> Their <i>reviewed</i> of a set-up holding the bound commit reaches the look, by its
/// reach authority, the person; their <i>not yet</i> rejects it in their words; their skip passes it over in their words. A rejection or a skip
/// kept with no words is no word of theirs here, and the look waits rather than move on words nobody gave.</para>
/// <para><b>Only the off choice passes it over by the record</b> (<see cref="NodeAuthority.SkipBy"/>), never in the person's words; and a
/// landing at the bound commit that kept no review had none asked of it, which the landing keeps of the gate, so the record passes it over too.</para>
/// <para><b>Bound to its commit and its work.</b> A verdict read at another commit than the binding's, with no commit read, or for another
/// repository says nothing, and every other state of the gate is the person's still to answer: the look waits, never reached on a guess.</para>
/// </remarks>
public static class LookNode
{
    /// <summary>What the review gate says of <paramref name="node"/> for <paramref name="unit"/>'s work: an outcome its records keep, or none.</summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, ReviewGateState? gate)
    {
        NodeObservation Seen(string? outcome, string said) => new(unit.Id, node.Id, node.Kind, outcome, said) { Attempt = binding.Attempt };

        // The person's reviewed: by the look's reach authority, on the binding it was started on. It exposes no evidence (§E2: the person's look
        // is no record a consumer reads).
        NodeObservation Reached(string said) => Seen(NodeStates.Reached, said) with { Source = NodeSource.Person, Acceptance = binding };

        NodeObservation Persons(string outcome, string? words, string said) => string.IsNullOrWhiteSpace(words)
            ? Seen(null, $"{said}, and no words were kept with it, which the person's {(outcome == NodeStates.Rejected ? "not yet" : "skip")} needs")
            : Seen(outcome, said) with { By = NodeCause.Person, Words = words };

        NodeObservation PassedOver(string said) => Seen(NodeStates.Skipped, said) with { Source = NodeSource.Review };

        if (node.Kind != NodeKinds.Look) return Seen(null, "this kind is not a look") with { Refused = NodeRefusal.NotObserved };
        // FABRIC-CUTOVER1a: the unit its binding observes, a run's branch on the run's record, never the record's own unit.
        if (binding.Observes != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version)
        {
            return Seen(null, "its binding names another subject, node or version") with { Refused = NodeRefusal.NotBound };
        }

        // FABRIC-NODE1b: a landing recorded at the bound commit keeps the review that let it go, whatever the gate says now (as an opinion's does).
        var landed = binding.SubjectRevision is { } bound
            ? unit.Records.Landings.Where(each => string.Equals(each.Tip, bound, StringComparison.Ordinal)).ToList()
            : [];
        if (landed.Count > 0)
        {
            var landing = landed.LastOrDefault(each => each.Review is not null) ?? landed[^1];
            return landing.Review switch
            {
                null => PassedOver($"`{landing.Branch}` landed with no review asked of it"),
                { Said: ReviewVerdicts.Reviewed } => Reached($"`{landing.Branch}` landed on your review"),
                { Said: ReviewVerdicts.Skipped } review => Persons(NodeStates.Skipped, review.Words, $"`{landing.Branch}` landed on your skip of its review"),
                var review => Seen(null, $"`{landing.Branch}` landed on a review it kept as {review.Said}"),
            };
        }

        if (gate is null) return Seen(null, "its gate was not read for this work");

        // The gate says none where the level's table asks no review (ReviewDecision.Reviews false), and a look waiting on a set-up that cannot
        // start is no choice: both fields say it, so neither is read as off alone.
        if (gate.State == ReviewStates.None && !gate.Decision.Reviews) return PassedOver("the choice turns its review off for this work");

        if (unit.Owner.Repository is { } repository && gate.Decision.Repository.Length > 0
            && !string.Equals(gate.Decision.Repository, repository, StringComparison.OrdinalIgnoreCase))
        {
            return Seen(null, $"its gate was read for `{gate.Decision.Repository}`, not this work's `{repository}`");
        }

        if (gate.Tip is not { } tip || !string.Equals(tip, binding.SubjectRevision, StringComparison.Ordinal))
        {
            return Seen(null, gate.Tip is null ? "its gate was read with no commit" : $"its gate was read at {TextBounds.ShortCommit(gate.Tip, 8)}, not the bound commit");
        }

        return gate.State switch
        {
            ReviewStates.Reviewed => Reached("you reviewed a set-up holding this work"),
            ReviewStates.NotYet => Persons(NodeStates.Rejected, gate.Verdict?.Words, "you said not yet of its set-up"),
            ReviewStates.Skipped => Persons(NodeStates.Skipped, gate.Decision.Skip?.Words ?? gate.Decision.Words, "you skipped its review"),
            _ => Seen(null, $"its gate says {gate.State}"),
        };
    }
}
