namespace Daoris.Driver;

/// <summary>What stopping a turn did.</summary>
/// <param name="Cancelled">
/// A turn that has reached the harness is asked to stop. It ends on the harness's own word, as a
/// <c>cancelled</c> turn in the record — this is the asking, not the ending.
/// </param>
/// <param name="Withdrawn">
/// What the person had sent that had not reached the harness, in the order sent — withdrawn and handed
/// back, never sent, so nothing they queued fires after they said stop.
/// </param>
public sealed record TurnStop(bool Cancelled, IReadOnlyList<ChatMessage> Withdrawn)
{
    /// <summary>Nothing is running and nothing is waiting: an answer, never an error.</summary>
    public static readonly TurnStop Nothing = new(false, []);
}

/// <summary>What taking back or rewording one queued word did: a normal answer either way, never an error.</summary>
public enum QueueEdit
{
    /// <summary>The word is still waiting, and is removed or reworded.</summary>
    Done,

    /// <summary>A turn already took it (or it is no word of this queue): nothing changed.</summary>
    Taken,
}

/// <summary>Where a conversation's turns stand, as the page is told it.</summary>
/// <param name="Taking">A turn is on its way to the harness or running there — what a stop would act on.</param>
/// <param name="Queued">What the person sent that has not reached the harness, in the order sent.</param>
/// <param name="LastTurnEnded">
/// When this machine last saw one of its turns end, or null before any has. A live chat's last
/// move: its record moves on state changes only, and writing it every turn would carry a chat's activity
/// to a teammate's machine, so it is told here, machine-local, and never recorded.
/// </param>
/// <param name="Opening">
/// The door is still opening: what is queued waits for the session to be ready, not for a turn
/// to end. The page says so; told only <paramref name="Taking"/>, a conversation's first words read as
/// waiting behind a turn when nothing had answered yet.
/// </param>
/// <param name="Pressing">
/// The person's press on a chat run's work holds the conversation: what is queued waits for
/// that press to answer, landed or refused, not for a turn. The page says so.
/// </param>
public sealed record ChatQueue(
    bool Taking, IReadOnlyList<ChatMessage> Queued, DateTimeOffset? LastTurnEnded = null, bool Opening = false, bool Pressing = false) : IAgentQueue
{
    IReadOnlyList<IAgentWord> IAgentQueue.Queued => Queued;

    /// <summary>Nothing running and nothing waiting — a conversation between turns, or none at all.</summary>
    public static readonly ChatQueue Idle = new(false, []);
}

/// <summary>
/// What a second opinion's send-back into a live conversation came to (<c>ChatTurns.SendBackAsync</c>):
/// three outcomes the findings' delivery keeps distinct, and two that say why nothing is taken.
/// </summary>
public enum SendBack
{
    /// <summary>Reserved and taken: the findings word the service kept is the conversation's next turn, at once.</summary>
    Reserved,

    /// <summary>The person's turn count rose since the press: their words went first, and the findings go to them.</summary>
    PersonWentOn,

    /// <summary>Activity that is not the person's (a turn of Daoris's or a hand-over's, a press, a limit): the next look tries again.</summary>
    Waiting,

    /// <summary>Reserved, but the service's hand refused: the reservation is let go, and the next look tries again.</summary>
    Refused,

    /// <summary>No runner in this process holds the conversation: the findings wait, as a hand-over's box waits for a runner.</summary>
    NotHere,

    /// <summary>An ended conversation its runner cannot resume: the findings go to the person, never to a new session.</summary>
    CannotGoOn,
}

/// <summary>What became of a word said to a conversation, in the reach the record and the page speak of.</summary>
public enum TurnReach
{
    /// <summary>No turn ran: it is the next turn, sent at once.</summary>
    Now,

    /// <summary>Handed to the turn on the wire at once, read at the agent's next step.</summary>
    NextStep,

    /// <summary>Waiting for the running turn to end, then its own turn.</summary>
    TurnEnd,

    /// <summary>
    /// The person's *Send now*: handed to the turn on the wire by the steering extension,
    /// which cuts the step in flight, and taken there; spelled <c>now</c>.
    /// </summary>
    Steered,
}

/// <summary>What keeping a word for a conversation's turns did: where it reaches, and the record's id for it.</summary>
/// <param name="Word">The id its record kept it under; null where the record would not keep it, and it waits here alone.</param>
public sealed record TurnKept(TurnReach Reach, string? Word)
{
    /// <summary>
    /// The record answered the say <b>settled</b>: the word's key left a record of the line, so this
    /// is a retry of a word held already, and nothing is kept, queued, steered, attempted or taken. <see cref="Word"/> is null.
    /// </summary>
    public bool Settled { get; init; }

    /// <summary>
    /// The record is no longer its line's head: the say door refused the word <c>continued</c>,
    /// naming this head, and nothing is queued, steered, attempted or written here. <see cref="Word"/> is null; the caller says the word there.
    /// </summary>
    public string? Head { get; init; }

    /// <summary>A word at <c>now</c>: what the door did with it (<c>ChatTurns.TakeNowAsync</c>).</summary>
    public SentNow? Now { get; init; }
}

/// <summary>What the person's words to a live chat did: when they reach it in the record's spelling, and the record's id for them.</summary>
/// <param name="Reaches">As <c>ChatRunner.Say</c> spells it: <c>next-step</c>, <c>turn-end</c>, <c>now</c>, or null where they are its turn at once.</param>
/// <param name="Word">The id the record kept them under; null where nothing kept them (a text-only door, a record that would not).</param>
public sealed record ChatSaid(string? Reaches, string? Word)
{
    /// <summary>The say is answered settled: a retry of a word held already, nothing kept, queued, steered, attempted or taken.</summary>
    public bool Settled { get; init; }

    /// <summary>The say is refused <c>continued</c>: the line's head it named, where the word is said instead; nothing kept here.</summary>
    public string? Head { get; init; }

    /// <summary>The person's *Send now*: what the door did with it.</summary>
    public SentNow? Now { get; init; }
}

/// <summary>When what a person tells a driven session reaches it.</summary>
public enum DrivenReach
{
    /// <summary>Held, and the next prompt of the same session when its turn ends.</summary>
    TurnEnd,

    /// <summary>
    /// Sent at once, as a prompt the agent takes during its turn: Claude Code folds it in at its next step.
    /// </summary>
    NextStep,

    /// <summary>
    /// The person's *Send now*: steered into the running turn at once, cutting the step in flight;
    /// spelled <c>now</c>. A word's reach only, never the inbox's.
    /// </summary>
    Steered,
}

/// <summary>
/// What a running session's door can do with a word while a turn runs, which the page
/// offers the composer's levels and the keeping stop by.
/// </summary>
/// <param name="Steers">Its agent steers: a word at <c>now</c> cuts its step in flight.</param>
/// <param name="KeepingStop">
/// It can stop a turn and keep the session, so the keeping stop is offered beside words held for the turn's end: a protocol door
/// (<c>session/cancel</c>) and Claude Code's conversation (its <c>interrupt</c>); never the native door's run.
/// </param>
public sealed record TurnDoor(bool Steers, bool KeepingStop);

/// <summary>What a run does next with what the person said: prompt a word still held, or wait for one already sent.</summary>
/// <param name="Held">A word held for the turn's end, to prompt now; null when <paramref name="Answer"/> is set.</param>
/// <param name="Answer">The answer to a word sent during the turn, in the order sent; null when <paramref name="Held"/> is set.</param>
public sealed record DrivenNext(ChatMessage? Held, Task<string>? Answer)
{
    /// <summary>
    /// What the run waits for before it goes on, which is no turn and no stop reason: a steer the person's *Send now*
    /// sent, whose answer is where the words are taken; or a stop decided for the turn that just ended, still on its way, which
    /// must reach the wire before the held words' own turn does. Never faults. Null for the other two.
    /// </summary>
    public Task? Waits { get; init; }
}

/// <summary>
/// What the person's *Send now* did with their words: the answer <c>SESSION_INPUT</c> gives
/// with <c>now</c>, for a driven session and a conversation alike.
/// </summary>
/// <param name="Reaches">
/// When they reach the session, in the record's spelling: <c>now</c> (steered into the running turn), <c>next-step</c>,
/// <c>turn-end</c>; null where no turn ran and they are its turn at once, or where the door is not known yet.
/// </param>
/// <param name="Now">
/// The press is carried out: steered into the running turn, or taken at once because no turn ran. Never a turn stopped: no word
/// stops one, and the keeping stop is the person's own press.
/// </param>
/// <param name="Why">Where it is not, why, by a <see cref="SendNowWhy"/> code; null where it is.</param>
public sealed record SentNow(string? Reaches, bool Now, string? Why);

/// <summary>Why the person's *Send now* is not carried out: codes the page words itself. The words still go.</summary>
public static class SendNowWhy
{
    /// <summary>
    /// A turn runs on a door whose agent does not steer, and no word interrupts it: the words go at its
    /// next step on a door that takes words then, and are held for its turn's end on any other (a protocol door that holds words,
    /// Claude Code's conversation, the native door's driven run), or go at once on a text-only door. Where the door can stop a turn
    /// and keep the session, the keeping stop is the person's own press beside the held words.
    /// </summary>
    public const string NoInterrupt = "no-interrupt";

    /// <summary>The session's first prompt is not on the wire yet: the words go right after it, as any word said then.</summary>
    public const string Opening = "opening";

    /// <summary>
    /// On a next-step door whose agent cannot steer, other words wait before these or are on their way to the agent (a word sent
    /// at its next step, a helper's answer waiting as its own turn): the words go behind them, at the next step or as the next
    /// turn, and nothing is stopped, since a stop would answer a word on its way cancelled while the agent may still act on it.
    /// Where the agent steers, the words are steered past them instead.
    /// </summary>
    public const string Behind = "behind";

    /// <summary>A reach in the record's spelling, <c>now</c> for a steer.</summary>
    public static string? Spelled(DrivenReach? reach) => reach switch
    {
        DrivenReach.NextStep => "next-step",
        DrivenReach.TurnEnd => "turn-end",
        DrivenReach.Steered => "now",
        _ => null,
    };

    /// <summary>A conversation's reach in the record's spelling, <c>now</c> for a steer and null for a turn taken at once.</summary>
    public static string? Spelled(TurnReach? reach) => reach switch
    {
        TurnReach.NextStep => "next-step",
        TurnReach.TurnEnd => "turn-end",
        TurnReach.Steered => "now",
        _ => null,
    };
}
