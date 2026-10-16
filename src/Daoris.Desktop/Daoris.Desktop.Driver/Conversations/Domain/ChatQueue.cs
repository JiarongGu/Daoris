namespace Daoris.Driver;

/// <summary>What stopping a turn did (CONV4a).</summary>
/// <param name="Cancelled">
/// A turn had reached the harness and was asked to stop. It ends on the harness's own word, as a
/// <c>cancelled</c> turn in the record — this is the asking, not the ending.
/// </param>
/// <param name="Withdrawn">
/// What the person had sent that had not reached the harness, in the order sent — withdrawn and handed
/// back, never sent, so nothing they queued fires after they said stop.
/// </param>
public sealed record TurnStop(bool Cancelled, IReadOnlyList<ChatMessage> Withdrawn)
{
    /// <summary>Nothing was running and nothing was waiting: an answer, never an error.</summary>
    public static readonly TurnStop Nothing = new(false, []);
}

/// <summary>What taking back or rewording one queued word did (QUEUEEDIT1a): a normal answer either way, never an error.</summary>
public enum QueueEdit
{
    /// <summary>The word was still waiting, and is removed or reworded.</summary>
    Done,

    /// <summary>A turn already took it (or it is no word of this queue): nothing changed.</summary>
    Taken,
}

/// <summary>Where a conversation's turns stand, as the page is told it (CONV4a, CONV4b).</summary>
/// <param name="Taking">A turn is on its way to the harness or running there — what a stop would act on.</param>
/// <param name="Queued">What the person sent that has not reached the harness, in the order sent.</param>
/// <param name="LastTurnEnded">
/// When this machine last saw one of its turns end (RAIL2), or null before any has. A live chat's last
/// move: its record moves on state changes only, and writing it every turn would carry a chat's activity
/// to a teammate's machine (D47 §4), so it is told here, machine-local, and never recorded.
/// </param>
/// <param name="Opening">
/// The door is still opening (HELP4): what is queued waits for the session to be ready, not for a turn
/// to end. The page says so; told only <paramref name="Taking"/>, a conversation's first words read as
/// waiting behind a turn when nothing had answered yet.
/// </param>
public sealed record ChatQueue(
    bool Taking, IReadOnlyList<ChatMessage> Queued, DateTimeOffset? LastTurnEnded = null, bool Opening = false)
{
    /// <summary>Nothing running and nothing waiting — a conversation between turns, or none at all.</summary>
    public static readonly ChatQueue Idle = new(false, []);
}

/// <summary>What became of a word said to a conversation (MSG1c, D137 §2.1), in the reach the record and the page speak of.</summary>
public enum TurnReach
{
    /// <summary>No turn ran: it is the next turn, sent at once.</summary>
    Now,

    /// <summary>Handed to the turn on the wire at once, read at the agent's next step (D136).</summary>
    NextStep,

    /// <summary>Waiting for the running turn to end, then its own turn (CONV4a).</summary>
    TurnEnd,

    /// <summary>
    /// The person's *Send now* (STEER2a, D136's 2026-10-16 note): handed to the turn on the wire by the steering extension,
    /// which cuts the step in flight, and taken there; spelled <c>now</c>.
    /// </summary>
    Steered,
}

/// <summary>When what a person tells a driven session reaches it (STEER1, D136).</summary>
public enum DrivenReach
{
    /// <summary>Held, and the next prompt of the same session when its turn ends (SESS3, D90).</summary>
    TurnEnd,

    /// <summary>
    /// Sent at once, as a prompt the agent takes during its turn: Claude Code folds it in at its next step
    /// (docs/2026-10-03-steer-evidence.md §1).
    /// </summary>
    NextStep,

    /// <summary>
    /// The person's *Send now* (STEER2a): steered into the running turn at once, cutting the step in flight
    /// (docs/2026-10-03-steer-evidence.md §2, §6 turn B); spelled <c>now</c>. A word's reach only, never the inbox's.
    /// </summary>
    Steered,
}

/// <summary>What a run does next with what the person said: prompt a word still held, or wait for one already sent.</summary>
/// <param name="Held">A word held for the turn's end, to prompt now; null when <paramref name="Answer"/> is set.</param>
/// <param name="Answer">The answer to a word sent during the turn, in the order sent; null when <paramref name="Held"/> is set.</param>
public sealed record DrivenNext(ChatMessage? Held, Task<string>? Answer)
{
    /// <summary>
    /// What the run waits for before it goes on (STEER2a), which is no turn and no stop reason: a steer the person's *Send now*
    /// sent, whose answer is where the words are taken; or a stop decided for the turn that just ended, still on its way, which
    /// must reach the wire before the held words' own turn does. Never faults. Null for the other two.
    /// </summary>
    public Task? Waits { get; init; }
}

/// <summary>
/// What the person's *Send now* did with their words (STEER2a, D136's 2026-10-16 note): the answer <c>SESSION_INPUT</c> gives
/// with <c>now</c>, for a driven session and a conversation alike.
/// </summary>
/// <param name="Reaches">
/// When they reach the session, in the record's spelling: <c>now</c> (steered into the running turn), <c>next-step</c>,
/// <c>turn-end</c>; null where no turn ran and they are its turn at once, or where the door is not known yet.
/// </param>
/// <param name="Now">
/// The press was carried out: steered into the running turn, held with the turn stopped so they go next (D90), or taken at
/// once because no turn ran.
/// </param>
/// <param name="Why">Where it was not, why, by a <see cref="SendNowWhy"/> code; null where it was.</param>
public sealed record SentNow(string? Reaches, bool Now, string? Why);

/// <summary>Why the person's *Send now* was not carried out (STEER2a): codes the page words itself. The words still go as before.</summary>
public static class SendNowWhy
{
    /// <summary>
    /// A turn runs on a door that can neither steer it nor stop it keeping the session: the native door's driven run, a text-only
    /// door, or a next-step door whose agent never said it steers (a stop there would answer a word on its way cancelled).
    /// </summary>
    public const string NoInterrupt = "no-interrupt";

    /// <summary>The session's first prompt is not on the wire yet: the words go right after it, as any word said then.</summary>
    public const string Opening = "opening";

    /// <summary>
    /// On a next-step door, other words wait before these or are on their way to the agent (a word sent at its next step, a
    /// helper's answer waiting as its own turn): the words go behind them, at the next step or as the next turn, and nothing is
    /// stopped, since a stop would answer a word on its way cancelled while the agent may still act on it (D136 point 4).
    /// </summary>
    public const string Behind = "behind";

    /// <summary>A reach in the record's spelling (D136 §3), <c>now</c> for a steer.</summary>
    public static string? Spelled(DrivenReach? reach) => reach switch
    {
        DrivenReach.NextStep => "next-step",
        DrivenReach.TurnEnd => "turn-end",
        DrivenReach.Steered => "now",
        _ => null,
    };

    /// <summary>A conversation's reach in the record's spelling (MSG1c), <c>now</c> for a steer and null for a turn taken at once.</summary>
    public static string? Spelled(TurnReach? reach) => reach switch
    {
        TurnReach.NextStep => "next-step",
        TurnReach.TurnEnd => "turn-end",
        TurnReach.Steered => "now",
        _ => null,
    };
}
