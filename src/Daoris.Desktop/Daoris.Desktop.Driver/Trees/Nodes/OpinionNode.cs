namespace Daoris.Driver;

/// <summary>
/// The <c>opinion</c> node observed (FABRIC-WIRE1g; the fabric events design §E4, D173's FABRIC-WORKFLOW2 note, choice 3): reached, passed
/// over or pending on the second opinion's gate as <see cref="OpinionGate.JudgeAsync"/> judged it, or on the opinion a recorded landing
/// kept. Pure: the caller hands the judgement in, and nothing is judged again or written.
/// </summary>
/// <remarks>
/// <para><b>The gate's reading, not a second one.</b> The gate's file keeps asks and the person's answers, never a verdict: whether an
/// opinion covers the tip, was delivered and answered, under the rule that applies, is the gate's judgement, which the caller hands in.
/// This reads its state as D157's run reads it (<c>WorkflowRun</c>'s opinion step): <i>settled</i> and the person's three answers reach
/// it; <i>none</i>, and an <i>unavailable</i> the rule does not require, pass it over, by the gate's record and never in the person's
/// words (<see cref="NodeAuthority.SkipBy"/>); every other state is pending, never failed, since nothing fails an opinion.</para>
/// <para><b>Bound to its commit and its work.</b> A judgement at another commit than the binding's, with no commit read, for another
/// repository, or not handed in at all says nothing: the node stays pending, never completed or passed over on a guess. A rule that asks
/// no opinion here judges no commit, and its read carries none, so it passes the node over whatever it is bound to (FABRIC-RUN1c).</para>
/// <para><b>A landing recorded at the bound commit keeps today's reading</b>: what its gate let go, as the landing record kept it, whatever
/// the gate says now. A kept <i>unavailable</i> passes it over; any other, or none kept (a landing from before the record kept one, or one
/// no rule asked of), reaches it.</para>
/// </remarks>
public static class OpinionNode
{
    /// <summary>Where the gate's record is kept, by name (<c>&lt;home&gt;/opinions/gates/&lt;chain&gt;.json</c>): what a reach's evidence names.</summary>
    public const string GateRecord = "opinions/gates";

    /// <summary>
    /// What the gate says of <paramref name="node"/> on <paramref name="binding"/>, for <paramref name="unit"/>'s work: an outcome by the
    /// gate's authority, or none, which is pending.
    /// </summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, OpinionGateState? gate)
    {
        NodeObservation Seen(string? outcome, string said) => new(unit.Id, node.Id, node.Kind, outcome, said) { Attempt = binding.Attempt };

        NodeObservation Reached(string record, string artifact, string? revision, string said) => Seen(NodeStates.Reached, said) with
        {
            Source = NodeSource.Opinion,
            Acceptance = binding with { Evidence = new NodePort(BuiltInNodes.EvidenceSchema(node.Kind), $"{record}#{artifact}", revision, binding.Id) },
        };

        NodeObservation Skipped(string said) => Seen(NodeStates.Skipped, said) with { Source = NodeSource.Opinion };

        if (node.Kind != NodeKinds.Opinion) return Seen(null, "this kind is not an opinion") with { Refused = NodeRefusal.NotObserved };
        if (binding.Unit != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version)
        {
            return Seen(null, "its binding names another unit, node or version") with { Refused = NodeRefusal.NotBound };
        }

        var landed = binding.SubjectRevision is { } bound
            ? unit.Records.Landings.Where(each => string.Equals(each.Tip, bound, StringComparison.Ordinal)).ToList()
            : [];
        if (landed.Count > 0)
        {
            var landing = landed.LastOrDefault(each => each.Opinion is not null) ?? landed[^1];
            return landing.Opinion?.Said switch
            {
                OpinionGateStates.Unavailable => Skipped($"`{landing.Branch}` landed without one: none could be had, and none was required"),
                null => Reached(LandedBranches.FileName, landing.Branch, landing.Tip, $"`{landing.Branch}` landed: what its gate let go"),
                var said => Reached(LandedBranches.FileName, landing.Branch, landing.Tip, $"`{landing.Branch}` landed on its gate's {said}"),
            };
        }

        if (gate is null) return Seen(null, "no judgement of its gate was read for this work");
        if (unit.Owner.Repository is { } repository && !string.Equals(gate.Repository, repository, StringComparison.OrdinalIgnoreCase))
        {
            return Seen(null, $"its gate was judged for `{gate.Repository}`, not this work's `{repository}`");
        }

        // FABRIC-RUN1c: a rule that asks no second opinion here judges no commit, and a landing door's read of it carries none
        // (TreeGates.OpinionAsync), so it passes the opinion over whatever its node is bound to, a quest's work included. One read at a commit
        // is that commit's, as WIRE1g judged it.
        if (gate is { State: OpinionGateStates.None, Tip: null }) return Skipped("its rule asks no second opinion here");

        if (gate.Tip is not { } tip || !string.Equals(tip, binding.SubjectRevision, StringComparison.Ordinal))
        {
            return Seen(null, gate.Tip is null ? "its gate was judged with no commit read" : $"its gate was judged at {TextBounds.ShortCommit(gate.Tip, 8)}, not the bound commit");
        }

        return gate.State switch
        {
            OpinionGateStates.Settled or OpinionGateStates.Anyway or OpinionGateStates.Myself or OpinionGateStates.Answered =>
                Reached(GateRecord, $"{gate.Repository}:{gate.Opinion?.Id ?? gate.Person?.Said ?? gate.State}", tip, $"its gate says {gate.State}"),
            OpinionGateStates.None => Skipped("its rule asks no second opinion here"),
            OpinionGateStates.Unavailable when !gate.Required => Skipped("none could be had, and its rule does not require one"),
            _ => Seen(null, $"its gate says {gate.State}"),
        };
    }
}
