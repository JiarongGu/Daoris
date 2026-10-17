using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>What became of a dispatched attempt so far (FABRIC-PERFORM1b; the fabric events design §D6), as the unit's record keeps it.</summary>
public static class AttemptStates
{
    /// <summary>Written on the unit's record before its send, and not acknowledged yet: a loop that ended here sent it or did not.</summary>
    public const string Recorded = "recorded";

    /// <summary>Acknowledged by a process of its plugin (<see cref="NodeAttempt.Generation"/>), its outcome still to come by <c>work/step</c>.</summary>
    public const string Sent = "sent";

    /// <summary>Its outcome cannot be established now: <c>work/reconcile</c> is asked before any new attempt.</summary>
    public const string Unknown = "unknown";

    /// <summary>Its outcome is recorded, or its plugin answered that it never started: a replay of it changes nothing.</summary>
    public const string Settled = "settled";

    /// <summary>
    /// Nothing establishes it by itself: reconciliation answered <c>unknown</c>, the step declares neither <c>idempotent</c> nor
    /// <c>reconcile</c>, or its outcome did not bind. The person sees it, and no new attempt is made by itself (§D6).
    /// </summary>
    public const string Held = "held";
}

/// <summary>
/// One dispatched attempt (FABRIC-PERFORM1b; §D6's <i>dispatch is persisted before it is sent</i>): its invocation and key, the call as it
/// is sent every time, the plugin that performs it, and how far it got. Kept on its unit's record, so an invocation binds to its attempt
/// across restarts.
/// </summary>
/// <param name="Node">The node's id in its graph.</param>
/// <param name="Attempt">The node's attempt number it is (<see cref="NodeStatus.Attempt"/>).</param>
/// <param name="Plugin">The plugin whose process performs it: the sender every answer about it must come from (§C4's first rule).</param>
/// <param name="Call">What <c>work/perform</c> sends, the same at every delivery: its invocation, key, revision, inputs and deadline.</param>
/// <param name="State">One of <see cref="AttemptStates"/>.</param>
public sealed record NodeAttempt(string Node, int Attempt, string Plugin, WorkPerformCall Call, string State)
{
    /// <summary>When it was written, before its first send.</summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>The generation of the process that last acknowledged it, or answered what became of it.</summary>
    public int? Generation { get; init; }

    /// <summary>How many sends were acknowledged, each with the same key.</summary>
    public int Deliveries { get; init; }

    /// <summary>What settled or held it: <c>reached</c>, <c>failed</c>, <c>not-started</c>, or <c>unknown</c>.</summary>
    public string? Outcome { get; init; }

    /// <summary>A failure's code, or why its outcome was held (a refusal's code).</summary>
    public string? Code { get; init; }

    /// <summary>A declared retry's persisted deadline (§D6, under the node's <see cref="NodeRetry.DelaysSeconds"/>): the node is ready again from it.</summary>
    public DateTimeOffset? RetryAt { get; init; }

    /// <summary>When Daoris next asks after it by itself: a <c>running</c> answer waits to here.</summary>
    public DateTimeOffset? AskAt { get; init; }
}

/// <summary>A status a move took away (<see cref="NodeMoved.Cleared"/>): kept as the attempt's history before the reset (§D3).</summary>
/// <param name="At">When it was cleared.</param>
/// <param name="Why">What cleared it: the evaluation's why, or the performer's.</param>
/// <param name="Was">The status as it stood.</param>
public sealed record NodeHistory(DateTimeOffset At, string Why, NodeStatus Was);

/// <summary>
/// A unit's node record (FABRIC-PERFORM1b; §E7, <i>unit records keep §D6's attempts</i>): every node's status on the unit, the attempts
/// dispatched for them, the trigger firings their starts consumed, and the history their moves cleared. One writer, the driver in the
/// loop that holds the home's lease (<see cref="NodeRecordFiles"/>).
/// </summary>
/// <param name="Unit">The unit (<see cref="WorkUnit.Id"/>).</param>
public sealed record NodeRecord(string Unit)
{
    /// <summary>How many settled attempts are kept beside every unsettled one, and how many history entries: the newest.</summary>
    public const int SettledKept = 50;

    public const int HistoryKept = 200;

    public IReadOnlyDictionary<string, NodeStatus> Statuses { get; init; } = ImmutableSortedDictionary<string, NodeStatus>.Empty;

    public IReadOnlyList<NodeAttempt> Attempts { get; init; } = [];

    /// <summary>The trigger firings recorded (<see cref="WireFiring.Key"/>), what <see cref="WireRun.Fired"/> is read from.</summary>
    public IReadOnlyList<string> Fired { get; init; } = [];

    public IReadOnlyList<NodeHistory> History { get; init; } = [];

    /// <summary>The attempt an invocation names, or null.</summary>
    public NodeAttempt? Invoked(string invocation) =>
        Attempts.LastOrDefault(attempt => string.Equals(attempt.Call.Invocation, invocation, StringComparison.Ordinal));

    /// <summary>The node's newest attempt, or null.</summary>
    public NodeAttempt? Newest(string node) => Attempts.LastOrDefault(attempt => string.Equals(attempt.Node, node, StringComparison.Ordinal));
}
