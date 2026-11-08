using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>What became of a dispatched attempt so far, as the unit's record keeps it.</summary>
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
    /// <c>reconcile</c>, or its outcome did not bind. The person sees it, and no new attempt is made by itself.
    /// </summary>
    public const string Held = "held";

    /// <summary>Every state an attempt may be in: a record holding another does not read, since none of the performer's
    /// rules would ask after it.</summary>
    public static readonly IReadOnlyList<string> All = [Recorded, Sent, Unknown, Settled, Held];
}

/// <summary>
/// One dispatched attempt (persisted before it is sent): its invocation and key, the call as it
/// is sent every time, the plugin that performs it, and how far it got. Kept on its unit's record, so an invocation binds to its attempt
/// across restarts.
/// </summary>
/// <param name="Node">The node's id in its graph.</param>
/// <param name="Attempt">The node's attempt number it is (<see cref="NodeStatus.Attempt"/>).</param>
/// <param name="Plugin">The plugin whose process performs it: the sender every answer about it must come from.</param>
/// <param name="Call">What <c>work/perform</c> sends, the same at every delivery: its invocation, key, revision, inputs and deadline.</param>
/// <param name="State">One of <see cref="AttemptStates"/>.</param>
public sealed record NodeAttempt(string Node, int Attempt, string Plugin, WorkPerformCall Call, string State)
{
    /// <summary>When it is written, before its first send.</summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>The generation of the process that last acknowledged it, or answered what became of it.</summary>
    public int? Generation { get; init; }

    /// <summary>How many sends were acknowledged, each with the same key.</summary>
    public int Deliveries { get; init; }

    /// <summary>What settled or held it: <c>reached</c>, <c>failed</c>, <c>not-started</c>, or <c>unknown</c>.</summary>
    public string? Outcome { get; init; }

    /// <summary>A failure's code, or why its outcome is held (a refusal's code).</summary>
    public string? Code { get; init; }

    /// <summary>A declared retry's persisted deadline (under the node's<see cref="NodeRetry.DelaysSeconds"/>): the node is ready again from it.</summary>
    public DateTimeOffset? RetryAt { get; init; }

    /// <summary>When Daoris next asks after it by itself: a <c>running</c> answer waits to here.</summary>
    public DateTimeOffset? AskAt { get; init; }

    /// <summary>
    /// A <c>quest.close</c> attempt's close: its token and the quests it names with the submissions it judged,
    /// persisted with its key before the first send, so a timeout or a restart sends the same close again; and a refusal's words once one
    /// came. Null on a plugin's attempt.
    /// </summary>
    public QuestCloseCall? Close { get; init; }

    /// <summary>
    /// The person's word on a held attempt (one of <see cref="HeldResolutions"/>): written by their door while it is held,
    /// applied by the runtime under the node's declaration, and kept once it settles the attempt, so an outcome settled by the person is
    /// never read as its plugin's word. Null on every attempt the person did not settle.
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>When the person gave <see cref="Resolution"/>.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>The outcome the person named with <i>it happened</i> on a held decision; null on every other word and attempt.</summary>
    public string? Decision { get; init; }
}

/// <summary>
/// The person's ways on from a held attempt: their word settles what nothing else can establish, and lifts the gate the attempt held.
/// </summary>
public static class HeldResolutions
{
    /// <summary>It happened: the node is reached on the person's word, its evidence the person's, never its plugin's.</summary>
    public const string Happened = "happened";

    /// <summary>It did not: the attempt failed by the person's word, and the node is ready for a new attempt with a new key.</summary>
    public const string DidNot = "did-not";

    /// <summary>
    /// Stop the node: the attempt's outcome stays unknown, settled as the person's to answer for, and nothing more is asked. On a held close,
    /// stop its work's further nodes instead: the close's answer is still its door's, kept or asked for with its key. Taken
    /// back once the close's submission is reopened or replaced: the work then is the next submission's.
    /// </summary>
    public const string Stop = "stop";

    /// <summary>
    /// On a held close whose door's answer is kept on it: record that answer again through the one table, never a new close
    /// and never the person's word for the door's.
    /// </summary>
    public const string Record = "record";

    /// <summary>On a held close: ask its door again with the same key, token and submissions, which answers its first outcome.</summary>
    public const string Resend = "resend";

    /// <summary>
    /// On a failure kept for the person (a code not transient, its tries spent, or another graph's or run's): the person's
    /// <c>failed → ready</c>, a new attempt the next evaluation starts with a new key under the graph governing now. The
    /// failure stays its plugin's: the word is cleared as it is applied, so it is never read as the person's outcome.
    /// </summary>
    public const string Again = "again";

    /// <summary>The words on a plugin's attempt.</summary>
    public static readonly IReadOnlyList<string> Plugin = [Happened, DidNot, Stop];

    /// <summary>The words on Daoris's own close: its recoveries, and the stop offered apart.</summary>
    public static readonly IReadOnlyList<string> Close = [Record, Resend, Stop];

    /// <summary>The words on a close whose door's answer is not kept: nothing to record again, so it is asked for again, or its work stopped.</summary>
    public static readonly IReadOnlyList<string> CloseUnkept = [Resend, Stop];

    /// <summary>The word on a failure kept for the person: their new attempt.</summary>
    public static readonly IReadOnlyList<string> Failure = [Again];

    /// <summary>
    /// A failed verification's press: ask for a fix. Never written on an attempt: it publishes an ask, which the look
    /// finds again by its words, so the record holds nothing of it.
    /// </summary>
    public const string AskFix = "ask-fix";

    /// <summary>The words on a failed check: <i>Ask for a fix…</i>, <i>Run again</i>, and <i>Not now</i> (the person's stop, settling it as theirs).</summary>
    public static readonly IReadOnlyList<string> CheckFailure = [AskFix, Again, Stop];

    /// <summary>
    /// The words on a held check: <i>Run again</i> and <i>Not now</i>. <i>It happened</i> and
    /// <i>it did not</i> stay a plugin's: a check's verdict is its gates', never a person's word.
    /// </summary>
    public static readonly IReadOnlyList<string> Check = [Again, Stop];

    /// <summary>
    /// Every word a record may hold on an attempt. The closer and the performer clear a close's word and <c>again</c> as they apply
    /// them: only a word not applied yet is ever written.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Happened, DidNot, Stop, Record, Resend, Again];

    /// <summary>
    /// The words offered on a held attempt: a plugin's three; on Daoris's own close, <c>record</c> only where its door closed
    /// the quest (that answer needs nothing but its key), then <c>resend</c> and <c>stop</c>. A refusal is asked for again, its door's code and
    /// words coming back with its first outcome.
    /// </summary>
    public static IReadOnlyList<string> Of(NodeAttempt attempt) =>
        attempt.Plugin == NodeKinds.Check ? Check
        : attempt.Plugin != QuestClosing.Sender ? Plugin : attempt.Outcome == WorkStates.Reached ? Close : CloseUnkept;
}

/// <summary>A status a move took away (<see cref="NodeMoved.Cleared"/>): kept as the attempt's history before the reset.</summary>
/// <param name="Why">What cleared it: the evaluation's why, or the performer's.</param>
public sealed record NodeHistory(DateTimeOffset At, string Why, NodeStatus Was);

/// <summary>
/// One event taken for an observed node (a <c>work/step</c> naming no invocation). Its identity is its plugin, its
/// repository's identity, its unit (the record's), its step, its commit and its id: the process's generation fences delivery, never this.
/// </summary>
/// <param name="Plugin">The plugin whose process sent it, never what the frame says.</param>
/// <param name="Repository">The repository's identity, as the frame actually sent named it.</param>
/// <param name="Step">The plugin's own step id, normalised (<c>&lt;plugin&gt;/</c> taken off).</param>
/// <param name="Commit">The full commit it is bound to.</param>
/// <param name="Id">The plugin's id for the event.</param>
/// <param name="State">Reached or failed: an unknown establishes nothing and is not kept.</param>
/// <param name="At">When the plugin says it happened.</param>
/// <param name="Node">The node it is taken for.</param>
public sealed record ObservedEvent(string Plugin, string Repository, string Step, string Commit, string Id, string State, DateTimeOffset At, string Node)
{
    /// <summary>
    /// The unit it is told under where that is not its record's: on a run's record, the subject its
    /// node observed, so a branch at the same commit inherits no replay or late refusal from the one it replaced. Null on a unit's own record.
    /// </summary>
    public string? Subject { get; init; }
}

/// <summary>
/// Where a run bound to a graph version ended: its newest quest here when its end is judged, and
/// when. Kept by the runtime on the run's record; a quest of its chain coming back here moves it, and the run goes on.
/// </summary>
public sealed record RunEnd(string Quest, DateTimeOffset At);

/// <summary>What the person may say of a run's node at its door: pass it over, or stop it.</summary>
public static class NodeWordActs
{
    /// <summary>Pass it over for this run, with the person's words.</summary>
    public const string Skip = "skip";

    /// <summary>Stop it, with the person's words where they give any.</summary>
    public const string Stop = "stop";

    /// <summary>A declined request asked again, as a new attempt under the next number: the table's failed to ready by the person.</summary>
    public const string AskAgain = "ask-again";

    /// <summary>A declined request gone on without, with the person's words: a failed request passed over as a skip is.</summary>
    public const string GoOn = "go-on";

    /// <summary>
    /// The person's Accept of the work: a <c>review</c> reached on their word at the binding it stands on, kept by the landing's
    /// press.
    /// </summary>
    public const string Accept = "accept";

    /// <summary>
    /// The person's changes asked of the work, with their words: a <c>review</c>'s rejection on their word, kept by the same Person
    /// route as their Accept.
    /// </summary>
    public const string Changes = "changes";

    /// <summary>
    /// The person's <i>Send</i> of a ready request under <c>start: you</c>: kept on the run's record as a mark of that attempt, which
    /// the next pass reads as the start's say-so. It moves no state itself, so the table has no row for it.
    /// </summary>
    public const string Send = "send";

    /// <summary>
    /// The person's <i>Done</i> of a waiting task: a <c>human</c> node reached on their word, with its outcome <c>done</c> where it
    /// declares one and their note where they gave one.
    /// </summary>
    public const string Done = "done";

    /// <summary>The person's <i>Approve</i> of a waiting approval: a <c>human</c> node declaring <c>approved</c> and <c>rejected</c>, reached with <c>approved</c>.</summary>
    public const string Approve = "approve";

    /// <summary>The person's <i>Reject</i> of a waiting approval: reached with the outcome <c>rejected</c>, which its <c>when</c> wire routes; a note is theirs to give.</summary>
    public const string Reject = "reject";

    public static readonly IReadOnlyList<string> All = [Skip, Stop, AskAgain, GoOn, Accept, Changes, Send, Done, Approve, Reject];
}

/// <summary>
/// The person's skip or stop of a run's node, kept on the run's record by their
/// door and not applied yet: the runtime, the record's one writer of statuses, applies it at its next pass and takes it off.
/// </summary>
/// <param name="Node">The node's id in the run's graph.</param>
/// <param name="Act">One of <see cref="NodeWordActs"/>.</param>
/// <param name="Words">The person's words: a skip's, which it needs, or a stop's where given.</param>
/// <param name="At">When the person gave it.</param>
public sealed record NodeWord(string Node, string Act, string? Words, DateTimeOffset At)
{
    /// <summary>The landing supervisor whose word it is; null for the person's.</summary>
    public NodeWordBy? By { get; init; }

    /// <summary>The attempt a supervisor's word is released on, which the pass holds it to; null for the person's.</summary>
    public int? Attempt { get; init; }

    /// <summary>The supervisor's decision this person's word took the place of, which the pass settles <c>superseded</c>.</summary>
    public string? Supersedes { get; init; }

    /// <summary>The source its move carries to the node: the person's, or the landing supervisor's.</summary>
    public string Source => By is null ? NodeSource.Person : NodeSource.Supervisor;
}

/// <summary>
/// The repair a review's Accept carries: the work could not land at the tip the person accepted (its tree held
/// uncommitted work, or its replay conflicted), so Daoris carried the session on to clear it and the Accept waits. Where the tip then moved
/// only by the session's own commits, the next pass accepts the review again at the new tip, once; a stop, a decline or the carry-on's bound
/// takes it back.
/// </summary>
/// <param name="Node">The <c>review</c> node whose Accept it is.</param>
/// <param name="Tip">The tip the Accept is given at, which the new tip must descend from.</param>
/// <param name="Turn">The carry-on's turn key, as the say door kept it (<see cref="CarryKeys"/>).</param>
/// <param name="Carries">How many times the session has been carried on for this Accept.</param>
/// <param name="At">When it is kept.</param>
public sealed record NodeRepair(string Node, string Tip, string Turn, int Carries, DateTimeOffset At);

/// <summary>
/// A unit's node record: every node's status on the unit, the attempts
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
    /// The plugins' events an observed node reaches or fails on: the newest taken for each step and commit, what a
    /// replay is known by and a later event is ordered against.
    /// </summary>
    public IReadOnlyList<ObservedEvent> Observations { get; init; } = [];

    /// <summary>A run's end, which only a run's record holds; null while it goes on, and on every unit's record.</summary>
    public RunEnd? Ended { get; init; }

    /// <summary>
    /// The person's skips and stops of a run's nodes, each kept by their door and not applied yet, at most one a node; the
    /// next pass applies them and takes them off. Empty on every unit's record.
    /// </summary>
    public IReadOnlyList<NodeWord> Pending { get; init; } = [];

    /// <summary>
    /// The review Accepts waiting on a carried-on session to clear its tree, at most one a node. Empty on every unit's record.
    /// </summary>
    public IReadOnlyList<NodeRepair> Repairs { get; init; } = [];

    /// <summary>The attempt an invocation names, or null.</summary>
    public NodeAttempt? Invoked(string invocation) =>
        Attempts.LastOrDefault(attempt => string.Equals(attempt.Call.Invocation, invocation, StringComparison.Ordinal));

    /// <summary>The node's newest attempt, or null.</summary>
    public NodeAttempt? Newest(string node) => Attempts.LastOrDefault(attempt => string.Equals(attempt.Node, node, StringComparison.Ordinal));
}
