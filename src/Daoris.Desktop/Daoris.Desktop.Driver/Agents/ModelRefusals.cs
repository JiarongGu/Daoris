namespace Daoris.Driver;

/// <summary>Where a refused model goes, by door and phase (the agent-lyntai design §5.3's round-5 paragraph).</summary>
public enum ModelRefusalPath
{
    /// <summary>Read as today's tables read the same failure.</summary>
    AsToday,

    /// <summary>The native door after a prompt: the ending's refused model, its offers record written as today.</summary>
    Ending,

    /// <summary>A protocol open: D205's re-ask on the same session with the offers write, never the ending's flag.</summary>
    ReAsk,
}

/// <summary>
/// A refused model told by its whole outcome and mapped by door and phase (the agent-lyntai design §5.3, round 5): a subject alone is not a
/// refusal, since a per-model limit and a model at capacity are scoped to the model too.
/// </summary>
public static class ModelRefusals
{
    /// <summary>
    /// Whether the outcome is a refused model: fatal, its restriction's subject the model and its recovery an owner's action, and decided
    /// by an entry in <paramref name="doorEntries"/>, the door's model-refusal entries.
    /// </summary>
    public static bool Refuses(AgentErrorKind kind, string by, FailureRestriction? restriction, IReadOnlySet<string> doorEntries) =>
        kind == AgentErrorKind.Fatal
        && restriction is { Subject: FailureSubject.Model, Recovery.Basis: RecoveryBasis.OwnerAction }
        && doorEntries.Contains(by);

    /// <summary>
    /// Where a refused model goes: on the native door's failed result, under today's guard (a turn not stopped that ran nothing of the
    /// session's own), the ending's flag; at a protocol open, D205's re-ask; anywhere else as today's tables read it.
    /// </summary>
    /// <param name="where">The phase (<see cref="DoorWhere"/>).</param>
    /// <param name="ranNothing">Whether the turn showed no words, thinking or tool call of the session's own.</param>
    /// <param name="stopped">Whether a stop was asked before the turn ended.</param>
    public static ModelRefusalPath Path(Transport door, string where, bool ranNothing, bool stopped) => door switch
    {
        Transport.Native when where == DoorWhere.Result && ranNothing && !stopped => ModelRefusalPath.Ending,
        Transport.Protocol when where == DoorWhere.Open => ModelRefusalPath.ReAsk,
        _ => ModelRefusalPath.AsToday,
    };
}
