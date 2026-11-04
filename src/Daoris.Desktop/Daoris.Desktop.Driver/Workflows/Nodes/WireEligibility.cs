using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// One node's eligibility as evaluated (§E2's blocked → ready): whether its groups and data inputs are satisfied, the witness of each
/// <c>anyOf</c> group, the receipts it would consume, and the binding a start takes.
/// </summary>
public sealed record WireEligibility(string Node, bool Eligible)
{
    /// <summary>Each <c>anyOf</c> group's witness wire, by group id: one, by wire-id order when several qualify at once.</summary>
    public IReadOnlyDictionary<string, string> Witnesses { get; init; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>The receipts its groups consume, in ordinal order: every <c>allOf</c> member's, each <c>anyOf</c>'s witness's.</summary>
    public IReadOnlyList<string> Receipts { get; init; } = [];

    /// <summary>The wires not satisfied, in wire-id order: what it waits on.</summary>
    public IReadOnlyList<string> Waiting { get; init; } = [];

    /// <summary>
    /// The binding its next attempt starts on, while eligible on a subject it can be judged on: its inputs from <c>data</c> wires and its
    /// receipts, and the subject it observes where that is not its record's unit.
    /// </summary>
    public NodeAcceptance? Binding { get; init; }

    /// <summary>
    /// Why it is offered no start whatever its wires say (FABRIC-CUTOVER1a): <see cref="WireEvaluation.NoSubject"/>, no subject found for it this
    /// pass, or <see cref="WireEvaluation.NoRevision"/>, its branch subject's revision not handed in; null where it is judged on its subject.
    /// </summary>
    public string? Withheld { get; init; }

    /// <summary>What a <c>wait.unit</c>'s <c>wait</c> wire names, for its adapter (WAITUNIT1): never an eligibility condition.</summary>
    public WaitTarget? Waits { get; init; }

    /// <summary>The trigger firings a start on <see cref="Binding"/> makes: each trigger receipt it consumes that has not fired yet.</summary>
    public IReadOnlyList<WireFiring> Firings { get; init; } = [];
}
