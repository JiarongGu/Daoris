using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>How a node starts (the fabric events design §E2's <c>reach</c>; §D4, with <c>waited</c> added by §E1).</summary>
public static class NodeReach
{
    /// <summary>Git or a plugin reports it: every built-in proof (§C2), a plugin's events (§C4).</summary>
    public const string Observed = "observed";

    /// <summary>Daoris asks a plugin to do it (§D6), or does it itself (<c>quest.close</c>, §E5).</summary>
    public const string Performed = "performed";

    /// <summary>A person reaches it at D156's doors (§D5).</summary>
    public const string Pressed = "pressed";

    /// <summary>It waits on other work through its adapter (<c>wait.unit</c>, §E6).</summary>
    public const string Waited = "waited";

    public static readonly IReadOnlyList<string> All = [Observed, Performed, Pressed, Waited];
}

/// <summary>The node kinds this build knows (§E2's authority table, §E4). A plugin's own node is <c>&lt;plugin&gt;/&lt;node&gt;</c>.</summary>
public static class NodeKinds
{
    /// <summary>The agent's done, on the ledger: a claim, and never the end (§C2).</summary>
    public const string Claimed = "claimed";

    /// <summary>Daoris's landing: the landing branch at its recorded tip, or the merge on the line.</summary>
    public const string Handed = "handed";

    /// <summary>The plugin's push answer, its tip held by git's remote-tracking ref.</summary>
    public const string Pushed = "pushed";

    /// <summary>The plugin's completed answer, passing D148 point 4's checks.</summary>
    public const string PullRequest = "pull-request";

    /// <summary>Git's proof that the line holds the work (§B3): the kept end fact, <i>held</i>.</summary>
    public const string Line = "line";

    /// <summary>A person's task (§D5).</summary>
    public const string Human = "human";

    /// <summary>The person's look at the work (D154): <i>reviewed</i> or <i>not yet</i>.</summary>
    public const string Look = "look";

    /// <summary>The person's go-ahead (D135).</summary>
    public const string GoAhead = "go-ahead";

    /// <summary>Waiting on other work, through a commitment (§E6).</summary>
    public const string WaitUnit = "wait.unit";

    /// <summary>Daoris closing the quest at the work's end (§E5).</summary>
    public const string QuestClose = "quest.close";

    /// <summary>A join of a group's members, keeping its identity and failure policy (§E7).</summary>
    public const string All = "all";

    /// <summary>The second opinion's gate (D155 §8.2, §E4): observed, before the look and the landing in D157 §2.2's order (FABRIC-WORKFLOW2).</summary>
    public const string Opinion = "opinion";

    /// <summary>
    /// Where a failure or a refusal stops the run short of its end: a decoded D157 version's <c>stop</c>, §E7's synthetic sink
    /// (FABRIC-WORKFLOW2). Reached on the receipt of the path into it; never a graph's end.
    /// </summary>
    public const string Stop = "stop";

    public static readonly IReadOnlyList<string> BuiltIn = [Claimed, Handed, Pushed, PullRequest, Line, Human, Look, GoAhead, WaitUnit, QuestClose, All, Opinion, Stop];

    /// <summary>Whether a kind is a plugin's own node: <c>&lt;plugin&gt;/&lt;node&gt;</c>, both parts named.</summary>
    public static bool IsPlugin(string kind)
    {
        var slash = kind.IndexOf('/');
        return slash > 0 && slash < kind.Length - 1 && kind.IndexOf('/', slash + 1) < 0 && !kind.Any(char.IsWhiteSpace);
    }
}

/// <summary>
/// The one lifecycle's states (§E2). Every kind has these and no others: what differs by kind is only who may report an outcome.
/// </summary>
public static class NodeStates
{
    /// <summary>Its wires, inputs or authority are not satisfied.</summary>
    public const string Blocked = "blocked";

    /// <summary>Eligible, and not started.</summary>
    public const string Ready = "ready";

    /// <summary>Started: observing, dispatching, offering the task, or waiting.</summary>
    public const string Active = "active";

    /// <summary>Accepted, with bound evidence (<see cref="NodeAcceptance"/>).</summary>
    public const string Reached = "reached";

    /// <summary>An authoritative negative outcome, with its code.</summary>
    public const string Failed = "failed";

    /// <summary>A person's rejection, with a reason.</summary>
    public const string Rejected = "rejected";

    /// <summary>The outcome cannot be established now: reconciled before any new attempt.</summary>
    public const string Unknown = "unknown";

    /// <summary>The person passed it over for this work, with their words: it satisfies <c>after</c> and no <c>data</c> wire.</summary>
    public const string Skipped = "skipped";

    /// <summary>The person stopped it.</summary>
    public const string Stopped = "stopped";

    /// <summary>Its binding changed: history is kept, and it is judged again on a fresh binding.</summary>
    public const string Invalidated = "invalidated";

    public static readonly IReadOnlyList<string> All = [Blocked, Ready, Active, Reached, Failed, Rejected, Unknown, Skipped, Stopped, Invalidated];

    /// <summary>
    /// The states §E2's <i>any live state</i> names, which the person may stop: every state with no settled outcome. An invalidated node
    /// is live, since it is about to be judged again (FABRIC-NODE1's reading, D173's note).
    /// </summary>
    public static readonly IReadOnlyList<string> Live = [Blocked, Ready, Active, Unknown, Invalidated];

    /// <summary>
    /// The states §E2's <i>any bound state</i> names, which a changed binding invalidates: an attempt started or its outcome, each bound
    /// to the binding it ran on. A skip and a stop are the person's word for this work, not for a revision, so a new tip keeps them
    /// (FABRIC-NODE1's reading, D173's note).
    /// </summary>
    public static readonly IReadOnlyList<string> Bound = [Active, Reached, Failed, Rejected, Unknown];
}

/// <summary>Who causes a move (§E2's third column).</summary>
public static class NodeCause
{
    /// <summary>Daoris: its evaluation of wires and inputs, a start, an unknown, an invalidation, a declared retry. Never an outcome.</summary>
    public const string Daoris = "daoris";

    /// <summary>The kind's reach or failure authority, naming its <see cref="NodeSource"/>.</summary>
    public const string Authority = "authority";

    /// <summary>The person, at D156's doors: a rejection, a skip, a stop, a new attempt.</summary>
    public const string Person = "person";
}

/// <summary>
/// Where an outcome's report comes from (§E2's authority, §C4's belief). Each kind names the one source that reaches it and the one
/// that fails it; a report from any other source is no outcome. <see cref="Ledger"/> is a claim, which reaches <c>claimed</c> alone.
/// </summary>
public static class NodeSource
{
    /// <summary>Git's proof, as the driver's own records keep it: the landing, the kept end fact.</summary>
    public const string Git = "git";

    /// <summary>A plugin's believed answer or event: the five rules of §C4 passed, bound to the work's commit.</summary>
    public const string Plugin = "plugin";

    /// <summary>A plugin's push answer whose pushed tip git's remote-tracking ref holds (§C2's <c>pushed</c>).</summary>
    public const string PluginRef = "plugin-ref";

    /// <summary>The person, at D156's doors: an approval, <i>reviewed</i>, a yes.</summary>
    public const string Person = "person";

    /// <summary>A commitment's receipt, or its owner's decline (§E6).</summary>
    public const string Commitment = "commitment";

    /// <summary>The close door's write, or its authoritative refusal (§E5).</summary>
    public const string CloseDoor = "close-door";

    /// <summary>A join's members: every one reached or skipped, or one failed under the group's policy (§E7).</summary>
    public const string Members = "members";

    /// <summary>The agent's own word, the ledger's done: a claim, never another node's evidence.</summary>
    public const string Ledger = "ledger";

    /// <summary>The second opinion's gate as the driver keeps it (D155 §8.2): an answer covering the tip with no dispute open, or the person's answer in its place.</summary>
    public const string Opinion = "opinion";

    /// <summary>The path into a sink (§E7): the failure or the refusal whose receipt leads there, as a join's members reach it.</summary>
    public const string Path = "path";
}

/// <summary>A node's declared retry (§D4, §D6): how many attempts it may take on one binding, and the delay before each new one.</summary>
/// <param name="MaxAttempts">Attempts on one binding, the first included; a person's new attempt or a fresh binding counts afresh.</param>
/// <param name="DelaysSeconds">The delay before the second attempt, the third, and so on; the last repeats. A copy of what was set.</param>
public sealed record NodeRetry(int MaxAttempts, IReadOnlyList<int> DelaysSeconds)
{
    // FABRIC-NODE1d: copied when set, so a caller's list changing later moves neither the retry nor its declaration's Problem.
    private readonly IReadOnlyList<int> delaysSeconds = Copied(DelaysSeconds);

    public IReadOnlyList<int> DelaysSeconds { get => delaysSeconds; init => delaysSeconds = Copied(value); }

    private static ImmutableArray<int> Copied(IReadOnlyList<int> delays) =>
        ImmutableArray.CreateRange(delays ?? throw new ArgumentNullException(nameof(delays)));

    // FABRIC-UNIT1d: equal by what it says, so a manifest's declaration is the one built by hand.
    public bool Equals(NodeRetry? other) =>
        other is not null && MaxAttempts == other.MaxAttempts && DelaysSeconds.SequenceEqual(other.DelaysSeconds);

    public override int GetHashCode() => DelaysSeconds.Aggregate(MaxAttempts.GetHashCode(), HashCode.Combine);
}

/// <summary>
/// How a plugin's performed node is performed (FABRIC-UNIT1d; the fabric events design §D6, <i>the performer's contract</i>): what the
/// kit promises and PERFORM1 dispatches by. Only a plugin's own <c>performed</c> node declares one; a manifest spells it in the step's
/// <c>perform</c>, its point <c>work/perform</c>.
/// </summary>
/// <param name="Idempotent">The plugin deduplicates a redelivered idempotency key durably: the same key never makes a second external effect.</param>
/// <param name="Reconcile">The plugin answers <c>work/reconcile</c>: what became of an attempt whose outcome is unknown.</param>
/// <param name="Transient">
/// The failure codes worth retrying, under the node's <see cref="NodeDeclaration.Retry"/>; any other code stops it for the person. A
/// copy of what was set.
/// </param>
public sealed record NodePerform(bool Idempotent, bool Reconcile, IReadOnlyList<string> Transient)
{
    /// <summary>A failure code's shape (§D6's <c>unavailable</c>, <c>rate-limited</c>): lowercase letters, digits and dashes, at most 40.</summary>
    public static readonly System.Text.RegularExpressions.Regex CodeShape =
        new(@"^[a-z0-9][a-z0-9-]{0,39}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<string> transient = Copied(Transient);

    public IReadOnlyList<string> Transient { get => transient; init => transient = Copied(value); }

    private static ImmutableArray<string> Copied(IReadOnlyList<string> codes) =>
        ImmutableArray.CreateRange(codes ?? throw new ArgumentNullException(nameof(codes)));

    public bool Equals(NodePerform? other) =>
        other is not null && Idempotent == other.Idempotent && Reconcile == other.Reconcile
        && Transient.SequenceEqual(other.Transient, StringComparer.Ordinal);

    public override int GetHashCode() =>
        Transient.Aggregate(HashCode.Combine(Idempotent, Reconcile), (hash, code) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(code)));
}

/// <summary>
/// An immutable reference a port carries (§E2, <i>ports are references</i>): a consumer receives this, never a copied payload or a
/// file path, and a resolver checks permission for itself.
/// </summary>
/// <param name="Schema">What it is, by schema and version (<c>acme.deploy/receipt@1</c>).</param>
/// <param name="Ref">Where its producer keeps it, by record and name: never a machine path.</param>
/// <param name="Revision">The revision it was made at.</param>
/// <param name="Acceptance">The acceptance that produced it (<see cref="NodeAcceptance.Id"/>): what invalidates its consumers.</param>
public sealed record NodePort(string Schema, string Ref, string? Revision, string? Acceptance);

/// <summary>
/// One node (FABRIC-NODE1; the fabric events design §E2): <c>{ id, kind, version, reach, inputs, outputs, evidence, retry?,
/// timeoutSeconds?, options? }</c>. Its <c>kind</c> chooses an adapter and its <c>reach</c> says how it starts; its lifecycle is
/// <see cref="NodeLifecycle"/>'s, the same for every kind. The unit it acts on is its run's binding (<see cref="NodeStatus.Unit"/>).
/// </summary>
/// <param name="Id">Its id in its graph.</param>
/// <param name="Kind">One of <see cref="NodeKinds"/>, or a plugin's own <c>&lt;plugin&gt;/&lt;node&gt;</c>.</param>
/// <param name="Version">Its declaration's version: an acceptance binds it, so a new version invalidates the old acceptances.</param>
/// <param name="Reach">One of <see cref="NodeReach"/>: a built-in kind's own, a plugin's observed or performed.</param>
public sealed record NodeDeclaration(string Id, string Kind, int Version, string Reach)
{
    /// <summary>No ports: a declaration's inputs or outputs before any are named.</summary>
    public static readonly IReadOnlyDictionary<string, string> NoPorts = ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal);

    // FABRIC-NODE1d: each collection is copied when it is set, in name order, so a graph's digest over declarations never moves with a
    // caller's dictionary.
    private readonly IReadOnlyDictionary<string, string> inputs = NoPorts;
    private readonly IReadOnlyDictionary<string, string> outputs = NoPorts;

    /// <summary>
    /// Its inputs by name, each with the schema it takes: filled by <c>data</c> wires with references, never payloads. Each is mandatory
    /// (§E3). A copy of what was set.
    /// </summary>
    public IReadOnlyDictionary<string, string> Inputs { get => inputs; init => inputs = Frozen(value); }

    /// <summary>Its declared outputs by name, each with its schema: a reached node exposes each as a <see cref="NodePort"/>. A copy of what was set.</summary>
    public IReadOnlyDictionary<string, string> Outputs { get => outputs; init => outputs = Frozen(value); }

    private static IReadOnlyDictionary<string, string> Frozen(IReadOnlyDictionary<string, string> ports) =>
        (ports ?? throw new ArgumentNullException(nameof(ports))).ToImmutableSortedDictionary(StringComparer.Ordinal);

    /// <summary>The schema of the evidence it is reached on, or null for a built-in, whose evidence is its record's.</summary>
    public string? Evidence { get; init; }

    public NodeRetry? Retry { get; init; }

    /// <summary>A persisted deadline (§D4), delivered as an event: it never means success, failure, or a deletion.</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// The option a plugin's own node declares to let a person reject it (§E2: <i>only where the node declares it</i>). A built-in
    /// kind's judgement is the kind's, never a declaration's.
    /// </summary>
    public bool Rejectable { get; init; }

    /// <summary>
    /// Whether the person may pass a plugin's own node over (D157's skip): a plugin's declaration may forbid it with <c>false</c>
    /// (FABRIC-NODE1c). A built-in kind's skip is the kind's, never a declaration's.
    /// </summary>
    public bool Skippable { get; init; } = true;

    /// <summary>
    /// How a plugin's own <c>performed</c> node is performed (FABRIC-UNIT1d, §D6), or null: none declared, which is dispatched and never
    /// redelivered by itself. Every other node declares none.
    /// </summary>
    public NodePerform? Perform { get; init; }

    private readonly IReadOnlyDictionary<string, string> options = NoPorts;

    /// <summary>
    /// Its adapter's options by name (§E2's <c>options</c>, as a human node's <c>brief</c>), each its JSON value's text as written: what
    /// the adapter reads, never a condition (FABRIC-GRAPH1). A copy of what was set, in name order, so a graph's digest holds it.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options { get => options; init => options = Frozen(value); }

    /// <summary>
    /// Made by decoding a D157 version (a sink, a person's task or a join, §E7), never authored: counted apart from a graph's 24-node
    /// bound (FABRIC-GRAPH1, <see cref="NodeGraphs.MaxNodes"/>). A graph read from a person's file declares none.
    /// </summary>
    public bool Synthetic { get; init; }

    /// <summary>
    /// FABRIC-UNIT1d: two declarations are equal where they say the same, whoever built their collections, so a manifest's step is the
    /// declaration built by hand and a graph compares declarations by what they say (GRAPH1's options and synthetic mark included).
    /// </summary>
    public bool Equals(NodeDeclaration? other) =>
        other is not null
        && string.Equals(Id, other.Id, StringComparison.Ordinal) && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
        && Version == other.Version && string.Equals(Reach, other.Reach, StringComparison.Ordinal)
        && Same(Inputs, other.Inputs) && Same(Outputs, other.Outputs)
        && string.Equals(Evidence, other.Evidence, StringComparison.Ordinal) && Equals(Retry, other.Retry)
        && TimeoutSeconds == other.TimeoutSeconds && Rejectable == other.Rejectable && Skippable == other.Skippable
        && Equals(Perform, other.Perform) && Same(Options, other.Options) && Synthetic == other.Synthetic;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id, StringComparer.Ordinal);
        hash.Add(Kind, StringComparer.Ordinal);
        hash.Add(Version);
        hash.Add(Reach, StringComparer.Ordinal);
        foreach (var (name, schema) in Inputs.Concat(Outputs).Concat(Options))
        {
            hash.Add(name, StringComparer.Ordinal);
            hash.Add(schema, StringComparer.Ordinal);
        }

        hash.Add(Evidence, StringComparer.Ordinal);
        hash.Add(Retry);
        hash.Add(TimeoutSeconds);
        hash.Add(Rejectable);
        hash.Add(Skippable);
        hash.Add(Perform);
        hash.Add(Synthetic);
        return hash.ToHashCode();
    }

    // Both are ordinal-sorted copies (FABRIC-NODE1d), so the same ports are in the same order.
    private static bool Same(IReadOnlyDictionary<string, string> ports, IReadOnlyDictionary<string, string> others) =>
        ports.Count == others.Count && ports.Zip(others).All(pair =>
            string.Equals(pair.First.Key, pair.Second.Key, StringComparison.Ordinal)
            && string.Equals(pair.First.Value, pair.Second.Value, StringComparison.Ordinal));
}

/// <summary>
/// Who may report each outcome of a kind (§E2's authority table): the one thing that differs between kinds. Every other part of the
/// lifecycle is the same for all of them.
/// </summary>
/// <param name="Reach">The kind's reach; a plugin's own node takes its declaration's.</param>
/// <param name="ReachBy">The <see cref="NodeSource"/> whose report reaches it.</param>
/// <param name="FailureBy">The source whose report fails it, or null: not yet is pending, never failed.</param>
/// <param name="PersonJudges">Whether a person may reject it, at D156's doors.</param>
/// <param name="PersonSkips">Whether the person may pass it over for this work (D157's skip).</param>
/// <param name="MayEnd">Whether it may be a graph's end: <c>claimed</c> never is (§C2, §E4).</param>
/// <param name="Evidenced">
/// Whether a reach must carry an evidence reference (§E2, <i>ports are references</i>): every record's or answer's kind does. The
/// person's kinds do not, since their word is the outcome (HUMAN1 adds attachments), nor a join, whose receipts are its members'.
/// </param>
public sealed record NodeAuthority(string Kind, string Reach, string ReachBy, string? FailureBy, bool PersonJudges, bool PersonSkips, bool MayEnd, bool Evidenced)
{
    /// <summary>
    /// Whether its start sends an effect outside Daoris (a performed or a waited reach): until its authority's outcome or a
    /// reconciliation establishes how that attempt ended, no new attempt starts, whatever moved the node off it (FABRIC-NODE1c).
    /// </summary>
    public bool Dispatches => Reach is NodeReach.Performed or NodeReach.Waited;
}

/// <summary>The authority by kind (§E2): the built-ins' table, and a plugin's own node's from its declaration.</summary>
/// <remarks>
/// <para><b>Skips</b> (§E2 leaves which nodes allow one to the node; FABRIC-NODE1's reading, D173's note): git's proofs
/// (<c>claimed</c>, <c>handed</c>, <c>line</c>) are never skipped, since <i>held</i> and the cleanup rest on them and a skip would read
/// as evidence; nor is <c>quest.close</c>, whose override is the person's own done (§E5), nor a join, whose members are skipped
/// instead, nor a <c>stop</c>, which is the run stopped (FABRIC-WORKFLOW2). Every other kind may be, and a plugin's own node unless
/// its declaration forbids it (FABRIC-NODE1c).</para>
/// </remarks>
public static class NodeAuthorities
{
    public static readonly IReadOnlyDictionary<string, NodeAuthority> BuiltIn = new Dictionary<string, NodeAuthority>(StringComparer.Ordinal)
    {
        [NodeKinds.Claimed] = new(NodeKinds.Claimed, NodeReach.Observed, NodeSource.Ledger, null, false, false, MayEnd: false, Evidenced: true),
        [NodeKinds.Handed] = new(NodeKinds.Handed, NodeReach.Observed, NodeSource.Git, null, false, false, true, true),
        [NodeKinds.Line] = new(NodeKinds.Line, NodeReach.Observed, NodeSource.Git, null, false, false, true, true),
        [NodeKinds.Pushed] = new(NodeKinds.Pushed, NodeReach.Observed, NodeSource.PluginRef, NodeSource.Plugin, false, true, true, true),
        [NodeKinds.PullRequest] = new(NodeKinds.PullRequest, NodeReach.Observed, NodeSource.Plugin, NodeSource.Plugin, false, true, true, true),
        [NodeKinds.Human] = new(NodeKinds.Human, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false),
        // §E4: the look is its gate, observed, and the person's *reviewed* is what reaches it.
        [NodeKinds.Look] = new(NodeKinds.Look, NodeReach.Observed, NodeSource.Person, null, true, true, true, false),
        [NodeKinds.GoAhead] = new(NodeKinds.GoAhead, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false),
        [NodeKinds.WaitUnit] = new(NodeKinds.WaitUnit, NodeReach.Waited, NodeSource.Commitment, NodeSource.Commitment, false, true, true, true),
        [NodeKinds.QuestClose] = new(NodeKinds.QuestClose, NodeReach.Performed, NodeSource.CloseDoor, NodeSource.CloseDoor, false, false, true, true),
        [NodeKinds.All] = new(NodeKinds.All, NodeReach.Observed, NodeSource.Members, NodeSource.Members, false, false, true, false),
        // FABRIC-WORKFLOW2: D157's opinion never fails (a required one waits), and the person may pass it over, as its gate's
        // unavailable reads as skipped today. A sink is the run stopped: never skipped, never the end, its receipt its path's.
        [NodeKinds.Opinion] = new(NodeKinds.Opinion, NodeReach.Observed, NodeSource.Opinion, null, false, true, true, true),
        [NodeKinds.Stop] = new(NodeKinds.Stop, NodeReach.Observed, NodeSource.Path, null, false, false, MayEnd: false, Evidenced: false),
    };

    /// <summary>
    /// The node's authority: its built-in kind's, or a plugin's own (its believed answer either way, a person only where declared, a
    /// skip unless declared otherwise); null for a kind this build does not know. A plugin's answer is named <see cref="NodeSource.Plugin"/>
    /// here alone: that the answer came from the declaring plugin is PERFORM1c's authenticated sender check, not the lifecycle's.
    /// </summary>
    public static NodeAuthority? Of(NodeDeclaration node) =>
        BuiltIn.TryGetValue(node.Kind, out var builtIn) ? builtIn
        : NodeKinds.IsPlugin(node.Kind) ? new NodeAuthority(node.Kind, node.Reach, NodeSource.Plugin, NodeSource.Plugin, node.Rejectable, node.Skippable, true, true)
        : null;
}

/// <summary>A declaration's own checks (§D4's <i>validated before saved</i>, the node's half; the wires' half is WIRE1a's).</summary>
public static class NodeDeclarations
{
    /// <summary>
    /// What does not hold, by its field, or null: a kind this build does not know (<see cref="NodeRefusal.UnknownKind"/>), a reach not
    /// the kind's (a plugin's is observed or performed), a version below 1, a retry of fewer than one attempt or on a kind nothing can
    /// fail, a timeout not above zero, an output named <c>evidence</c> (the evidence port's name, <see cref="WireEnd.Evidence"/>), or a
    /// rejection a built-in's declaration tries to grant or a skip it tries to forbid (the kind's own, never a declaration's). Then
    /// FABRIC-UNIT1d's (§D6): a <c>perform</c> on any node but a plugin's performed one, a transient code not of
    /// <see cref="NodePerform.CodeShape"/> or named twice, transient codes with no retry to take them, and a plugin's retry of more
    /// than one attempt that nothing could take: a transient code, on a node that is idempotent or reconciled.
    /// </summary>
    public static string? Problem(NodeDeclaration node)
    {
        if (string.IsNullOrWhiteSpace(node.Id)) return "id";
        var authority = NodeAuthorities.Of(node);
        if (authority is null) return NodeRefusal.UnknownKind;
        var builtIn = NodeAuthorities.BuiltIn.ContainsKey(node.Kind);
        if (builtIn ? node.Reach != authority.Reach : node.Reach is not (NodeReach.Observed or NodeReach.Performed)) return "reach";
        if (node.Version < 1) return "version";
        if (node.Retry is { } retry && (retry.MaxAttempts < 1 || retry.DelaysSeconds.Any(delay => delay < 0) || authority.FailureBy is null)) return "retry";
        if (node.TimeoutSeconds is <= 0) return "timeoutSeconds";
        // FABRIC-WIRE1a: a data wire names a node's evidence by this port, so no output may take its name.
        if (node.Outputs.ContainsKey(WireEnd.Evidence)) return "outputs";
        if (builtIn && node.Rejectable) return "rejectable";
        if (builtIn && !node.Skippable) return "skippable";

        // FABRIC-UNIT1d (§D6): how a plugin performs its node is that node's alone, and a retry or a transient code is refused where
        // it could never be taken, as a retry on a kind nothing can fail is. Daoris's own performed kind's retry stays its kind's.
        if (node.Perform is { } perform)
        {
            if (builtIn || node.Reach != NodeReach.Performed) return "perform";
            if (perform.Transient.Any(code => code is null || !NodePerform.CodeShape.IsMatch(code))
                || perform.Transient.Distinct(StringComparer.Ordinal).Count() != perform.Transient.Count
                || (perform.Transient.Count > 0 && node.Retry is not { MaxAttempts: > 1 }))
            {
                return "transient";
            }
        }

        if (!builtIn && node.Retry is { MaxAttempts: > 1 } && !(node.Perform is { Transient.Count: > 0 } taken && (taken.Idempotent || taken.Reconcile)))
        {
            return "retry";
        }

        return null;
    }
}
