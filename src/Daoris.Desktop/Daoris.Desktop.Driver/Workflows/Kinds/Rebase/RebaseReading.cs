namespace Daoris.Driver;

/// <summary>
/// A <c>rebase</c> read from what its door wrote of its attempt (<see cref="RebaseRecords"/>, handed to the pass as <see cref="NodeGitReads.Rebased"/>):
/// <c>ready</c> with its <c>commit</c> on the attempt's step ref, made onto the target's tip; <c>conflicted</c> or <c>uncommitted</c> with its
/// <c>problem</c> and the stop's identity; failed on a git error. Nothing yet while its door replays.
/// </summary>
internal static class RebaseReading
{
    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.NotBound(reading) is { } refused) return refused;
        var binding = reading.Binding;
        var run = RunUnits.Is(binding.Unit) ? binding.Unit[RunUnits.Prefix.Length..] : binding.Unit;
        if (reading.Git?.Rebased?.Invoke(run, reading.Node.Id, binding.Attempt) is not { } made
            || !string.Equals(made.Tip, binding.SubjectRevision, StringComparison.OrdinalIgnoreCase))
        {
            return NodeObserving.Seen(reading, null, "Replaying its work in a tree of Daoris's own.");
        }

        if (made.Outcome == NodeStates.Failed) return NodeObserving.Seen(reading, NodeStates.Failed, made.Said) with { Source = NodeSource.Git, Code = NodeSource.Git };
        var stepRef = StepRefs.Of(run, reading.Node.Id, binding.Attempt);
        var reached = NodeObserving.Reached(reading, NodeSource.Git, binding.SubjectRevision, "rebases", stepRef, made.Said);
        var producer = new NodeProducer(reading.Node.Id, binding.Attempt);
        var accepted = reached.Acceptance!;
        var outputs = made.Outcome == RebaseOutcomes.Ready
            ? new Dictionary<string, NodePort>(StringComparer.Ordinal)
            {
                [RebaseNode.Commit] = new(WireValues.Commit, stepRef, made.Commit, accepted.Id) { Producer = producer, Onto = made.Onto },
            }
            : new Dictionary<string, NodePort>(StringComparer.Ordinal)
            {
                [RebaseNode.Problem] = new(WireValues.Problem, stepRef, made.StopCommit ?? made.Tip, accepted.Id) { Producer = producer, Onto = made.Onto, Stop = made.Stop },
            };
        return reached with { Acceptance = accepted with { Outcome = made.Outcome, Outputs = outputs } };
    }
}
