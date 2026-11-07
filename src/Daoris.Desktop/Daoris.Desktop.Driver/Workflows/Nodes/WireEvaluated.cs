using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>What one evaluation came to.</summary>
/// <param name="Statuses">Every node's status after the moves, by node id.</param>
/// <param name="Moves">The moves made, in the order made: invalidations, observations, joins, then eligibility.</param>
/// <param name="Refused">Moves the one table refused, kept apart: none is expected, and each is a defect to name.</param>
/// <param name="Eligibility">Each node's eligibility on the statuses after the invalidations, in declaration order.</param>
/// <param name="Starts">The ready nodes that may start now, in node-id order, within the limit.</param>
/// <param name="Queued">The ready dispatched nodes over the limit, in node-id order: started once a running one settles.</param>
public sealed record WireEvaluated(
    IReadOnlyDictionary<string, NodeStatus> Statuses,
    IReadOnlyList<WireMove> Moves,
    IReadOnlyList<WireMove> Refused,
    IReadOnlyList<WireEligibility> Eligibility,
    IReadOnlyList<WireStart> Starts,
    IReadOnlyList<string> Queued)
{
    /// <summary>
    /// Why the evaluation judged nothing, or null: <see cref="WireEvaluation.NoRevision"/> when the caller's revisions
    /// are not a complete snapshot. A refused evaluation moves nothing, offers no start and returns the statuses as handed in.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>The units the refusal names, in ordinal order: each a unit a node acts on with no revision handed in for it.</summary>
    public IReadOnlyList<string> Missing { get; init; } = [];

    /// <summary>
    /// The nodes no route can start any more given the outcomes reached, in node-id order: one behind a <c>when</c> of another
    /// outcome, an <c>allOf</c> group with such a wire, an <c>anyOf</c> group all of whose wires are, or a data input, transitively. Derived
    /// at each evaluation, never stored; an <c>all</c> join counts such a member as settled.
    /// </summary>
    public IReadOnlyList<string> NotTaken { get; init; } = [];

    /// <summary>
    /// The <c>back</c> wires taken, each keyed as a trigger's firing on its source's receipt of its <c>on</c>: its target is invalidated
    /// for a new attempt, and what rests on the target lapses after it. The record writer keeps their keys with the moves, so one is taken once.
    /// </summary>
    public IReadOnlyList<WireFiring> Backs { get; init; } = [];

    /// <summary>The nodes whose hold's reason this evaluation kept with no move, which the record writer keeps all the same.</summary>
    public IReadOnlyList<string> Held { get; init; } = [];

    /// <summary>
    /// Whether the evaluator moved nothing: evaluated again on the same statuses, it moves nothing. It says nothing of pending starts
    /// (<see cref="Starts"/>, <see cref="Queued"/>) nor of execution finished, and a refused evaluation moved nothing too:
    /// read <see cref="Refusal"/> first.
    /// </summary>
    public bool Settled => Moves.Count == 0;

    /// <summary>The eligibility of <paramref name="node"/>, or null for a node not evaluated.</summary>
    public WireEligibility? Of(string node) => Eligibility.FirstOrDefault(each => each.Node == node);
}
