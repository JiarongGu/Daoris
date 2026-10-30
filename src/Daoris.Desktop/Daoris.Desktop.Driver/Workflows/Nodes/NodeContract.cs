namespace Daoris.Driver;

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

    /// <summary>
    /// The declared gates Daoris runs itself on a verification pipeline's run (FABRIC-PIPELINE1a, D192 point 6, the pipeline design
    /// §4.1): performed, no agent session, never skipped by its kind. Declared and judged here; row 1d performs it, so until then it is
    /// started by nothing.
    /// </summary>
    public const string Check = "check";

    /// <summary>
    /// The unit's step in the line's integration (INTEGRATE1n; the integration design §17.4): its landed branch asked of the one request door and
    /// waited on, reached and failed by the integration record. Declared and judged here, drawn by Current under a rule carrying
    /// <c>integrate</c>; its start is <see cref="IntegrateNode"/>, and nothing runs the integration until row g.
    /// </summary>
    public const string Integrate = "integrate";

    /// <summary>
    /// The hold on the work's own requests (INTEGRATE1o; the integration design §17.3): observed from the quests the run's sessions published,
    /// placed after <c>claimed</c> and before the landing. A kind and no option of <c>claimed</c>, since an older build reads an option as
    /// readable and runs it with the hold dropped, where it refuses this kind and holds the run whole (§17.6 point 8).
    /// </summary>
    public const string WorkRequests = "work.requests";

    public static readonly IReadOnlyList<string> BuiltIn = [Claimed, Handed, Pushed, PullRequest, Line, Human, Look, GoAhead, WaitUnit, QuestClose, All, Opinion, Stop, Check, Integrate, WorkRequests];

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

    /// <summary>The review's gate as the driver keeps it (D154): the choice of the chain or the ask that turns the look off for this work (FABRIC-WIRE1h).</summary>
    public const string Review = "review";

    /// <summary>The landing record: no branch was landed for the work to open a pull request on, or nothing was landed at all (FABRIC-WIRE1h).</summary>
    public const string Landing = "landing";

    /// <summary>The path into a sink (§E7): the failure or the refusal whose receipt leads there, as a join's members reach it.</summary>
    public const string Path = "path";

    /// <summary>
    /// The exits of the declared gates as Daoris's executor ran them (FABRIC-PIPELINE1a, the pipeline design §4.1): what reaches and
    /// fails a <c>check</c>.
    /// </summary>
    public const string Gates = "gates";

    /// <summary>
    /// The integration record (INTEGRATE1n; §17.4's <i>its authority</i>): what reaches and fails an <c>integrate</c>, its member promoted and
    /// settled, or handed back, as the line's run writes it (§5.6). Never a person's word and never a plugin's.
    /// </summary>
    public const string Integration = "integration";

    /// <summary>
    /// The quests' records (INTEGRATE1o; §17.3's <i>its authority</i>): what reaches a <c>work.requests</c>, each quest the run's sessions published
    /// here done, as the exchange keeps it. A decline reaches nothing and fails nothing: it holds the node active and waiting on the person.
    /// </summary>
    public const string Requests = "requests";
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

    /// <summary>
    /// Whether a plugin's frame, once §C4's belief rules hold, is itself the reach (FABRIC-PERFORM1c; §C4's trust, §E2's authority by kind): a
    /// plugin's own node alone, its evidence kept as the plugin's word (D143). <c>pushed</c> needs Daoris's own reading of git's
    /// remote-tracking ref beside the plugin's answer, and <c>pull-request</c> the plugin's answer to Daoris's own ask (<c>work/state</c>, with
    /// D148 point 4's checks) kept on the landing, so a frame naming either is never their outcome; nor is it git's, a person's or a record's.
    /// </summary>
    public bool OnPluginWord => ReachBy == NodeSource.Plugin && NodeKinds.IsPlugin(Kind);

    /// <summary>
    /// Whether the person may settle an attempt whose outcome nothing else establishes (FABRIC-PERFORM1g; §D6, <i>unknown stops the step,
    /// and the person sees it</i>): a plugin's performed node alone, whose attempt the performer holds when reconciliation cannot say. Its
    /// <c>unknown → reached</c> and <c>unknown → failed</c> by the person are the person's word, never the plugin's (<see cref="NodeSource.Person"/>).
    /// </summary>
    public bool PersonSettles => Reach == NodeReach.Performed && NodeKinds.IsPlugin(Kind);

    /// <summary>
    /// The source whose record passes the node over for its attempt's graph and run, where nothing is asked of it (FABRIC-WIRE1g): an
    /// <c>opinion</c>'s gate, whose rule asks none here or an optional one none could be had, read as skipped as D157's run reads it; and
    /// (FABRIC-WIRE1h) a <c>look</c>'s review gate, where the chain's or the ask's choice turns it off, and a <c>pull-request</c>'s landing
    /// record, where no branch landed to open one on. Null for every other kind, which the person alone passes over. Never the person's skip: it carries no words, and its source is the record's.
    /// </summary>
    public string? SkipBy { get; init; }
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
        // FABRIC-WIRE1h: the landing record passes it over where the work went into the line with no branch landed to open one on.
        [NodeKinds.PullRequest] = new(NodeKinds.PullRequest, NodeReach.Observed, NodeSource.Plugin, NodeSource.Plugin, false, true, true, true) { SkipBy = NodeSource.Landing },
        [NodeKinds.Human] = new(NodeKinds.Human, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false),
        // §E4: the look is its gate, observed, and the person's *reviewed* is what reaches it.
        // FABRIC-WIRE1h: the review gate's record passes it over where the chain's or the ask's choice turns it off for this work.
        [NodeKinds.Look] = new(NodeKinds.Look, NodeReach.Observed, NodeSource.Person, null, true, true, true, false) { SkipBy = NodeSource.Review },
        [NodeKinds.GoAhead] = new(NodeKinds.GoAhead, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false),
        [NodeKinds.WaitUnit] = new(NodeKinds.WaitUnit, NodeReach.Waited, NodeSource.Commitment, NodeSource.Commitment, false, true, true, true),
        [NodeKinds.QuestClose] = new(NodeKinds.QuestClose, NodeReach.Performed, NodeSource.CloseDoor, NodeSource.CloseDoor, false, false, true, true),
        [NodeKinds.All] = new(NodeKinds.All, NodeReach.Observed, NodeSource.Members, NodeSource.Members, false, false, true, false),
        // FABRIC-WORKFLOW2: D157's opinion never fails (a required one waits), and the person may pass it over, as its gate's
        // unavailable reads as skipped today. A sink is the run stopped: never skipped, never the end, its receipt its path's.
        // FABRIC-WIRE1g: the gate's own record passes it over where its rule asks none, or an optional one none could be had.
        [NodeKinds.Opinion] = new(NodeKinds.Opinion, NodeReach.Observed, NodeSource.Opinion, null, false, true, true, true) { SkipBy = NodeSource.Opinion },
        [NodeKinds.Stop] = new(NodeKinds.Stop, NodeReach.Observed, NodeSource.Path, null, false, false, MayEnd: false, Evidenced: false),
        // FABRIC-PIPELINE1a (the pipeline design §4.1): reached and failed by the gates' exits, never skipped by its kind, since a skip
        // would read as a verdict a stage and the line rest on; the person's doors on it are a new attempt and a stop.
        [NodeKinds.Check] = new(NodeKinds.Check, NodeReach.Performed, NodeSource.Gates, NodeSource.Gates, false, false, true, true),
        // INTEGRATE1n (§17.4): reached and failed by the integration record, never skipped, since a skip satisfies `after` and would read as
        // integrated where nothing was; the person's door is the stop, which the record bounds (F50, row n2).
        [NodeKinds.Integrate] = new(NodeKinds.Integrate, NodeReach.Waited, NodeSource.Integration, NodeSource.Integration, false, PersonSkips: false, true, true),
        // INTEGRATE1o (§17.3, C28): reached by the quests' records, each the run's sessions published here done. A decline holds it active and
        // waiting on the person, so nothing fails it; the person's pass with words is the run's existing skip, and no record passes it over.
        [NodeKinds.WorkRequests] = new(NodeKinds.WorkRequests, NodeReach.Observed, NodeSource.Requests, null, false, PersonSkips: true, true, true),
    };

    /// <summary>
    /// The node's authority: its built-in kind's, or a plugin's own (its believed answer either way, a person only where declared, a
    /// skip unless declared otherwise); null for a kind this build does not know. A plugin's answer is named <see cref="NodeSource.Plugin"/>
    /// here alone: that the answer came from the declaring plugin is <see cref="WorkBelief.Completion"/>'s sender check, not the lifecycle's.
    /// </summary>
    public static NodeAuthority? Of(NodeDeclaration node) => Of(node, BuiltIn);

    /// <summary>
    /// The node's authority as a build whose built-in table is <paramref name="table"/> reads it: an older build's table lacks a later row, so
    /// a declaration naming that kind is no kind there (INTEGRATE1n; §17.6 point 6).
    /// </summary>
    internal static NodeAuthority? Of(NodeDeclaration node, IReadOnlyDictionary<string, NodeAuthority> table) =>
        table.TryGetValue(node.Kind, out var builtIn) ? builtIn
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
    /// than one attempt that nothing could take: a transient code, on a node that is idempotent or reconciled. Then INTEGRATE1n's: an
    /// <c>integrate</c>'s <c>request</c> and <c>conflicts</c> (the unit's half of the install's policy, §17.4) each a text of the install's own
    /// choices, judged here with the declaration so a cycle, a version and a stored file are held to one check; the graph's rules about where it
    /// stands are <see cref="NodeGraphs.Problems"/>'s.
    /// </summary>
    public static string? Problem(NodeDeclaration node) => Problem(node, NodeAuthorities.BuiltIn);

    /// <summary>The option name round 4 gave <c>claimed</c> for the hold on the work's requests, refused by name (INTEGRATE1o, C28): the hold is <see cref="NodeKinds.WorkRequests"/>.</summary>
    private const string WorkRequestsOption = "requests";

    /// <summary>The declaration's checks as a build whose built-in table is <paramref name="table"/> makes them (INTEGRATE1n, §17.6 point 6).</summary>
    internal static string? Problem(NodeDeclaration node, IReadOnlyDictionary<string, NodeAuthority> table)
    {
        if (string.IsNullOrWhiteSpace(node.Id)) return "id";
        var authority = NodeAuthorities.Of(node, table);
        if (authority is null) return NodeRefusal.UnknownKind;
        var builtIn = table.ContainsKey(node.Kind);
        if (builtIn ? node.Reach != authority.Reach : node.Reach is not (NodeReach.Observed or NodeReach.Performed)) return "reach";
        if (node.Version < 1) return "version";
        if (node.Retry is { } retry && (retry.MaxAttempts < 1 || retry.DelaysSeconds.Any(delay => delay < 0) || authority.FailureBy is null)) return "retry";
        if (node.TimeoutSeconds is <= 0) return "timeoutSeconds";
        // FABRIC-WIRE1a: a data wire names a node's evidence by this port, so no output may take its name.
        if (node.Outputs.ContainsKey(WireEnd.Evidence)) return "outputs";
        if (builtIn && node.Rejectable) return "rejectable";
        if (builtIn && !node.Skippable) return "skippable";
        if (node.Kind == NodeKinds.Integrate && IntegrateOptionProblem(node) is { } option) return option;
        // INTEGRATE1o (§17.6 point 8, C28): the hold on the work's requests is the kind `work.requests`. Written as an option of `claimed`, a build
        // that knows `claimed` would read it and run the work with the hold dropped, so no file may say it there.
        if (node.Kind == NodeKinds.Claimed && node.Options.ContainsKey(WorkRequestsOption)) return WorkRequestsOption;

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

    /// <summary>
    /// The first of an <c>integrate</c>'s options that holds no choice of the install's, by its name (INTEGRATE1n; §17.4): <c>request</c> is
    /// one of <see cref="LandingIntegrate.Requests"/> and <c>conflicts</c> one of <see cref="LandingIntegrate.ConflictChoices"/>, each written as
    /// text. Any other option is left as written, as a graph's options are opaque to a reader that does not know them.
    /// </summary>
    private static string? IntegrateOptionProblem(NodeDeclaration node)
    {
        foreach (var (name, choices) in new (string, IReadOnlyList<string>)[] { ("request", LandingIntegrate.Requests), ("conflicts", LandingIntegrate.ConflictChoices) })
        {
            if (node.Options.ContainsKey(name) && !(NodeOptions.Text(node, name) is { } text && choices.Contains(text, StringComparer.Ordinal))) return name;
        }

        return null;
    }
}
