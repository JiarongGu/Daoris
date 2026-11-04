namespace Daoris.Driver;

/// <summary>What the performer did with one start, answer or attempt (<see cref="WorkPerformed.What"/>), for the loop's log and the tests.</summary>
public static class PerformedWhat
{
    /// <summary>The attempt was written and its send acknowledged.</summary>
    public const string Sent = "sent";

    /// <summary>The start was refused by the one table or the record (<see cref="WorkPerformed.Code"/>): nothing was written or sent.</summary>
    public const string Refused = "refused";

    /// <summary>The attempt was written, and its send failed or went unanswered: its outcome is unknown until reconciled.</summary>
    public const string Unknown = "unknown";

    /// <summary>An outcome applied to its attempt's node: reached, failed, or unknown by the plugin's word.</summary>
    public const string Applied = "applied";

    /// <summary>An outcome its attempt already has: applied once, never again.</summary>
    public const string Replay = "replay";

    /// <summary>An answer not taken (<see cref="WorkPerformed.Code"/>): no attempt by that invocation, another sender, a stale generation.</summary>
    public const string NotTaken = "not-taken";

    /// <summary>An earlier attempt's outcome, recorded as a stale attempt's: it lifts the gate and moves nothing (§D3).</summary>
    public const string Stale = "stale";

    /// <summary>Reconciliation found it never started: delivered again with the same key.</summary>
    public const string Redelivered = "redelivered";

    /// <summary>Reconciliation found it running: it waits to its deadline.</summary>
    public const string Running = "running";

    /// <summary>Nothing establishes its outcome by itself: the person's (§D6).</summary>
    public const string Held = "held";

    /// <summary>A declared retry's delay passed: the node is ready for a new attempt with a new key.</summary>
    public const string Retry = "retry";

    /// <summary>Reconciliation could not be asked now (the process is not up, or its answer was not taken): asked again later.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>
    /// A node that dispatches nothing was started (FABRIC-RUN1): an observed built-in made active with the firings its start consumed, read
    /// from its records at the next evaluation.
    /// </summary>
    public const string Begun = "begun";

    /// <summary>The person's word on a held attempt was recorded (FABRIC-PERFORM1g): the runtime applies it under the node's declaration.</summary>
    public const string Worded = "worded";

    /// <summary>The person's word was applied (FABRIC-PERFORM1g): the attempt settled by the person, its node moved through the one table.</summary>
    public const string Resolved = "resolved";

    /// <summary>
    /// A believed event that establishes nothing (FABRIC-PERFORM1i): an observed step's <c>unknown</c>, which moves no node and is not kept,
    /// so no later word is ordered behind it.
    /// </summary>
    public const string Noted = "noted";
}
