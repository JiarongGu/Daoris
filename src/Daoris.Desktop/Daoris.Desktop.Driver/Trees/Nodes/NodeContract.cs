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

    public static readonly IReadOnlyList<string> BuiltIn = [Claimed, Handed, Pushed, PullRequest, Line, Human, Look, GoAhead, WaitUnit, QuestClose, All];

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
/// instead. Every other kind may be, and a plugin's own node unless its declaration forbids it (FABRIC-NODE1c).</para>
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
    /// fail, a timeout not above zero, or a rejection a built-in's declaration tries to grant or a skip it tries to forbid (the kind's
    /// own, never a declaration's).
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
        if (builtIn && node.Rejectable) return "rejectable";
        if (builtIn && !node.Skippable) return "skippable";
        return null;
    }
}
