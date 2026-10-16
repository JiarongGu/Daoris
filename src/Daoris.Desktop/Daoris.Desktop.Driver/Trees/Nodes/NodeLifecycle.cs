namespace Daoris.Driver;

/// <summary>One row of the one table (§E2): from a state, to a state, by whom, and when.</summary>
public sealed record NodeTransition(string From, string To, string By, string When);

/// <summary>
/// Where one node stands on the unit it acts on (FABRIC-NODE1; §E2): its state, and what the lifecycle needs to judge the next move.
/// A value, never a store: an observed built-in's is derived from its records at each read (<see cref="BuiltInNodes"/>), and an
/// attempt's store is the unit's canonical record, kept by the rows that first start one (§E7, <i>unit records keep §D6's attempts</i>).
/// </summary>
/// <param name="Unit">The unit it acts on (<see cref="WorkUnit.Id"/>).</param>
/// <param name="Node">Its declaration's id.</param>
/// <param name="State">One of <see cref="NodeStates"/>.</param>
public sealed record NodeStatus(string Unit, string Node, string State)
{
    /// <summary>The newest attempt's number, from 1; 0 before the first start. It only grows: an acceptance binds it.</summary>
    public int Attempt { get; init; }

    /// <summary>Attempts started since the binding was fresh or the person last asked: what <see cref="NodeRetry.MaxAttempts"/> bounds.</summary>
    public int Tries { get; init; }

    /// <summary>
    /// The attempt whose outcome is not established yet, apart from the visible state: one that went unknown, and a dispatched one
    /// (<see cref="NodeAuthority.Dispatches"/>) from its start until its authority's outcome, whatever moved the node off it (a stop, a
    /// skip, a rejection, an invalidation). No new attempt starts while it is set (FABRIC-NODE1c).
    /// </summary>
    public int? Unreconciled { get; init; }

    /// <summary>
    /// The binding the newest attempt runs on, as the acceptance it would be reached on (no evidence): set at its start from Daoris's
    /// evaluation, and what a reach must match. Kept as history until a reset to ready or blocked (FABRIC-NODE1c).
    /// </summary>
    public NodeAcceptance? Binding { get; init; }

    /// <summary>A skip's graph and run: it holds for those alone (<see cref="NodeLifecycle.Skips"/>).</summary>
    public NodeScope? Scope { get; init; }

    /// <summary>The acceptance it was reached on; kept as history once invalidated (<see cref="Current"/> is null then).</summary>
    public NodeAcceptance? Acceptance { get; init; }

    /// <summary>The source of its last outcome (<see cref="NodeSource"/>).</summary>
    public string? By { get; init; }

    /// <summary>A failure's code.</summary>
    public string? Code { get; init; }

    /// <summary>Whether that failure was declared transient (§D6): only such a failure retries without the person.</summary>
    public bool Transient { get; init; }

    /// <summary>The person's words: a rejection's reason, a skip's, a stop's where given.</summary>
    public string? Words { get; init; }

    /// <summary>The acceptance that counts now: only a reached node's. An invalidated one's is history, never current evidence.</summary>
    public NodeAcceptance? Current => State == NodeStates.Reached ? Acceptance : null;
}

/// <summary>A move asked of a node: the state it goes to and who causes it, with what that cause must bring.</summary>
/// <param name="To">One of <see cref="NodeStates"/>.</param>
/// <param name="By">One of <see cref="NodeCause"/>.</param>
public sealed record NodeMove(string To, string By)
{
    /// <summary>An outcome's source: the kind's authority must name it.</summary>
    public string? Source { get; init; }

    /// <summary>
    /// The attempt an outcome or an unknown is about: required of a reach, a failure, a rejection and an unknown, and refused unless it
    /// is the current attempt, so a late report of an earlier attempt never lands on a newer one (FABRIC-NODE1c).
    /// </summary>
    public int? Attempt { get; init; }

    /// <summary>A reach's bound evidence: it must be the status's <see cref="NodeStatus.Binding"/>, with the evidence its kind needs.</summary>
    public NodeAcceptance? Acceptance { get; init; }

    /// <summary>
    /// A start's binding, as Daoris evaluated it: this unit, this node at its declared version, the next attempt, and the revision,
    /// graph, inputs and prerequisite receipts it runs on. Its evidence and outputs are not read.
    /// </summary>
    public NodeAcceptance? Binding { get; init; }

    /// <summary>A failure's code.</summary>
    public string? Code { get; init; }

    public bool Transient { get; init; }

    /// <summary>The person's words: required for a rejection and a skip.</summary>
    public string? Words { get; init; }

    /// <summary>A skip's graph and run: required, since a skip holds for its own alone.</summary>
    public NodeScope? Scope { get; init; }
}

/// <summary>Where a skip holds (FABRIC-NODE1c): the graph that bound the run, and the run, D157's skip on a run (§E7).</summary>
/// <param name="GraphDigest">The graph's digest.</param>
/// <param name="Run">The run's id.</param>
public sealed record NodeScope(string GraphDigest, string Run);

/// <summary>What a move came to: the node's status after it, or as it was with the refusal's code.</summary>
public sealed record NodeMoved(NodeStatus Status, string? Refused)
{
    public bool Moved => Refused is null;

    /// <summary>
    /// The status as it stood, when the move cleared any of what it held: a binding, an acceptance, an outcome's source, code or words,
    /// a skip's scope. What the record writer keeps as the attempt's history before the reset (§D3: history is kept); null when the
    /// move cleared nothing (FABRIC-NODE1c).
    /// </summary>
    public NodeStatus? Cleared { get; init; }
}

/// <summary>Why a move, a declaration or an observation is refused, as a code a test holds and a page words.</summary>
public static class NodeRefusal
{
    /// <summary>The table holds no move between these states.</summary>
    public const string NotInTable = "not-in-table";

    /// <summary>The table holds the move for another cause: Daoris never reports an outcome, and a person only judges.</summary>
    public const string NotCause = "not-cause";

    /// <summary>The report's source is not the one the kind's authority names: a claim, another plugin, a guess.</summary>
    public const string NotAuthority = "not-authority";

    /// <summary>Nothing may fail this kind: not yet is pending.</summary>
    public const string NoFailureAuthority = "no-failure-authority";

    /// <summary>A person may not reject this node.</summary>
    public const string NotJudged = "not-judged";

    /// <summary>The person may not pass this node over.</summary>
    public const string NotSkippable = "not-skippable";

    /// <summary>A rejection or a skip without the person's words.</summary>
    public const string NoWords = "no-words";

    /// <summary>A failure without its code.</summary>
    public const string NoCode = "no-code";

    /// <summary>A reach without bound evidence.</summary>
    public const string NoAcceptance = "no-acceptance";

    /// <summary>An outcome or an unknown that names no attempt.</summary>
    public const string NoAttempt = "no-attempt";

    /// <summary>The outcome names another attempt: a stale attempt's completion never counts as current evidence (§D3).</summary>
    public const string StaleAttempt = "stale-attempt";

    /// <summary>
    /// The acceptance is not the binding its attempt started on (another unit, node, revision, graph, version, inputs or prerequisite
    /// receipts), or a start's binding is not this node's next attempt at its declared version.
    /// </summary>
    public const string NotBound = "not-bound";

    /// <summary>A start that brings no binding, or a reach on a status that holds none.</summary>
    public const string NoBinding = "no-binding";

    /// <summary>A reach without the evidence reference its kind needs, or with evidence of a schema the node does not declare.</summary>
    public const string NoEvidence = "no-evidence";

    /// <summary>
    /// A reach whose evidence or an output names another acceptance than the reach's own: a consumer's invalidation follows the
    /// acceptance a port names, so it must be the one that produced it (FABRIC-NODE1d).
    /// </summary>
    public const string OtherAcceptance = "other-acceptance";

    /// <summary>
    /// A reach exposing an output its node does not declare: a <c>data</c> wire's schema, checked when saved, would mean nothing at run time
    /// (FABRIC-NODE1e).
    /// </summary>
    public const string UndeclaredOutput = "undeclared-output";

    /// <summary>A reach exposing a declared output at another schema than its declaration's (FABRIC-NODE1e).</summary>
    public const string OutputSchema = "output-schema";

    /// <summary>A reach without one of its node's declared outputs: every declared output is exposed (FABRIC-NODE1e).</summary>
    public const string NoOutput = "no-output";

    /// <summary>A skip that names no graph and run.</summary>
    public const string NoScope = "no-scope";

    /// <summary>The declaration judged is another node's than the status's.</summary>
    public const string NotThisNode = "not-this-node";

    /// <summary>A second, different result for a binding that already has one: one immutable result per binding.</summary>
    public const string OtherResult = "other-result";

    /// <summary>A retry of a failure not declared transient: the person's new attempt is the way on.</summary>
    public const string NotTransient = "not-transient";

    /// <summary>The declared attempts are spent on this binding.</summary>
    public const string NoAttemptsLeft = "no-attempts-left";

    /// <summary>An earlier attempt's unknown outcome is not reconciled yet.</summary>
    public const string Unreconciled = "unreconciled";

    /// <summary>A kind this build does not know.</summary>
    public const string UnknownKind = "unknown-kind";

    /// <summary>The kind is not observed from records: a pressed, performed or waited node is its own row's, a join its members'.</summary>
    public const string NotObserved = "not-observed";

    /// <summary>The kind observes another kind of unit: <c>claimed</c> a quest, git's proofs a branch.</summary>
    public const string NotThisUnit = "not-this-unit";
}

/// <summary>
/// The one lifecycle (FABRIC-NODE1; the fabric events design §E2): the only moves a node of any kind makes, and the checks a move must
/// pass. A kind changes nothing here but who may report its outcomes (<see cref="NodeAuthorities"/>). Pure: it judges a move and
/// returns the next status, and writes nothing.
/// </summary>
/// <remarks>
/// <para><b>Evidence authority.</b> Only the kind's reach authority reaches it, only its failure authority fails it, each naming its
/// <see cref="NodeSource"/>. Daoris never reports an outcome: silence and a deadline never count. A reach carries an acceptance bound to
/// this unit, this node and this attempt; a stale attempt's completion is never current evidence.</para>
/// <para><b>Retries.</b> Daoris retries a failure only where its code was declared transient and the declared attempts are not spent on
/// this binding. The person's new attempt and a fresh binding count afresh; the attempt's number only grows.</para>
/// <para><b>Reconciliation.</b> An unknown outcome is reconciled before any new attempt: a start is refused while one is unreconciled,
/// whatever came between (a stop, the person's ask, an invalidation).</para>
/// </remarks>
public static class NodeLifecycle
{
    /// <summary>§E2's table, its <i>any live state</i> and <i>any bound state</i> rows expanded over <see cref="NodeStates.Live"/> and <see cref="NodeStates.Bound"/>.</summary>
    public static readonly IReadOnlyList<NodeTransition> Table = Build();

    private static List<NodeTransition> Build()
    {
        const string daoris = NodeCause.Daoris, authority = NodeCause.Authority, person = NodeCause.Person;
        List<NodeTransition> rows =
        [
            new(NodeStates.Blocked, NodeStates.Ready, daoris, "its wires, inputs and authority are satisfied"),
            new(NodeStates.Ready, NodeStates.Blocked, daoris, "that stops being true before it starts"),
            new(NodeStates.Ready, NodeStates.Active, daoris, "it starts: observing, dispatching, offering the task, or waiting"),
            new(NodeStates.Active, NodeStates.Reached, authority, "accepted, bound evidence"),
            new(NodeStates.Active, NodeStates.Failed, authority, "an authoritative negative outcome; silence and a deadline never count"),
            new(NodeStates.Active, NodeStates.Rejected, person, "a person's rejection, with a reason, on a node a person may judge"),
            new(NodeStates.Active, NodeStates.Unknown, daoris, "the outcome cannot be established now"),
            new(NodeStates.Unknown, NodeStates.Active, daoris, "reconciliation says it is running, or never started"),
            new(NodeStates.Unknown, NodeStates.Reached, authority, "reconciliation establishes the outcome"),
            new(NodeStates.Unknown, NodeStates.Failed, authority, "reconciliation establishes the outcome"),
            new(NodeStates.Failed, NodeStates.Ready, daoris, "a declared retry: a transient code with attempts left, as a new attempt"),
            new(NodeStates.Failed, NodeStates.Ready, person, "a new attempt the person asks for"),
            new(NodeStates.Rejected, NodeStates.Ready, person, "a new attempt the person asks for"),
            new(NodeStates.Stopped, NodeStates.Ready, person, "a new attempt the person asks for"),
        ];
        foreach (var from in new[] { NodeStates.Ready, NodeStates.Blocked, NodeStates.Active })
        {
            rows.Add(new(from, NodeStates.Skipped, person, "the person passes it over for this graph and run, with their words, where the node allows a skip"));
        }

        // FABRIC-NODE1c: a skip is the person's word for one graph and run, so they may take it back, and another graph or run lapses it.
        rows.Add(new(NodeStates.Skipped, NodeStates.Blocked, person, "the person withdraws their skip: it is judged again"));
        rows.Add(new(NodeStates.Skipped, NodeStates.Invalidated, daoris, "its graph or run is another: a skip holds for its own alone"));

        foreach (var from in NodeStates.Live) rows.Add(new(from, NodeStates.Stopped, person, "the person stops this node"));
        foreach (var from in NodeStates.Bound)
        {
            rows.Add(new(from, NodeStates.Invalidated, daoris, "its binding changed: history is kept, and unresolved attempts are superseded"));
        }

        rows.Add(new(NodeStates.Invalidated, NodeStates.Blocked, daoris, "judged again on a fresh binding"));
        rows.Add(new(NodeStates.Invalidated, NodeStates.Ready, daoris, "judged again on a fresh binding"));
        return rows;
    }

    /// <summary>A node not yet eligible, on the unit it acts on.</summary>
    public static NodeStatus Start(string unit, string node) => new(unit, node, NodeStates.Blocked);

    /// <summary>The delay before attempt <paramref name="attempt"/> (from 2) under the node's retry: its delays in order, the last repeating.</summary>
    public static TimeSpan DelayBefore(NodeDeclaration node, int attempt) =>
        attempt < 2 || node.Retry is not { DelaysSeconds.Count: > 0 } retry
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(retry.DelaysSeconds[Math.Min(attempt - 2, retry.DelaysSeconds.Count - 1)]);

    /// <summary>
    /// An earlier attempt's outcome established after it stopped being current (a stopped, skipped, rejected or superseded attempt's
    /// completion, recorded as a stale attempt's, §D3, or its authority's answer that it never started): it no longer holds a new attempt
    /// back. Any other attempt changes nothing, and so does a current one: an active or unknown attempt's outcome is a move.
    /// </summary>
    public static NodeStatus Reconciled(NodeStatus status, int attempt) =>
        status.Unreconciled == attempt && status.State is not (NodeStates.Active or NodeStates.Unknown) ? status with { Unreconciled = null } : status;

    /// <summary>Whether <paramref name="status"/> is a skip that holds for <paramref name="scope"/>: its own graph and run alone.</summary>
    public static bool Skips(NodeStatus status, NodeScope scope) => status.State == NodeStates.Skipped && status.Scope == scope;

    /// <summary>The move judged against the one table and the kind's authority: the next status, or the status as it was and why not.</summary>
    public static NodeMoved Apply(NodeDeclaration node, NodeStatus status, NodeMove move)
    {
        NodeMoved Refuse(string code) => new(status, code);
        NodeMoved To(NodeStatus next) => new(next, null) { Cleared = Clears(status, next) ? status : null };

        // FABRIC-NODE1c: an outcome or an unknown is about one attempt; a late report of an earlier one never lands on the current one.
        string? Unbound(int? attempt) =>
            attempt is null ? NodeRefusal.NoAttempt : attempt != status.Attempt ? NodeRefusal.StaleAttempt : null;

        var authority = NodeAuthorities.Of(node);
        if (authority is null) return Refuse(NodeRefusal.UnknownKind);
        if (node.Id != status.Node) return Refuse(NodeRefusal.NotThisNode);
        var rows = Table.Where(row => row.From == status.State && row.To == move.To).ToList();
        if (rows.Count == 0) return Refuse(NodeRefusal.NotInTable);
        if (!rows.Any(row => row.By == move.By)) return Refuse(NodeRefusal.NotCause);

        var next = status with { State = move.To };
        switch (move.To)
        {
            case NodeStates.Reached:
                if (move.Source != authority.ReachBy) return Refuse(NodeRefusal.NotAuthority);
                if (Unbound(move.Attempt) is { } reachAttempt) return Refuse(reachAttempt);
                if (move.Acceptance is not { } accepted) return Refuse(NodeRefusal.NoAcceptance);
                if (status.Binding is not { } expected) return Refuse(NodeRefusal.NoBinding);
                // The id is the whole binding: unit, revision, graph, node, version, inputs, receipts and attempt.
                if (accepted.Id != expected.Id) return Refuse(NodeRefusal.NotBound);
                if (authority.Evidenced && accepted.Evidence is null) return Refuse(NodeRefusal.NoEvidence);
                if (node.Evidence is { } schema && accepted.Evidence?.Schema != schema) return Refuse(NodeRefusal.NoEvidence);
                if (Outputs(node, accepted) is { } outputs) return Refuse(outputs);
                // FABRIC-NODE1d: a port names the acceptance that produced it, which is what its consumers' invalidation follows.
                if (Others(accepted.Evidence, accepted.Id) || accepted.Outputs.Values.Any(port => Others(port, accepted.Id))) return Refuse(NodeRefusal.OtherAcceptance);
                return To(next with { Acceptance = Owned(accepted), By = move.Source, Code = null, Transient = false, Unreconciled = null });

            case NodeStates.Failed:
                if (authority.FailureBy is null) return Refuse(NodeRefusal.NoFailureAuthority);
                if (move.Source != authority.FailureBy) return Refuse(NodeRefusal.NotAuthority);
                if (Unbound(move.Attempt) is { } failAttempt) return Refuse(failAttempt);
                if (string.IsNullOrWhiteSpace(move.Code)) return Refuse(NodeRefusal.NoCode);
                return To(next with { By = move.Source, Code = move.Code, Transient = move.Transient, Unreconciled = null });

            case NodeStates.Rejected:
                // The person's word judges the attempt; it establishes no dispatched effect's outcome, so Unreconciled stays.
                if (!authority.PersonJudges) return Refuse(NodeRefusal.NotJudged);
                if (Unbound(move.Attempt) is { } rejectAttempt) return Refuse(rejectAttempt);
                if (string.IsNullOrWhiteSpace(move.Words)) return Refuse(NodeRefusal.NoWords);
                return To(next with { By = NodeSource.Person, Words = move.Words });

            case NodeStates.Skipped:
                if (!authority.PersonSkips) return Refuse(NodeRefusal.NotSkippable);
                if (string.IsNullOrWhiteSpace(move.Words)) return Refuse(NodeRefusal.NoWords);
                if (move.Scope is null) return Refuse(NodeRefusal.NoScope);
                return To(next with { By = NodeSource.Person, Words = move.Words, Scope = move.Scope });

            case NodeStates.Stopped:
                return To(next with { By = NodeSource.Person, Words = move.Words });

            case NodeStates.Unknown:
                if (Unbound(move.Attempt) is { } unknownAttempt) return Refuse(unknownAttempt);
                return To(next with { Unreconciled = status.Attempt });

            case NodeStates.Active when status.State == NodeStates.Unknown:
                // Reconciliation found the same attempt running, or never started (redelivered with its key, §D6): not a new attempt.
                // A dispatched one still running stays unreconciled until its outcome.
                return To(next with { Unreconciled = authority.Dispatches ? status.Attempt : null });

            case NodeStates.Active:
                if (status.Unreconciled is not null) return Refuse(NodeRefusal.Unreconciled);
                if (move.Binding is not { } binding) return Refuse(NodeRefusal.NoBinding);
                if (binding.Unit != status.Unit || binding.Node != status.Node || binding.NodeVersion != node.Version || binding.Attempt != status.Attempt + 1)
                {
                    return Refuse(NodeRefusal.NotBound);
                }

                return To(Reset(next) with
                {
                    Attempt = status.Attempt + 1,
                    Tries = status.Tries + 1,
                    Binding = binding with { Evidence = null, Outputs = NodeAcceptance.NoPorts },
                    Unreconciled = authority.Dispatches ? status.Attempt + 1 : null,
                });

            case NodeStates.Ready when status.State == NodeStates.Failed && move.By == NodeCause.Daoris:
                if (!status.Transient) return Refuse(NodeRefusal.NotTransient);
                if (status.Tries >= (node.Retry?.MaxAttempts ?? 1)) return Refuse(NodeRefusal.NoAttemptsLeft);
                return To(Reset(next));

            case NodeStates.Ready or NodeStates.Blocked when status.State is NodeStates.Invalidated or NodeStates.Skipped:
                // Judged again on a fresh binding, or the person's skip withdrawn: the bound counts afresh.
                return To(Reset(next) with { Tries = 0 });

            case NodeStates.Ready when move.By == NodeCause.Person:
                return To(Reset(next) with { Tries = 0 });

            default:
                // blocked ↔ ready on Daoris's evaluation, and an invalidation, which keeps the binding and acceptance as history.
                return To(next);
        }
    }

    private static bool Others(NodePort? port, string acceptance) => port?.Acceptance is { } named && named != acceptance;

    /// <summary>
    /// FABRIC-NODE1e: what a reach's outputs break of its declaration, or null: an output it does not declare, a declared one at another
    /// schema, a declared one missing. A <c>data</c> wire's schema is checked when saved (WIRE1a), so the outputs a reach exposes are the
    /// declared ones exactly, or that check means nothing at run time.
    /// </summary>
    private static string? Outputs(NodeDeclaration node, NodeAcceptance accepted) =>
        accepted.Outputs.Keys.Any(name => !node.Outputs.ContainsKey(name)) ? NodeRefusal.UndeclaredOutput
        : accepted.Outputs.Any(output => !string.Equals(output.Value.Schema, node.Outputs[output.Key], StringComparison.Ordinal)) ? NodeRefusal.OutputSchema
        : node.Outputs.Keys.Any(name => !accepted.Outputs.ContainsKey(name)) ? NodeRefusal.NoOutput
        : null;

    /// <summary>The acceptance with every port naming it: one the authority handed in without its acceptance is this reach's.</summary>
    private static NodeAcceptance Owned(NodeAcceptance accepted) => accepted with
    {
        Evidence = accepted.Evidence is { } evidence ? evidence with { Acceptance = accepted.Id } : null,
        Outputs = accepted.Outputs.ToDictionary(output => output.Key, output => output.Value with { Acceptance = accepted.Id }, StringComparer.Ordinal),
    };

    /// <summary>A status with nothing of an attempt's record left: what a reset to ready or blocked, and a start, begin from.</summary>
    private static NodeStatus Reset(NodeStatus status) =>
        status with { Binding = null, Acceptance = null, By = null, Code = null, Transient = false, Words = null, Scope = null };

    /// <summary>Whether a move took away anything the status held, which the record writer keeps before it is gone (FABRIC-NODE1c).</summary>
    private static bool Clears(NodeStatus was, NodeStatus next)
    {
        static bool Lost(object? held, object? kept) => held is not null && !Equals(held, kept);
        return Lost(was.Binding, next.Binding) || Lost(was.Acceptance, next.Acceptance) || Lost(was.By, next.By) || Lost(was.Code, next.Code)
            || Lost(was.Words, next.Words) || Lost(was.Scope, next.Scope);
    }
}
