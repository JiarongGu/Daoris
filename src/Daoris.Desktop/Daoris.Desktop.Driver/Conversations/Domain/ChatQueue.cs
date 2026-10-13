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
}

/// <summary>What a run does next with what the person said: prompt a word still held, or wait for one already sent.</summary>
/// <param name="Held">A word held for the turn's end, to prompt now; null when <paramref name="Answer"/> is set.</param>
/// <param name="Answer">The answer to a word sent during the turn, in the order sent; null when <paramref name="Held"/> is set.</param>
public sealed record DrivenNext(ChatMessage? Held, Task<string>? Answer);
