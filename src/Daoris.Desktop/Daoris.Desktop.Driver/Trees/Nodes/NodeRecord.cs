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

    /// <summary>Every state an attempt may be in: a record holding another does not read (FABRIC-PERFORM1h), since none of the performer's
    /// rules would ask after it.</summary>
    public static readonly IReadOnlyList<string> All = [Recorded, Sent, Unknown, Settled, Held];
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

    /// <summary>
    /// A <c>quest.close</c> attempt's close (FABRIC-CLOSE1b, §E5 step 1): its token and the quests it names with the submissions it judged,
    /// persisted with its key before the first send, so a timeout or a restart sends the same close again; and a refusal's words once one
    /// came. Null on a plugin's attempt, and on every attempt from before.
    /// </summary>
    public QuestCloseCall? Close { get; init; }

    /// <summary>
    /// The person's word on a held attempt (FABRIC-PERFORM1g, one of <see cref="HeldResolutions"/>): written by their door while it is held,
    /// applied by the runtime under the node's declaration, and kept once it settles the attempt, so an outcome settled by the person is
    /// never read as its plugin's word. Null on every attempt the person did not settle, and on a record written before the field.
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>When the person gave <see cref="Resolution"/>.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }
}

/// <summary>
/// The person's ways on from a held attempt (FABRIC-PERFORM1g; the fabric events design §D6, <i>unknown stops the step, and the person sees
/// it</i>): their word settles what nothing else can establish, and lifts the gate the attempt held.
/// </summary>
public static class HeldResolutions
{
    /// <summary>It happened: the node is reached on the person's word, its evidence the person's, never its plugin's.</summary>
    public const string Happened = "happened";

    /// <summary>It did not: the attempt failed by the person's word, and the node is ready for a new attempt with a new key.</summary>
    public const string DidNot = "did-not";

    /// <summary>
    /// Stop the node: the attempt's outcome stays unknown, settled as the person's to answer for, and nothing more is asked. On a held close
    /// (FABRIC-CLOSE1q), stop its work's further nodes instead: the close's answer is still its door's, kept or asked for with its key. Taken
    /// back once the close's submission was reopened or replaced (FABRIC-CLOSE1q2): the work then is the next submission's.
    /// </summary>
    public const string Stop = "stop";

    /// <summary>
    /// On a held close (FABRIC-CLOSE1q) whose door's answer is kept on it: record that answer again through the one table, never a new close
    /// and never the person's word for the door's.
    /// </summary>
    public const string Record = "record";

    /// <summary>On a held close (FABRIC-CLOSE1q): ask its door again with the same key, token and submissions, which answers its first outcome.</summary>
    public const string Resend = "resend";

    /// <summary>
    /// On a failure kept for the person (FABRIC-PERFORM1k; RUN1e: a code not transient, its tries spent, or another graph's or run's): §E2's
    /// <c>failed → ready</c> by the person, a new attempt the next evaluation starts with a new key under the graph governing now. The
    /// failure stays its plugin's: the word is cleared as it is applied, so it is never read as the person's outcome.
    /// </summary>
    public const string Again = "again";

    /// <summary>The words on a plugin's attempt (FABRIC-PERFORM1g), as they were.</summary>
    public static readonly IReadOnlyList<string> Plugin = [Happened, DidNot, Stop];

    /// <summary>The words on Daoris's own close (FABRIC-CLOSE1q): its recoveries, and the stop offered apart.</summary>
    public static readonly IReadOnlyList<string> Close = [Record, Resend, Stop];

    /// <summary>The words on a close whose door's answer is not kept: nothing to record again, so it is asked for again, or its work stopped.</summary>
    public static readonly IReadOnlyList<string> CloseUnkept = [Resend, Stop];

    /// <summary>The word on a failure kept for the person (FABRIC-PERFORM1k): their new attempt.</summary>
    public static readonly IReadOnlyList<string> Failure = [Again];

    /// <summary>
    /// Every word a record may hold on an attempt. A record holding a close's word reads as unreadable on a build before FABRIC-CLOSE1q, and
    /// one holding <c>again</c> on a build before FABRIC-PERFORM1k, which is why the closer and the performer clear those words as they apply
    /// them: only a word not applied yet is ever written.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Happened, DidNot, Stop, Record, Resend, Again];

    /// <summary>
    /// The words offered on a held attempt (FABRIC-CLOSE1q): a plugin's three; on Daoris's own close, <c>record</c> only where its door closed
    /// the quest (that answer needs nothing but its key), then <c>resend</c> and <c>stop</c>. A refusal is asked for again, its door's code and
    /// words coming back with its first outcome.
    /// </summary>
    public static IReadOnlyList<string> Of(NodeAttempt attempt) =>
        attempt.Plugin != QuestClosing.Sender ? Plugin : attempt.Outcome == WorkStates.Reached ? Close : CloseUnkept;
}

/// <summary>A status a move took away (<see cref="NodeMoved.Cleared"/>): kept as the attempt's history before the reset (§D3).</summary>
/// <param name="At">When it was cleared.</param>
/// <param name="Why">What cleared it: the evaluation's why, or the performer's.</param>
/// <param name="Was">The status as it stood.</param>
public sealed record NodeHistory(DateTimeOffset At, string Why, NodeStatus Was);

/// <summary>
/// One event taken for an observed node (FABRIC-PERFORM1i; §C4's <c>work/step</c> naming no invocation). Its identity is its plugin, its
/// repository's identity, its unit (the record's), its step, its commit and its id: the process's generation fences delivery, never this.
/// </summary>
/// <param name="Plugin">The plugin whose process sent it (§C4's first rule), never what the frame says.</param>
/// <param name="Repository">The repository's identity (§B5), as the frame actually sent named it.</param>
/// <param name="Step">The plugin's own step id, normalised (<c>&lt;plugin&gt;/</c> taken off).</param>
/// <param name="Commit">The full commit it is bound to.</param>
/// <param name="Id">The plugin's id for the event.</param>
/// <param name="State">Reached or failed: an unknown establishes nothing and is not kept.</param>
/// <param name="At">When the plugin says it happened.</param>
/// <param name="Node">The node it was taken for.</param>
public sealed record ObservedEvent(string Plugin, string Repository, string Step, string Commit, string Id, string State, DateTimeOffset At, string Node)
{
    /// <summary>
    /// The unit it was told under where that is not its record's (FABRIC-CUTOVER1c; the cutover design §2.2): on a run's record, the subject its
    /// node observed, so a branch at the same commit inherits no replay or late refusal from the one it replaced. Null on a unit's own record,
    /// and on every event kept before.
    /// </summary>
    public string? Subject { get; init; }
}

/// <summary>
/// Where a run bound to a graph version ended (FABRIC-CUTOVER1c; the cutover design §3): its newest quest here when its end was judged, and
/// when. Kept by the runtime on the run's record; a quest of its chain coming back here moves it, and the run goes on.
/// </summary>
public sealed record RunEnd(string Quest, DateTimeOffset At);

/// <summary>
/// A unit's node record (FABRIC-PERFORM1b; §E7, <i>unit records keep §D6's attempts</i>): every node's status on the unit, the attempts
/// dispatched for them, the trigger firings their starts consumed, and the history their moves cleared. One writer, the driver in the
/// loop that holds the home's lease (<see cref="NodeRecordFiles"/>).
/// </summary>
/// <param name="Unit">The unit (<see cref="WorkUnit.Id"/>).</param>
public sealed record NodeRecord(string Unit)
{
    /// <summary>
    /// How many settled attempts are kept beside every attempt a status still needs (an unsettled one, each node's current one, which a
    /// pending retry is, and one a node holds unreconciled), and how many history entries: the newest.
    /// </summary>
    public const int SettledKept = 50;

    public const int HistoryKept = 200;

    public IReadOnlyDictionary<string, NodeStatus> Statuses { get; init; } = ImmutableSortedDictionary<string, NodeStatus>.Empty;

    public IReadOnlyList<NodeAttempt> Attempts { get; init; } = [];

    /// <summary>The trigger firings recorded (<see cref="WireFiring.Key"/>), what <see cref="WireRun.Fired"/> is read from.</summary>
    public IReadOnlyList<string> Fired { get; init; } = [];

    public IReadOnlyList<NodeHistory> History { get; init; } = [];

    /// <summary>
    /// The plugins' events an observed node was reached or failed on (FABRIC-PERFORM1i): the newest taken for each step and commit, what a
    /// replay is known by and a later event is ordered against. A record written before them reads with none.
    /// </summary>
    public IReadOnlyList<ObservedEvent> Observations { get; init; } = [];

    /// <summary>A run's end (FABRIC-CUTOVER1c), which only a run's record holds; null while it goes on, and on every unit's record.</summary>
    public RunEnd? Ended { get; init; }

    /// <summary>The attempt an invocation names, or null.</summary>
    public NodeAttempt? Invoked(string invocation) =>
        Attempts.LastOrDefault(attempt => string.Equals(attempt.Call.Invocation, invocation, StringComparison.Ordinal));

    /// <summary>The node's newest attempt, or null.</summary>
    public NodeAttempt? Newest(string node) => Attempts.LastOrDefault(attempt => string.Equals(attempt.Node, node, StringComparison.Ordinal));
}
