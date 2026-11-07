namespace Daoris.Driver;

/// <summary>What became of a failed turn's words, as the agent layer stamps it per spawn: carried, never read for policy.</summary>
public enum FailureDelivery
{
    /// <summary>A tool call or its result is seen before the failure.</summary>
    Uncertain,

    /// <summary>Nothing is seen.</summary>
    NeverSent,

    /// <summary>Something other than a tool step is seen.</summary>
    Taken,
}

/// <summary>
/// A bound kind's failure, already read: the agent layer's reading of its own fields translated
/// into Daoris's outcome, which <see cref="AgentErrors.OfNormalized"/> answers with no table and no word read again. Built only in
/// <c>Agents/Layer/</c> (<c>LayerCensusTests</c>), so a raw failure cannot reach the trusted entries and a normalized one never reaches
/// the tables.
/// </summary>
/// <param name="Failure">Kept for the log: the bound door's transport word, the layer's kind word, the terminal's own text and the phase.</param>
/// <param name="By">What decided it, <c>layer:</c> and the layer's entry: the log's and the tests' name, which no reader reads policy from.</param>
/// <param name="Limit">A limit's reading, whole, under its bounds; null for every other kind.</param>
/// <param name="Access">An access failure's evidence, decoded apart; null where none is stated.</param>
/// <param name="Restriction">The restriction, by subject and cause before its recovery.</param>
public sealed record NormalizedFailure(
    AgentErrorKind Kind, DoorFailure Failure, string By, LimitSeen? Limit, AccessEvidence? Access, FailureRestriction? Restriction,
    FailureDelivery Delivery);
