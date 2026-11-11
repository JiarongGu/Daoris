namespace Daoris.Driver;

/// <summary>
/// What an observed built-in node's records say: an outcome by its authority, or none yet. Applied through the
/// same one table as any node's report (<see cref="Move"/>), so a built-in is a node and no second mechanism.
/// </summary>
/// <param name="Outcome">
/// <see cref="NodeStates.Reached"/>, <see cref="NodeStates.Failed"/> or <see cref="NodeStates.Unknown"/>, or <see cref="NodeStates.Skipped"/> where
/// the kind's record passes it over (an opinion's gate, <see cref="OpinionNode"/>); null where the records hold no outcome yet,
/// which is pending, never failed.
/// </param>
/// <param name="Said">What the records say, in a clause.</param>
public sealed record NodeObservation(string Unit, string Node, string Kind, string? Outcome, string Said)
{
    /// <summary>The outcome's source, which the kind's authority must name.</summary>
    public string? Source { get; init; }

    /// <summary>The attempt it is observed for: its outcome is that attempt's, never a later one's.</summary>
    public int Attempt { get; init; }

    /// <summary>A reach's acceptance, bound to the commit its evidence names and pointing at its record.</summary>
    public NodeAcceptance? Acceptance { get; init; }

    /// <summary>A failure's code.</summary>
    public string? Code { get; init; }

    /// <summary>Why nothing is observed: <see cref="NodeRefusal.NotObserved"/> or <see cref="NodeRefusal.NotThisUnit"/>.</summary>
    public string? Refused { get; init; }

    /// <summary>
    /// Who made the outcome where the record keeps the person's own word (a look's <i>not yet</i> or skip, a go-ahead refused):
    /// <see cref="NodeCause.Person"/>; null for an outcome by the kind's authority, or an unknown by Daoris.
    /// </summary>
    public string? By { get; init; }

    /// <summary>Words kept with an outcome: the line's proof, or the person's reason.</summary>
    public string? Words { get; init; }

    /// <summary>Why a hold keeps its node active, kept on the node's record (<see cref="NodeStatus.Holds"/>); null where it holds nothing or is no hold.</summary>
    public string? Holds { get; init; }

    /// <summary>The report as a move on an active node: an outcome by its authority, an unknown by Daoris; none where there is no outcome.</summary>
    public NodeMove? Move => Refused is not null || Outcome is null
        ? null
        : new NodeMove(Outcome, By ?? (Outcome == NodeStates.Unknown ? NodeCause.Daoris : NodeCause.Authority))
        {
            Source = Source, Attempt = Attempt, Acceptance = Acceptance, Code = Code, Words = Words,
        };
}
