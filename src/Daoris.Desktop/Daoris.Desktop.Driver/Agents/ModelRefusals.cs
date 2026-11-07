namespace Daoris.Driver;

/// <summary>Where a refused model goes, by door and phase.</summary>
public enum ModelRefusalPath
{
    /// <summary>Read as the tables read the same failure.</summary>
    AsToday,

    /// <summary>The native door after a prompt: the ending's refused model, its offers record written.</summary>
    Ending,

    /// <summary>A protocol open: a re-ask on the same session with the offers write, never the ending's flag.</summary>
    ReAsk,
}

/// <summary>
/// A refused model told by its whole outcome and mapped by door and phase: a subject alone is not a
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
    /// Where a refused model goes: on the native door's failed result, under its guard (a turn not stopped that ran nothing of the
    /// session's own), the ending's flag; at a protocol open, the re-ask; anywhere else as the tables read it.
    /// </summary>
    /// <param name="where">The phase (<see cref="DoorWhere"/>).</param>
    /// <param name="ranNothing">Whether the turn showed no words, thinking or tool call of the session's own.</param>
    /// <param name="stopped">Whether a stop is asked before the turn ends.</param>
    public static ModelRefusalPath Path(Transport door, string where, bool ranNothing, bool stopped) => door switch
    {
        Transport.Native when where == DoorWhere.Result && ranNothing && !stopped => ModelRefusalPath.Ending,
        Transport.Protocol when where == DoorWhere.Open => ModelRefusalPath.ReAsk,
        _ => ModelRefusalPath.AsToday,
    };
}
