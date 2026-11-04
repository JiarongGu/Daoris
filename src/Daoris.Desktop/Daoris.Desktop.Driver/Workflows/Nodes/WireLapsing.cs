using static Daoris.Driver.WireEligible;
using static Daoris.Driver.WireEvaluation;
using static Daoris.Driver.WireLapsing;

namespace Daoris.Driver;

/// <summary>Whether a bound node's binding is still its own and current, and why it is invalidated where it is not.</summary>
internal static class WireLapsing
{
    /// <summary>
    /// The binding of <paramref name="status"/>'s attempt where it was started in this graph and run, by its own rule (FABRIC-RUN1f,
    /// <see cref="WireRun.Own"/>), and that scope; a null binding where it is another's or there is none.
    /// </summary>
    internal static (NodeAcceptance? Binding, NodeScope Scope) Ours(NodeDeclaration node, NodeStatus status, WireRun run)
    {
        var scope = run.Own(node.Id, status.Binding?.GraphDigest);
        return (status.Binding is { } binding && binding.GraphDigest == scope.GraphDigest && status.Scope == scope ? binding : null, scope);
    }

    /// <summary>Whether Daoris still retries this failure (§D6): a transient code with attempts left, so its <c>failed</c> path is not taken yet.</summary>
    internal static bool Retries(NodeDeclaration node, NodeStatus status) => status.Transient && status.Tries < (node.Retry?.MaxAttempts ?? 1);

    /// <summary>
    /// Why a node is invalidated now, or null: a skip of another graph or run; a bound node whose binding's subject, revision, graph or version
    /// is no longer current; or one resting on a receipt or an input's acceptance that is no longer current.
    /// </summary>
    internal static (string Why, string? Detail)? Lapse(NodeDeclaration node, NodeStatus status, WireRun run, HashSet<string> current, Subjected subjected)
    {
        // FABRIC-WIRE1g: a skip by its kind's record (an opinion's gate, NodeAuthority.SkipBy) is that record's judgement of its binding, so
        // a new revision or a lost receipt lapses it, as it lapses an outcome; the person's skip is their word for the work, and holds.
        var recorded = status.State == NodeStates.Skipped && status.By is { } by && by != NodeSource.Person && NodeAuthorities.Of(node)?.SkipBy == by;
        if (status.State == NodeStates.Skipped)
        {
            if (!NodeLifecycle.Skips(status, run.Own(node.Id, status.Scope?.GraphDigest))) return (WireWhy.SkipScope, null);
            if (!recorded) return null;
        }

        if (!(recorded || NodeStates.Bound.Contains(status.State)) || status.Binding is not { } binding) return null;

        // FABRIC-CUTOVER1a (the cutover design §2.2): a binding lapses as `subject` once its node's subject moves, whatever the revision. A node
        // withheld this pass, its subject not found or its revision not read, is judged on its binding's own, so nothing lapses on a read not
        // made, and only a moved subject lapses it there.
        var subject = subjected.Subject ?? binding.Observes;
        var revision = subjected.Withheld is null ? subjected.Revision : binding.SubjectRevision;

        // FABRIC-RUN1e, in place of WIRE1f's lapse of another run's failure: a failure or a rejection of another graph or run is kept for the
        // person, since a run begins at a graph edit and a Setup save can edit a derived graph, so edits back and forth would restart failed
        // work past its retry bound for ever. Only new work lapses it, a new subject or a new revision, as in its own graph and run; the
        // person's new attempt restarts it. It is no receipt here (Receipts reads this graph's and run's alone), so nothing fires on it.
        // FABRIC-RUN1f: this graph and run are the node's place and the run, or for an attempt bound before, the graph and the run's old name.
        var (ours, own) = Ours(node, status, run);
        if (status.State is NodeStates.Failed or NodeStates.Rejected && ours is null)
        {
            return !string.Equals(binding.Observes, subject, StringComparison.Ordinal) ? (WireWhy.Lapsed, NodeLapse.Subject)
                : string.Equals(binding.SubjectRevision, revision, StringComparison.Ordinal) ? null : (WireWhy.Lapsed, NodeLapse.Revision);
        }

        if (NodeAcceptances.Lapsed(binding, subject, revision, own.GraphDigest, node.Version) is { } why) return (WireWhy.Lapsed, why);
        var lost = binding.PrerequisiteReceipts.FirstOrDefault(receipt => !current.Contains(receipt))
            ?? binding.Inputs.Values.Select(port => port.Acceptance).FirstOrDefault(acceptance => acceptance is not null && !current.Contains(acceptance));
        if (lost is not null) return (WireWhy.Upstream, lost);

        // DECIDE1a: a reached decision whose outcome no longer reads as one of its own is judged again, never routed on a guess.
        return status.Current is { } reached && !NodeOutcomes.Fits(node, reached.Outcome) ? (WireWhy.Lapsed, NodeLapse.Outcome) : null;
    }

    /// <summary>The revision a node is bound to and lapses on: its own where the caller gives one (FABRIC-PERFORM1m), else its unit's.</summary>
    internal static string? Revision(WireRun run, string unit, string node) =>
        run.NodeRevisions.TryGetValue(node, out var own) ? own : run.Revisions.TryGetValue(unit, out var revision) ? revision : null;
}
