namespace Daoris.Driver;

/// <summary>
/// The git reads an observation may be handed where the records cannot establish an outcome alone: <c>pushed</c>'s remote-tracking ref,
/// and <c>pull-request</c>'s ancestry check (D148 point 4's third). Absent, the outcome is unknown, never guessed.
/// </summary>
/// <param name="RemoteTip">The commit git's remote-tracking ref holds for a landing's branch, or null where git could not say.</param>
/// <param name="Holds">Whether the first commit holds the second (is it, or descends from it), or null where git could not say.</param>
public sealed record NodeGitReads(Func<LandedBranch, string?>? RemoteTip = null, Func<string, string, bool?>? Holds = null);

/// <summary>
/// What an observed built-in node's records say (FABRIC-NODE1; §E4): an outcome by its authority, or none yet. Applied through the
/// same one table as any node's report (<see cref="Move"/>), so a built-in is a node and no second mechanism.
/// </summary>
/// <param name="Outcome"><see cref="NodeStates.Reached"/>, <see cref="NodeStates.Failed"/> or <see cref="NodeStates.Unknown"/>; null where the records hold no outcome yet, which is pending, never failed.</param>
/// <param name="Said">What the records say, in a clause.</param>
public sealed record NodeObservation(string Unit, string Node, string Kind, string? Outcome, string Said)
{
    /// <summary>The outcome's source, which the kind's authority must name.</summary>
    public string? Source { get; init; }

    /// <summary>A reach's acceptance, bound to the commit its evidence names and pointing at its record.</summary>
    public NodeAcceptance? Acceptance { get; init; }

    /// <summary>A failure's code.</summary>
    public string? Code { get; init; }

    /// <summary>Why nothing was observed: <see cref="NodeRefusal.NotObserved"/> or <see cref="NodeRefusal.NotThisUnit"/>.</summary>
    public string? Refused { get; init; }

    /// <summary>The report as a move on an active node: an outcome by its authority, an unknown by Daoris; none where there is no outcome.</summary>
    public NodeMove? Move => Refused is not null || Outcome is null
        ? null
        : new NodeMove(Outcome, Outcome == NodeStates.Unknown ? NodeCause.Daoris : NodeCause.Authority) { Source = Source, Acceptance = Acceptance, Code = Code };
}

/// <summary>
/// The observed built-in nodes (FABRIC-NODE1; the fabric events design §E4, §C2, §C7): <c>claimed</c>, <c>handed</c>, <c>pushed</c>,
/// <c>pull-request</c> and <c>line</c>, each read from the records its unit was read from (<see cref="WorkUnit.Records"/>, UNIT1a's
/// adapters), where §C7 keeps its fact. Pure: nothing is judged again, nothing written.
/// </summary>
/// <remarks>
/// <para><b>Each fact has its one writer, unchanged</b>: the landing writes <c>handed</c>'s entry; the end judgement's keeper,
/// <see cref="EndKeeping"/>, the kept fact <c>line</c> reads; the plugin answers' writers <c>Pushed</c> and <c>PullRequestState</c>;
/// the service, through its quest exchange, the done <c>claimed</c> reads. The tidy is no node: no node removes anything (§E9), and
/// what it removed is the inventory's (§D3).</para>
/// <para><b>Not yet is no outcome</b>: git's proofs never fail. A record that cannot establish the outcome without a git read
/// (<see cref="NodeGitReads"/>) leaves it unknown, and an unknown owner's unit is unknown, never ended (canary 27).</para>
/// </remarks>
public static class BuiltInNodes
{
    /// <summary>The kinds observed from records.</summary>
    public static readonly IReadOnlyList<string> Observed = [NodeKinds.Claimed, NodeKinds.Handed, NodeKinds.Pushed, NodeKinds.PullRequest, NodeKinds.Line];

    /// <summary>What <paramref name="unit"/>'s records say of <paramref name="node"/>, bound for <paramref name="attempt"/> under <paramref name="graphDigest"/>.</summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, string graphDigest, int attempt, NodeGitReads? git = null)
    {
        NodeObservation Seen(string? outcome, string said) => new(unit.Id, node.Id, node.Kind, outcome, said);

        NodeObservation Reached(string source, string? revision, string record, string artifact, string said)
        {
            var accepted = new NodeAcceptance(unit.Id, revision, graphDigest, node.Id, node.Version, attempt);
            return Seen(NodeStates.Reached, said) with
            {
                Source = source,
                Acceptance = accepted with { Evidence = new NodePort($"daoris/{node.Kind}@1", $"{record}#{artifact}", revision, accepted.Id) },
            };
        }

        if (!Observed.Contains(node.Kind)) return Seen(null, "this kind is not observed from records") with { Refused = NodeRefusal.NotObserved };
        var wants = node.Kind == NodeKinds.Claimed ? WorkUnitKind.Quest : WorkUnitKind.Branch;
        if (unit.Kind != wants) return Seen(null, $"it observes a {wants} unit, and this is a {unit.Kind}") with { Refused = NodeRefusal.NotThisUnit };

        if (node.Kind == NodeKinds.Claimed) return Claimed(unit, Seen, Reached);
        if (!unit.OwnerKnown) return Seen(NodeStates.Unknown, "no record names its unit, so nothing of it is said");

        var landing = unit.Records.Landings.LastOrDefault(each => each.GoneAt is null) ?? unit.Records.Landings.LastOrDefault();
        var branch = unit.Records.Branch;
        switch (node.Kind)
        {
            case NodeKinds.Handed:
                if (landing is not null)
                {
                    return Reached(NodeSource.Git, landing.Tip, LandedBranches.FileName, landing.Branch,
                        $"`{landing.Branch}` was made at {TextBounds.ShortCommit(landing.Tip, 8)} {WorkInventory.Minute(landing.LandedAt)} UTC");
                }

                return branch?.Held is { } merged
                    ? Reached(NodeSource.Git, merged.Tip, SessionBranches.FileName, branch.Branch, $"merged: {WorkInventory.ProofSaid(merged.By, merged.On)}")
                    : Seen(null, "nothing was handed on yet");

            case NodeKinds.Line:
                if (unit.End.State == WorkEndState.Unknown) return Seen(NodeStates.Unknown, unit.End.Said);
                if (unit.End.State != WorkEndState.Ended) return Seen(null, unit.End.Said);
                var holder = unit.Records.Landings.FirstOrDefault(each => each.Held is not null)
                    ?? (branch?.Held is null ? unit.Records.Landings.FirstOrDefault(each => each.RemovedAs is not null) : null);
                return holder is not null
                    ? Reached(NodeSource.Git, unit.End.Revision, LandedBranches.FileName, holder.Branch, unit.End.Said)
                    : Reached(NodeSource.Git, unit.End.Revision, SessionBranches.FileName, branch?.Branch ?? unit.Subject.Artifact ?? unit.Id, unit.End.Said);

            case NodeKinds.Pushed:
                if (landing is not { Pushed: true, PushedTip: { } pushed }) return Seen(null, "no plugin answered that it pushed the work");
                if (git?.RemoteTip is not { } remoteTip)
                {
                    return Seen(NodeStates.Unknown, $"the plugin answered that it pushed {TextBounds.ShortCommit(pushed, 8)}, and git's remote-tracking ref was not read");
                }

                return remoteTip(landing) switch
                {
                    null => Seen(NodeStates.Unknown, "git could not read the remote-tracking ref"),
                    var remote when string.Equals(remote, pushed, StringComparison.Ordinal) => Reached(NodeSource.PluginRef, pushed, LandedBranches.FileName,
                        landing.Branch, $"pushed at {TextBounds.ShortCommit(pushed, 8)}, and the remote-tracking ref holds it"),
                    _ => Seen(null, "the remote-tracking ref does not hold the pushed commit"),
                };

            default:
                return PullRequest(landing, git, Seen, Reached);
        }
    }

    private static NodeObservation Claimed(
        WorkUnit unit, Func<string?, string, NodeObservation> seen, Func<string, string?, string, string, string, NodeObservation> reached) =>
        unit.Records.Quest?.Status switch
        {
            "Done" => reached(NodeSource.Ledger, unit.Subject.Revision, WorkRecord.Quests, unit.Records.Quest.Id,
                unit.Records.Quest.Held ? "its done is in, and held for the person's yes or its evidence" : "its done is in"),
            "Open" or "Taken" or "Declined" => seen(null, unit.End.Said),
            _ => seen(NodeStates.Unknown, unit.End.Said),
        };

    private static NodeObservation PullRequest(
        LandedBranch? landing, NodeGitReads? git, Func<string?, string, NodeObservation> seen,
        Func<string, string?, string, string, string, NodeObservation> reached)
    {
        if (landing is null) return seen(null, "nothing was handed on yet");
        var answer = landing.PullRequestState;
        if (answer?.State == PullRequestStates.Completed)
        {
            // D148 point 4's first check from the record: both commits named, its target the line. Its third, below.
            if (answer is not { MergeCommit.Length: > 0, SourceCommit: { Length: > 0 } source } || landing.Line is null
                || !string.Equals(answer.Target, landing.Line, StringComparison.Ordinal))
            {
                return seen(null, "its completed answer does not pass D148's checks, so it is no event");
            }

            var holds = string.Equals(source, landing.Tip, StringComparison.Ordinal) ? true : git?.Holds?.Invoke(source, landing.Tip);
            return holds switch
            {
                true => reached(NodeSource.Plugin, landing.Tip, LandedBranches.FileName, landing.Branch,
                    $"its pull request completed, merging {TextBounds.ShortCommit(source, 8)} into `{answer.Target}`"),
                false => seen(null, "its completed pull request's source does not hold the recorded tip"),
                null => seen(NodeStates.Unknown, "its pull request completed from a newer commit, and git's ancestry check was not read"),
            };
        }

        if (landing.PullRequestAskFailed is { } failed) return seen(NodeStates.Unknown, $"the last ask failed ({failed.Code})");
        return answer?.State switch
        {
            PullRequestStates.Abandoned => seen(NodeStates.Failed, "its pull request was abandoned") with { Source = NodeSource.Plugin, Code = PullRequestStates.Abandoned },
            PullRequestStates.Unknown => seen(NodeStates.Unknown, "the platform answered without a state"),
            PullRequestStates.Open => seen(null, "its pull request is open"),
            _ => seen(null, "no plugin answered about a pull request yet"),
        };
    }
}
