namespace Daoris.Driver;

/// <summary>
/// The <c>opinion</c> node observed: reached, passed
/// over or pending on the second opinion's gate as <see cref="OpinionGate.JudgeAsync"/> judged it, or on the opinion a recorded landing
/// kept. Pure: the caller hands the judgement in, and nothing is judged again or written.
/// </summary>
/// <remarks>
/// <para><b>The gate's reading, not a second one.</b> The gate's file keeps asks and the person's answers, never a verdict: whether an
/// opinion covers the tip, is delivered and answered, under the rule that applies, is the gate's judgement, which the caller hands in.
/// This reads its state as the run reads it (<c>WorkflowRun</c>'s opinion step): <i>settled</i> and the person's three answers reach
/// it; <i>none</i>, and an <i>unavailable</i> the rule does not require, pass it over, by the gate's record and never in the person's
/// words (<see cref="NodeAuthority.SkipBy"/>); every other state is pending, never failed, since nothing fails an opinion.</para>
/// <para><b>Bound to its commit and its work.</b> A judgement at another commit than the binding's, with no commit read, for another
/// repository, or not handed in at all says nothing: the node stays pending, never completed or passed over on a guess. A rule that asks
/// no opinion here judges no commit, and its read carries none, so it passes the node over whatever it is bound to.</para>
/// <para><b>A landing recorded at the bound commit keeps its reading</b>: what its gate let go, as the landing record kept it, whatever
/// the gate says now. A kept <i>unavailable</i> passes it over; any other, or none kept (a landing recorded with none, or one
/// no rule asked of), reaches it.</para>
/// </remarks>
public static class OpinionGateReading
{
    /// <summary>Where the gate's record is kept, by name (<c>&lt;home&gt;/opinions/gates/&lt;chain&gt;.json</c>): what a reach's evidence names.</summary>
    public const string GateRecord = "opinions/gates";

    /// <summary>What the gate says of the reading's node on its binding, for its unit's work: an outcome by the gate's authority, or none, which is pending.</summary>
    public static NodeObservation Observe(NodeReading reading)
    {
        var (unit, binding, gate) = (reading.Unit, reading.Binding, reading.Opinion);

        NodeObservation Seen(string? outcome, string said) => NodeObserving.Seen(reading, outcome, said);

        NodeObservation Reached(string record, string artifact, string? revision, string said) => Seen(NodeStates.Reached, said) with
        {
            Source = NodeSource.Opinion,
            Acceptance = binding with { Evidence = new NodePort(NodeObserving.EvidenceSchema(reading.Node.Kind), $"{record}#{artifact}", revision, binding.Id) },
        };

        NodeObservation Skipped(string said) => Seen(NodeStates.Skipped, said) with { Source = NodeSource.Opinion };

        // The unit its binding observes, a run's branch on the run's record, never the record's own unit.
        if (NodeObserving.NotBound(reading) is { } refused) return refused;

        var landed = binding.SubjectRevision is { } bound
            ? unit.Records.Landings.Where(each => string.Equals(each.Tip, bound, StringComparison.Ordinal) || string.Equals(each.Squashed, bound, StringComparison.Ordinal)).ToList()
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

        // A rule that asks no second opinion here judges no commit, and a landing door's read of it carries none
        // (TreeGates.OpinionAsync), so it passes the opinion over whatever its node is bound to, a quest's work included. One read at a commit
        // is that commit's, as the gate judged it.
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
