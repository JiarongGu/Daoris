namespace Daoris.Driver;

/// <summary>
/// The ids of the node kinds this build knows: the words the saved files, the page and the terminal use. Each is one
/// unit's (<see cref="NodeKit"/>). A plugin's own node is <c>&lt;plugin&gt;/&lt;node&gt;</c>.
/// </summary>
public static class NodeKinds
{
    /// <summary>The agent's done, on the ledger: a claim, and never the end.</summary>
    public const string Claimed = "claimed";

    /// <summary>
    /// Daoris's own push of the branch the <c>land</c> made: performed through the push act, reached when git's remote-tracking
    /// ref holds the pushed commit. A pull request is no kind of its own: it is a plugin's performed step, <c>&lt;plugin&gt;/pull-request</c>.
    /// </summary>
    public const string Push = "push";

    /// <summary>Git's proof that the line holds the work: the kept end fact, <i>held</i>.</summary>
    public const string Line = "line";

    /// <summary>A person's task.</summary>
    public const string Human = "human";

    /// <summary>The person's look at the work: <i>reviewed</i> or <i>not yet</i>.</summary>
    public const string Look = "look";

    /// <summary>The person's go-ahead.</summary>
    public const string GoAhead = "go-ahead";

    /// <summary>Waiting on other work, through a commitment.</summary>
    public const string WaitUnit = "wait.unit";

    /// <summary>Daoris closing the quest at the work's end.</summary>
    public const string QuestClose = "quest.close";

    /// <summary>A join of a group's members, keeping its identity and failure policy.</summary>
    public const string All = "all";

    /// <summary>The second opinion's gate: observed, before the look and the landing in the protected order.</summary>
    public const string Opinion = "opinion";

    /// <summary>
    /// Where a failure or a refusal stops the run short of its end: a decoded version's <c>stop</c>, the synthetic sink.
    /// Reached on the receipt of the path into it; never a graph's end.
    /// </summary>
    public const string Stop = "stop";

    /// <summary>
    /// The declared gates Daoris runs itself on a verification pipeline's run: performed, no agent session, never skipped by its kind.
    /// </summary>
    public const string Check = "check";

    /// <summary>
    /// The unit's step in the line's integration: its landed branch asked of the one request door and
    /// waited on, reached and failed by the integration record. Declared and judged here, drawn by Current under a rule carrying
    /// <c>integrate</c>; its start is <see cref="IntegrateNode"/>.
    /// </summary>
    public const string Integrate = "integrate";

    /// <summary>
    /// The hold on the work's own requests: observed from the quests the run's sessions published,
    /// placed after <c>claimed</c> and before the landing. A kind and no option of <c>claimed</c>, because an older build reads an option as
    /// readable and runs it with the hold dropped, where it refuses this kind and holds the run whole.
    /// </summary>
    public const string WorkRequests = "work.requests";

    /// <summary>
    /// Another repository asked, as a step: a quest published to <c>to</c> under the request's key
    /// and waited on, reached and failed by that commitment as a <c>wait.unit</c> is.
    /// </summary>
    public const string Request = "request";

    /// <summary>
    /// The unit's landing as a step Daoris performs, 落地: reached when the landing record holds the bound
    /// tip and failed by the landing's own refusal. An older build reads it as no kind and holds the run whole. It takes no <c>accept</c>: it starts once its wires hold.
    /// </summary>
    public const string Land = "land";

    /// <summary>
    /// The person's Accept of the work as a step of its own, placed before a <c>land</c> where a workflow wants one: reached by
    /// the Accept, never skipped, because a skip would read as accepted. A flow with none lands without a press.
    /// </summary>
    public const string Review = "review";

    /// <summary>
    /// The quest's own holds as a step before the landing (<see cref="HoldReading"/>): its done or submission standing, not taken back,
    /// and not held for the person's yes or its evidence's verdict. An older build reads it as no kind and holds the run whole.
    /// </summary>
    public const string QuestHolds = "quest.holds";

    /// <summary>
    /// What the line already holds, as a step before the landing (<see cref="HoldReading"/>): the proof that the work holds a commit
    /// no branch of the person's holds, and that neither the line nor such a branch holds it by content.
    /// </summary>
    public const string ContentHolds = "content.holds";

    /// <summary>
    /// The watch on the pull request the run's graph opened: reached once the answer its plugin gave through <c>work/state</c>
    /// says the request is merged or closed, a squash merge read as merged. Nothing fails it.
    /// </summary>
    public const string Monitor = "monitor";

    /// <summary>
    /// The chain's parts ended on the line once its pull request merged: each part's branch removed through the journal's delete,
    /// its commits kept on a recovery ref, and what it ended written on the landing record, which the keeper and the press read.
    /// </summary>
    public const string Tidy = "tidy";

    public const string Rebase = "rebase";

    /// <summary>A problem a step could not settle, handed to whoever the workflow names, bounded per stop, and returned to its rebase.</summary>
    public const string Resolve = "resolve";

    /// <summary>A commit moved onto a target ref by a declared method, and nothing else.</summary>
    public const string Merge = "merge";
}

/// <summary>
/// The one lifecycle's states. Every kind has these and no others: what differs by kind is only who may report an outcome.
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
    /// The states the person may stop: every state with no settled outcome. An invalidated node
    /// is live, because it is about to be judged again.
    /// </summary>
    public static readonly IReadOnlyList<string> Live = [Blocked, Ready, Active, Unknown, Invalidated];

    /// <summary>
    /// The states a changed binding invalidates: an attempt started or its outcome, each bound
    /// to the binding it ran on. A skip and a stop are the person's word for this work, not for a revision, so a new tip keeps them.
    /// </summary>
    public static readonly IReadOnlyList<string> Bound = [Active, Reached, Failed, Rejected, Unknown];
}

/// <summary>Who causes a move.</summary>
public static class NodeCause
{
    /// <summary>Daoris: its evaluation of wires and inputs, a start, an unknown, an invalidation, a declared retry. Never an outcome.</summary>
    public const string Daoris = "daoris";

    /// <summary>The kind's reach or failure authority, naming its <see cref="NodeSource"/>.</summary>
    public const string Authority = "authority";

    /// <summary>The person, at their doors: a rejection, a skip, a stop, a new attempt.</summary>
    public const string Person = "person";
}

/// <summary>
/// Where an outcome's report comes from. Each kind names the one source that reaches it and the one
/// that fails it; a report from any other source is no outcome. <see cref="Ledger"/> is a claim, which reaches <c>claimed</c> alone.
/// </summary>
public static class NodeSource
{
    /// <summary>Git's proof, as the driver's own records keep it: the landing, the kept end fact.</summary>
    public const string Git = "git";

    /// <summary>A plugin's believed answer or event: the five belief rules passed, bound to the work's commit.</summary>
    public const string Plugin = "plugin";

    /// <summary>The person, at their doors: an approval, <i>reviewed</i>, a yes.</summary>
    public const string Person = "person";

    /// <summary>The landing supervisor the person appointed, whose Accept or Approve reaches a node a person's press reaches.</summary>
    public const string Supervisor = "supervisor";

    /// <summary>A commitment's receipt, or its owner's decline.</summary>
    public const string Commitment = "commitment";

    /// <summary>The close door's write, or its authoritative refusal.</summary>
    public const string CloseDoor = "close-door";

    /// <summary>A join's members: every one reached or skipped, or one failed under the group's policy.</summary>
    public const string Members = "members";

    /// <summary>The agent's own word, the ledger's done: a claim, never another node's evidence.</summary>
    public const string Ledger = "ledger";

    /// <summary>The second opinion's gate as the driver keeps it: an answer covering the tip with no dispute open, or the person's answer in its place.</summary>
    public const string Opinion = "opinion";

    /// <summary>The review's gate as the driver keeps it: the choice of the chain or the ask that turns the look off for this work.</summary>
    public const string Review = "review";

    /// <summary>The landing record: no branch landed for the work to open a pull request on, or nothing landed at all.</summary>
    public const string Landing = "landing";

    /// <summary>The path into a sink: the failure or the refusal whose receipt leads there, as a join's members reach it.</summary>
    public const string Path = "path";

    /// <summary>
    /// The exits of the declared gates as Daoris's executor runs them: what reaches and
    /// fails a <c>check</c>.
    /// </summary>
    public const string Gates = "gates";

    /// <summary>
    /// The integration record: what reaches and fails an <c>integrate</c>, its member promoted and
    /// settled, or handed back, as the line's run writes it. Never a person's word and never a plugin's.
    /// </summary>
    public const string Integration = "integration";

    /// <summary>
    /// The quests' records: what reaches a <c>work.requests</c>, each quest the run's sessions published
    /// here done, as the exchange keeps it. A decline reaches nothing and fails nothing: it holds the node active and waiting on the person.
    /// </summary>
    public const string Requests = "requests";

    /// <summary>The state the step a <c>monitor</c> watches reports through its own reading (<see cref="IWatchable"/>).</summary>
    public const string Watched = "watched";

    /// <summary>
    /// What Daoris reads at the bound tip for a hold before the landing: the quest's record, and git's proof of what the line and the
    /// person's branches hold. A hold that does not let go reaches nothing and fails nothing: it keeps the node active with its reason.
    /// </summary>
    public const string Holds = "holds";
}

/// <summary>
/// An immutable reference a port carries: a consumer receives this, never a copied payload or a
/// file path, and a resolver checks permission for itself.
/// </summary>
/// <param name="Schema">What it is, by schema and version (<c>acme.deploy/receipt@1</c>).</param>
/// <param name="Ref">Where its producer keeps it, by record and name: never a machine path.</param>
/// <param name="Revision">The revision it is made at.</param>
/// <param name="Acceptance">The acceptance that produced it (<see cref="NodeAcceptance.Id"/>): what invalidates its consumers.</param>
public sealed record NodePort(string Schema, string Ref, string? Revision, string? Acceptance)
{
    /// <summary>The node and attempt that made a value (<see cref="WireValues"/>); null on any other port. Not part of a binding's id.</summary>
    public NodeProducer? Producer { get; init; }

    /// <summary>A <see cref="WireValues.Commit"/>'s base: the target's tip a replay made it onto, which a merge compares the target with.</summary>
    public string? Onto { get; init; }

    /// <summary>A <see cref="WireValues.Problem"/>'s stop, by its identity; null on any other port.</summary>
    public NodeStop? Stop { get; init; }
}

/// <summary>
/// Who may report each outcome of a kind: the one thing that differs between kinds. Every other part of the
/// lifecycle is the same for all of them.
/// </summary>
/// <param name="Reach">The kind's reach; a plugin's own node takes its declaration's.</param>
/// <param name="ReachBy">The <see cref="NodeSource"/> whose report reaches it.</param>
/// <param name="FailureBy">The source whose report fails it, or null: not yet is pending, never failed.</param>
/// <param name="PersonJudges">Whether a person may reject it, at their doors.</param>
/// <param name="PersonSkips">Whether the person may pass it over for this work.</param>
/// <param name="MayEnd">Whether it may be a graph's end: <c>claimed</c> never is.</param>
/// <param name="Evidenced">
/// Whether a reach must carry an evidence reference: every record's or answer's kind does. The
/// person's kinds do not, because their word is the outcome, nor a join, whose receipts are its members'.
/// </param>
public sealed record NodeAuthority(string Kind, string Reach, string ReachBy, string? FailureBy, bool PersonJudges, bool PersonSkips, bool MayEnd, bool Evidenced)
{
    /// <summary>
    /// Whether its start sends an effect outside Daoris (a performed or a waited reach): until its authority's outcome or a
    /// reconciliation establishes how that attempt ended, no new attempt starts, whatever moved the node off it.
    /// </summary>
    public bool Dispatches => Reach is NodeReach.Performed or NodeReach.Waited;

    /// <summary>
    /// Whether a plugin's frame, once the belief rules hold, is itself the reach: a
    /// plugin's own node alone, its evidence kept as the plugin's word. <c>pushed</c> needs Daoris's own reading of git's
    /// remote-tracking ref beside the plugin's answer, and <c>pull-request</c> the plugin's answer to Daoris's own ask (<c>work/state</c>, with
    /// its checks) kept on the landing, so a frame naming either is never their outcome; nor is it git's, a person's or a record's.
    /// </summary>
    public bool OnPluginWord => ReachBy == NodeSource.Plugin && NodeKit.IsPlugin(Kind);

    /// <summary>
    /// Whether the person may settle an attempt whose outcome nothing else establishes: a plugin's performed node alone, whose attempt the performer holds when reconciliation cannot say. Its
    /// <c>unknown → reached</c> and <c>unknown → failed</c> by the person are the person's word, never the plugin's (<see cref="NodeSource.Person"/>).
    /// </summary>
    public bool PersonSettles => Reach == NodeReach.Performed && NodeKit.IsPlugin(Kind);

    /// <summary>
    /// The source whose record passes the node over for its attempt's graph and run, where nothing is asked of it: an
    /// <c>opinion</c>'s gate, whose rule asks none here or an optional one none could be had, read as skipped; and
    /// a <c>look</c>'s review gate, where the chain's or the ask's choice turns it off, and a <c>pull-request</c>'s landing
    /// record, where no branch landed to open one on. Null for every other kind, which the person alone passes over. Never the person's skip: it carries no words, and its source is the record's.
    /// </summary>
    public string? SkipBy { get; init; }

    /// <summary>
    /// The source that reaches a pressed node by what it observes, beside its press: a <c>review</c> whose landing is seen made, the
    /// landing record's (git's), so a reach nobody pressed says it was observed and is never the person's. Null for every other kind.
    /// </summary>
    public string? ObservedBy { get; init; }
}

/// <summary>The authority by kind: each kind's unit's (<see cref="INodeKind.Authority"/>), a plugin's own node's from its declaration.</summary>
/// <remarks>
/// <para><b>Skips</b>: git's proofs
/// (<c>claimed</c>, <c>line</c>) are never skipped, because <i>held</i> and the cleanup rest on them and a skip would read
/// as evidence; nor is <c>quest.close</c>, whose override is the person's own done, nor a join, whose members are skipped
/// instead, nor a <c>stop</c>, which is the run stopped. Every other kind may be, and a plugin's own node unless
/// its declaration forbids it.</para>
/// </remarks>
public static class NodeAuthorities
{
    /// <summary>The node's authority as this build reads it; null for a kind it does not know.</summary>
    public static NodeAuthority? Of(NodeDeclaration node) => Of(node, NodeKit.Built);

    /// <summary>
    /// The node's authority as a build whose kit is <paramref name="kit"/> reads it: an older build's lacks a later kind, so a declaration naming
    /// that kind is no kind there.
    /// </summary>
    internal static NodeAuthority? Of(NodeDeclaration node, NodeKit kit) => kit.Find(node.Kind)?.Authority(node);
}


/// <summary>A declaration's own checks, validated before saved: the node's half, the wires' half being the wires'.</summary>
public static class NodeDeclarations
{
    /// <summary>
    /// What does not hold, by its field, or null: a kind this build does not know (<see cref="NodeRefusal.UnknownKind"/>), a reach not
    /// the kind's (a plugin's is observed or performed), a version below 1, a retry of fewer than one attempt or on a kind nothing can
    /// fail, a timeout not above zero, an output named <c>evidence</c> (the evidence port's name, <see cref="WireEnd.Evidence"/>), or a
    /// rejection a built-in's declaration tries to grant or a skip it tries to forbid (the kind's own, never a declaration's), or outcomes on a
    /// built-in whose kind is reached with none (<see cref="BuiltInNode.Outcomes"/>), or any that break <see cref="NodeOutcomes.Problem"/>. Then the kind's own checks (<see cref="INodeKind.Problem"/>).
    /// Then a <c>perform</c> on any node but a plugin's performed one, a transient code not of
    /// <see cref="NodePerform.CodeShape"/> or named twice, transient codes with no retry to take them, and a plugin's retry of more
    /// than one attempt that nothing could take: a transient code, on a node that is idempotent or reconciled. The graph's rules about where a
    /// node stands are <see cref="NodeGraphs.Problems"/>'s.
    /// </summary>
    public static string? Problem(NodeDeclaration node) => Problem(node, NodeKit.Built);

    /// <summary>The declaration's checks as a build whose kit is <paramref name="kit"/> makes them.</summary>
    internal static string? Problem(NodeDeclaration node, NodeKit kit)
    {
        if (string.IsNullOrWhiteSpace(node.Id)) return "id";
        if (kit.Find(node.Kind) is not { } unit) return NodeRefusal.UnknownKind;
        var authority = unit.Authority(node);
        var builtIn = unit is BuiltInNode;
        if (builtIn ? node.Reach != authority.Reach : node.Reach is not (NodeReach.Observed or NodeReach.Performed)) return "reach";
        if (node.Version < 1) return "version";
        if (node.Retry is { } retry && (retry.MaxAttempts < 1 || retry.DelaysSeconds.Any(delay => delay < 0) || authority.FailureBy is null)) return "retry";
        if (node.TimeoutSeconds is <= 0) return "timeoutSeconds";
        // A data wire names a node's evidence by this port, so no output may take its name.
        if (node.Outputs.ContainsKey(WireEnd.Evidence)) return "outputs";
        if (builtIn && node.Rejectable) return "rejectable";
        if (builtIn && !node.Skippable) return "skippable";
        // Outcomes keep the names a route is followed by; a built-in declares them only where its kind takes them (a monitor, a
        // person's task's own set).
        if (node.Outcomes.Count > 0 && (unit is BuiltInNode kind ? !kind.TakesOutcomes(node.Outcomes) : NodeOutcomes.Problem(node.Outcomes) is not null)) return "outcomes";
        if (unit.Problem(node) is { } own) return own;

        // How a plugin performs its node is that node's alone, and a retry or a transient code is refused where
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
