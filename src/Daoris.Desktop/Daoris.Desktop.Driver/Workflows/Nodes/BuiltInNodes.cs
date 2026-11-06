namespace Daoris.Driver;

/// <summary>
/// The git read an observation may be handed where the records cannot establish an outcome alone: <c>push</c>'s remote-tracking ref.
/// Absent, the outcome is unknown, never guessed.
/// </summary>
/// <param name="RemoteTip">The commit git's remote-tracking ref holds for a landing's branch, or null where git could not say.</param>
public sealed record NodeGitReads(Func<LandedBranch, string?>? RemoteTip = null)
{
    /// <summary>
    /// What the line and the person's branches hold of a branch unit's work at a commit (LANDNODE1k's <c>content.holds</c>), by the unit's id and
    /// that commit; null where it was not read there.
    /// </summary>
    public Func<string, string?, HoldContent?>? Content { get; init; }
}

/// <summary>
/// What an observed built-in node's records say (FABRIC-NODE1; §E4): an outcome by its authority, or none yet. Applied through the
/// same one table as any node's report (<see cref="Move"/>), so a built-in is a node and no second mechanism.
/// </summary>
/// <param name="Outcome">
/// <see cref="NodeStates.Reached"/>, <see cref="NodeStates.Failed"/> or <see cref="NodeStates.Unknown"/>, or <see cref="NodeStates.Skipped"/> where
/// the kind's record passes it over (an opinion's gate, <see cref="OpinionNode"/>; FABRIC-WIRE1g); null where the records hold no outcome yet,
/// which is pending, never failed.
/// </param>
/// <param name="Said">What the records say, in a clause.</param>
public sealed record NodeObservation(string Unit, string Node, string Kind, string? Outcome, string Said)
{
    /// <summary>The outcome's source, which the kind's authority must name.</summary>
    public string? Source { get; init; }

    /// <summary>The attempt it was observed for: its outcome is that attempt's, never a later one's.</summary>
    public int Attempt { get; init; }

    /// <summary>A reach's acceptance, bound to the commit its evidence names and pointing at its record.</summary>
    public NodeAcceptance? Acceptance { get; init; }

    /// <summary>A failure's code.</summary>
    public string? Code { get; init; }

    /// <summary>Why nothing was observed: <see cref="NodeRefusal.NotObserved"/> or <see cref="NodeRefusal.NotThisUnit"/>.</summary>
    public string? Refused { get; init; }

    /// <summary>
    /// Who made the outcome where the record keeps the person's own word (FABRIC-NODE1b: a look's <i>not yet</i> or skip, a go-ahead refused):
    /// <see cref="NodeCause.Person"/>; null for an outcome by the kind's authority, or an unknown by Daoris.
    /// </summary>
    public string? By { get; init; }

    /// <summary>The person's words the record kept with their rejection or their skip (FABRIC-NODE1b); null for every other outcome.</summary>
    public string? Words { get; init; }

    /// <summary>The report as a move on an active node: an outcome by its authority, an unknown by Daoris; none where there is no outcome.</summary>
    public NodeMove? Move => Refused is not null || Outcome is null
        ? null
        : new NodeMove(Outcome, By ?? (Outcome == NodeStates.Unknown ? NodeCause.Daoris : NodeCause.Authority))
        {
            Source = Source, Attempt = Attempt, Acceptance = Acceptance, Code = Code, Words = Words,
        };
}

/// <summary>
/// The observed built-in nodes (FABRIC-NODE1; the fabric events design §E4, §C2, §C7): <c>claimed</c>, <c>handed</c>, <c>pushed</c>,
/// <c>pull-request</c> and <c>line</c>, each read from the records its unit was read from (<see cref="WorkUnit.Records"/>, UNIT1a's
/// adapters), where §C7 keeps its fact. Pure: nothing is judged again, nothing written.
/// </summary>
/// <remarks>
/// <para><b>Each fact has its one writer, unchanged</b>: the landing writes <c>handed</c>'s entry; the end judgement's keeper,
/// <see cref="EndKeeping"/>, the kept fact <c>line</c> reads; the plugin answers' writers <c>Pushed</c> and <c>PullRequestState</c>;
/// the service, through its quest exchange, the done <c>claimed</c> reads. The look's tidy is no node, and what it removed is the
/// inventory's (§D3); a run's <c>tidy</c> (D245) is Daoris's performed step, read in its unit from what its door wrote on the landing.</para>
/// <para><b>Not yet is no outcome</b>: git's proofs never fail. A record that cannot establish the outcome without a git read
/// (<see cref="NodeGitReads"/>) leaves it unknown, and an unknown owner's unit is unknown, never ended (canary 27).</para>
/// <para><b>Each kind's reading is its unit's</b> (D247): a kind that observes says so (<see cref="IObserves"/>) and calls its reading here.</para>
/// </remarks>
public static class BuiltInNodes
{
    /// <summary>The schema of an observed built-in's evidence, its record's: what a <c>data</c> wire from its evidence port carries (WIRE1a).</summary>
    public static string EvidenceSchema(string kind) => $"daoris/{kind}@1";

    /// <summary>
    /// What <paramref name="unit"/>'s records say of <paramref name="node"/>, bound for <paramref name="attempt"/> under
    /// <paramref name="graphDigest"/>, where no wire evaluated a binding: no inputs and no prerequisite receipts.
    /// </summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, string graphDigest, int attempt, NodeGitReads? git = null) =>
        Observe(unit, node, new NodeAcceptance(unit.Id, unit.Subject.Revision, graphDigest, node.Id, node.Version, attempt), git);

    /// <summary>
    /// What <paramref name="unit"/>'s records say of <paramref name="node"/> on the binding Daoris evaluated for its attempt (FABRIC-NODE1c), with
    /// <paramref name="git"/> the only read handed: its kind's reading (<see cref="IObserves"/>), or <see cref="NodeRefusal.NotObserved"/> for a
    /// kind that observes nothing.
    /// </summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, NodeGitReads? git = null)
    {
        var reading = new NodeReading(unit, node, binding) { Git = git };
        return NodeKit.Of(node.Kind) is IObserves observes
            ? observes.Observe(reading)
            : Seen(reading, null, "this kind is not observed from records") with { Refused = NodeRefusal.NotObserved };
    }

    /// <summary>
    /// A <c>claimed</c>: the quest's done, by the ledger. On a run's subject (a binding naming its subject, which only a run's record does) it
    /// waits for the chain's last step here (FABRIC-CUTOVER1a; the cutover design §2.2): a done whose steps name a work step still to come in its
    /// repository is pending, as the close and the opinion's gate read a chain, since a reach there would lapse at the next quest and run again
    /// all that rests on it. A look's set-up step is the review's, never a step of the work.
    /// </summary>
    internal static NodeObservation Claimed(NodeReading reading)
    {
        if (Unbound(reading, WorkUnitKind.Quest) is { } refused) return refused;
        var unit = reading.Unit;
        var onRun = reading.Binding.Subject is not null;
        return unit.Records.Quest?.Status switch
        {
            "Done" when onRun && unit.Records.Quest.Then.Any(step => step.SetUpIn is null
                && string.Equals(step.To, unit.Records.Quest.To, StringComparison.OrdinalIgnoreCase)) =>
                Seen(reading, null, $"its done is in, and its chain has a step in `{unit.Records.Quest.To}` still to come: the run's work is claimed at its last"),
            "Done" => Reached(reading, NodeSource.Ledger, unit.Subject.Revision, WorkRecord.Quests, unit.Records.Quest.Id,
                unit.Records.Quest.Held ? "its done is in, and held for the person's yes or its evidence" : "its done is in"),
            "Open" or "Taken" or "Declined" => Seen(reading, null, unit.End.Said),
            _ => Seen(reading, NodeStates.Unknown, unit.End.Said),
        };
    }

    /// <summary>
    /// A <c>land</c>'s or a <c>review</c>'s: the work handed on, reached by <paramref name="source"/> (LANDNODE1c: a land's reach is the landing
    /// record's, a review's the person's). LANDNODE1c2: a review is read as a land is, since the doors that accept the work land it in one press.
    /// </summary>
    internal static NodeObservation Handed(NodeReading reading, string source)
    {
        if (OfBranch(reading) is { } refused) return refused;
        var (unit, binding) = (reading.Unit, reading.Binding);
        var branch = unit.Records.Branch;
        if (binding.Subject is not null)
        {
            // FABRIC-CUTOVER1a (the cutover design §2.4.4, §11.2): a run's `land` is its session's work handed on, reached where the session
            // branch's landing holds the tip it is bound to, made at it or squashing it (LandedBranch.Squashed), so a squash, whose commit is
            // another, still reaches it. Its acceptance keeps the bound tip, which its binding lapses on and the one table checks; its evidence
            // names the landing's tip, which what follows binds. Merged into the line with no branch landed, as below.
            if (binding.SubjectRevision is { } bound
                && unit.Records.Landings.LastOrDefault(each => each.Tip == bound || each.Squashed == bound) is { } holding)
            {
                return ReachedAt(reading, source, bound, holding.Tip, LandedBranches.FileName, holding.Branch,
                    $"`{holding.Branch}` was made at {TextBounds.ShortCommit(holding.Tip, 8)} {WorkInventory.Minute(holding.LandedAt)} UTC"
                    + (holding.Tip == bound ? "" : $", squashing {TextBounds.ShortCommit(bound, 8)}"));
            }

            return branch?.Held is { } joined
                ? Reached(reading, source, joined.Tip, SessionBranches.FileName, branch.Branch, $"merged: {WorkInventory.ProofSaid(joined.By, joined.On)}")
                : Seen(reading, null, "nothing was handed on at the bound commit yet");
        }

        if (LandingOf(unit) is { } landing)
        {
            return Reached(reading, source, landing.Tip, LandedBranches.FileName, landing.Branch,
                $"`{landing.Branch}` was made at {TextBounds.ShortCommit(landing.Tip, 8)} {WorkInventory.Minute(landing.LandedAt)} UTC");
        }

        return branch?.Held is { } merged
            ? Reached(reading, source, merged.Tip, SessionBranches.FileName, branch.Branch, $"merged: {WorkInventory.ProofSaid(merged.By, merged.On)}")
            : Seen(reading, null, "nothing was handed on yet");
    }

    /// <summary>A <c>line</c>'s: the unit's end, as the end judgement's keeper holds it.</summary>
    internal static NodeObservation Line(NodeReading reading)
    {
        if (OfBranch(reading) is { } refused) return refused;
        var unit = reading.Unit;
        var branch = unit.Records.Branch;
        if (unit.End.State == WorkEndState.Unknown) return Seen(reading, NodeStates.Unknown, unit.End.Said);
        if (unit.End.State != WorkEndState.Ended) return Seen(reading, null, unit.End.Said);
        var holder = unit.Records.Landings.FirstOrDefault(each => each.Held is not null)
            ?? (branch?.Held is null ? unit.Records.Landings.FirstOrDefault(each => each.RemovedAs is not null) : null);
        return holder is not null
            ? Reached(reading, NodeSource.Git, unit.End.Revision, LandedBranches.FileName, holder.Branch, unit.End.Said)
            : Reached(reading, NodeSource.Git, unit.End.Revision, SessionBranches.FileName, branch?.Branch ?? unit.Subject.Artifact ?? unit.Id, unit.End.Said);
    }

    /// <summary>The landing a branch unit's <c>handed</c>, <c>pushed</c> and <c>pull-request</c> read: its newest standing, else its newest; null where none.</summary>
    public static LandedBranch? LandingOf(WorkUnit unit) =>
        unit.Records.Landings.LastOrDefault(each => each.GoneAt is null) ?? unit.Records.Landings.LastOrDefault();

    /// <summary>
    /// Whether <c>pushed</c>'s observation of <paramref name="landing"/> asks git's remote-tracking ref (FABRIC-RUN1b): only where a plugin
    /// answered that it pushed, naming the commit, so a reader asks git nothing for a landing no plugin pushed.
    /// </summary>
    public static bool AsksRemote(LandedBranch landing) => landing is { Pushed: true, PushedTip: not null };

    /// <summary>
    /// A branch unit's reading refused where it is not bound to its node, or is not a branch; unknown where no record names its unit, so nothing
    /// of it is said. Null where it reads.
    /// </summary>
    internal static NodeObservation? OfBranch(NodeReading reading) =>
        Unbound(reading, WorkUnitKind.Branch)
        ?? (reading.Unit.OwnerKnown ? null : Seen(reading, NodeStates.Unknown, "no record names its unit, so nothing of it is said"));

    /// <summary>A unit of another kind than the one the node observes is no observation, nor is one it is not bound to (<see cref="NotBound"/>).</summary>
    private static NodeObservation? Unbound(NodeReading reading, string wants) =>
        NotBound(reading) ?? (reading.Unit.Kind == wants ? null
            : Seen(reading, null, $"it observes a {wants} unit, and this is a {reading.Unit.Kind}") with { Refused = NodeRefusal.NotThisUnit });

    /// <summary>
    /// A binding of another unit, node or version is no observation (<see cref="NodeRefusal.NotBound"/>): the unit it observes, its subject on a
    /// run's record (FABRIC-CUTOVER1a), never the record's own. Null where it is bound.
    /// </summary>
    internal static NodeObservation? NotBound(NodeReading reading)
    {
        var (unit, node, binding) = (reading.Unit, reading.Node, reading.Binding);
        return binding.Observes != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version
            ? Seen(reading, null, "its binding names another subject, node or version") with { Refused = NodeRefusal.NotBound }
            : null;
    }

    /// <summary>What the reading's node is observed as: <paramref name="outcome"/>, or none yet, with what its records say.</summary>
    internal static NodeObservation Seen(NodeReading reading, string? outcome, string said) =>
        new(reading.Unit.Id, reading.Node.Id, reading.Node.Kind, outcome, said) { Attempt = reading.Binding.Attempt };

    /// <summary>A reach by <paramref name="source"/>, its acceptance at <paramref name="revision"/> and its evidence the record's entry.</summary>
    internal static NodeObservation Reached(NodeReading reading, string source, string? revision, string record, string artifact, string said) =>
        ReachedAt(reading, source, revision, revision, record, artifact, said);

    /// <summary>
    /// A reach, its acceptance on the binding at <paramref name="revision"/> and its evidence naming <paramref name="evidenced"/>: apart only where
    /// a run's <c>handed</c> keeps its bound tip (FABRIC-CUTOVER1a). A reach's acceptance carries that binding's graph, attempt, inputs and
    /// prerequisite receipts, so invalidating anything it rests on reaches it; where its revision is not the binding's, the one table refuses it.
    /// </summary>
    private static NodeObservation ReachedAt(NodeReading reading, string source, string? revision, string? evidenced, string record, string artifact, string said)
    {
        var accepted = reading.Binding with { SubjectRevision = revision, Evidence = null, Outputs = NodeAcceptance.NoPorts };
        return Seen(reading, NodeStates.Reached, said) with
        {
            Source = source,
            Acceptance = accepted with { Evidence = new NodePort(EvidenceSchema(reading.Node.Kind), $"{record}#{artifact}", evidenced, accepted.Id) },
        };
    }
}
