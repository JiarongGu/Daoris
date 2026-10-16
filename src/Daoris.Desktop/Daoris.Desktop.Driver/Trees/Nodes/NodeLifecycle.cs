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

    /// <summary>The attempt whose outcome went unknown and is not established yet: no new attempt starts while it is set.</summary>
    public int? Unreconciled { get; init; }

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

    /// <summary>A reach's bound evidence: this unit, this node, this attempt.</summary>
    public NodeAcceptance? Acceptance { get; init; }

    /// <summary>A failure's code.</summary>
    public string? Code { get; init; }

    public bool Transient { get; init; }

    /// <summary>The person's words: required for a rejection and a skip.</summary>
    public string? Words { get; init; }
}

/// <summary>What a move came to: the node's status after it, or as it was with the refusal's code.</summary>
public sealed record NodeMoved(NodeStatus Status, string? Refused)
{
    public bool Moved => Refused is null;
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

    /// <summary>The acceptance names another attempt: a stale attempt's completion never counts as current evidence (§D3).</summary>
    public const string StaleAttempt = "stale-attempt";

    /// <summary>The acceptance names another unit or node.</summary>
    public const string NotBound = "not-bound";

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
            rows.Add(new(from, NodeStates.Skipped, person, "the person passes it over for this work, with their words, where the node allows a skip"));
        }

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
    /// An earlier attempt's outcome established after it stopped being current (a stopped or superseded attempt's completion, recorded as
    /// a stale attempt's, §D3): it no longer holds a new attempt back. Any other attempt changes nothing.
    /// </summary>
    public static NodeStatus Reconciled(NodeStatus status, int attempt) =>
        status.Unreconciled == attempt ? status with { Unreconciled = null } : status;

    /// <summary>The move judged against the one table and the kind's authority: the next status, or the status as it was and why not.</summary>
    public static NodeMoved Apply(NodeDeclaration node, NodeStatus status, NodeMove move)
    {
        NodeMoved Refuse(string code) => new(status, code);

        var authority = NodeAuthorities.Of(node);
        if (authority is null) return Refuse(NodeRefusal.UnknownKind);
        var rows = Table.Where(row => row.From == status.State && row.To == move.To).ToList();
        if (rows.Count == 0) return Refuse(NodeRefusal.NotInTable);
        if (!rows.Any(row => row.By == move.By)) return Refuse(NodeRefusal.NotCause);

        var next = status with { State = move.To };
        switch (move.To)
        {
            case NodeStates.Reached:
                if (move.Source != authority.ReachBy) return Refuse(NodeRefusal.NotAuthority);
                if (move.Acceptance is not { } accepted) return Refuse(NodeRefusal.NoAcceptance);
                if (accepted.Unit != status.Unit || accepted.Node != status.Node) return Refuse(NodeRefusal.NotBound);
                if (accepted.Attempt != status.Attempt) return Refuse(NodeRefusal.StaleAttempt);
                return new(next with { Acceptance = accepted, By = move.Source, Code = null, Transient = false, Unreconciled = null }, null);

            case NodeStates.Failed:
                if (authority.FailureBy is null) return Refuse(NodeRefusal.NoFailureAuthority);
                if (move.Source != authority.FailureBy) return Refuse(NodeRefusal.NotAuthority);
                if (string.IsNullOrWhiteSpace(move.Code)) return Refuse(NodeRefusal.NoCode);
                return new(next with { By = move.Source, Code = move.Code, Transient = move.Transient, Unreconciled = null }, null);

            case NodeStates.Rejected:
                if (!authority.PersonJudges) return Refuse(NodeRefusal.NotJudged);
                if (string.IsNullOrWhiteSpace(move.Words)) return Refuse(NodeRefusal.NoWords);
                return new(next with { By = NodeSource.Person, Words = move.Words }, null);

            case NodeStates.Skipped:
                if (!authority.PersonSkips) return Refuse(NodeRefusal.NotSkippable);
                if (string.IsNullOrWhiteSpace(move.Words)) return Refuse(NodeRefusal.NoWords);
                return new(next with { By = NodeSource.Person, Words = move.Words }, null);

            case NodeStates.Stopped:
                return new(next with { By = NodeSource.Person, Words = move.Words }, null);

            case NodeStates.Unknown:
                return new(next with { Unreconciled = status.Attempt }, null);

            case NodeStates.Active when status.State == NodeStates.Unknown:
                // Reconciliation found the same attempt running, or never started (redelivered with its key, §D6): not a new attempt.
                return new(next with { Unreconciled = null }, null);

            case NodeStates.Active:
                if (status.Unreconciled is not null) return Refuse(NodeRefusal.Unreconciled);
                return new(next with { Attempt = status.Attempt + 1, Tries = status.Tries + 1, Acceptance = null, Code = null, Transient = false, Words = null }, null);

            case NodeStates.Ready when status.State == NodeStates.Failed && move.By == NodeCause.Daoris:
                if (!status.Transient) return Refuse(NodeRefusal.NotTransient);
                if (status.Tries >= (node.Retry?.MaxAttempts ?? 1)) return Refuse(NodeRefusal.NoAttemptsLeft);
                return new(next with { Code = null, Transient = false }, null);

            case NodeStates.Ready or NodeStates.Blocked when status.State is NodeStates.Invalidated:
                return new(next with { Tries = 0, Acceptance = null, By = null, Code = null, Transient = false, Words = null }, null);

            case NodeStates.Ready when move.By == NodeCause.Person:
                return new(next with { Tries = 0, Code = null, Transient = false, Words = null }, null);

            default:
                // blocked ↔ ready on Daoris's evaluation, and an invalidation, which keeps the acceptance as history.
                return new(next, null);
        }
    }
}
