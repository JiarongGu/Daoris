namespace Daoris.Driver;

/// <summary>
/// What a chat run's findings delivery asks the runner that holds its conversation (CHATFLOW1e, the chat-flow design §6.1 (b), (c), (f)): the
/// record to take them, the line's newest in its tree; the person's turn count the press recorded, and the line whose history counts it;
/// the service's hand; and what the answering turn's own completion keeps.
/// </summary>
/// <param name="Session">The line's newest record in its tree, live between turns or ended.</param>
/// <param name="Pressed">The person's turn count at the press that asked the opinion (<see cref="OpinionDue.Turns"/>).</param>
/// <param name="Line">The ids of the line's records in its tree, oldest first, whose history the count reads.</param>
public sealed record ChatSendBackAsk(string Session, int Pressed, IReadOnlyList<string> Line)
{
    /// <summary>The service's hand of the findings to the record named (<see cref="OpinionDoors.HandWordAsync"/>): the word it kept, or null where it refused.</summary>
    public required Func<string, CancellationToken, Task<SaidWordView?>> Hand { get; init; }

    /// <summary>
    /// The answering turn's completion (§6.1 (c)): its ending tip and answers frozen, told whether a word of the person's was handed to it at a
    /// step, before the turns take another word.
    /// </summary>
    public required Func<bool, Task> Answered { get; init; }
}

/// <summary>What a send-back came to (CHATFLOW1e): the outcome, and where it was no reservation, why, in the driver's English.</summary>
public sealed record SentBack(SendBack Outcome, string? Why = null);

/// <summary>
/// The shell's chat runner as a chat run's findings reach its conversation (CHATFLOW1e, the chat-flow design §6.1 (b), (f); <see cref="LoopContext.SendsBack"/>):
/// a seam, so the delivery is the same wherever it runs, and its tests stand in. Null on a host that holds no runner, the headless one, where
/// the findings wait.
/// </summary>
public interface IChatSendBacks
{
    /// <summary>
    /// A live conversation's send-back (§6.1 (b)): the person's turn count compared and the turns reserved in one operation of its keep, the
    /// service's hand asked there and its word the reserved turn's. <see cref="SendBack.NotHere"/> where no runner here holds it.
    /// </summary>
    Task<SentBack> SendBackAsync(ChatSendBackAsk ask, CancellationToken ct = default);

    /// <summary>
    /// An ended conversation's path (§6.1 (f)): asked whether it can go on (its tree stands, its repository is registered, it is a chat of
    /// this machine's, its conversation kept on a door that resumes); where it can, the service's hand keeps the findings on that record and
    /// it goes on with them in its own conversation, under the answering turn's completion. <see cref="SendBack.CannotGoOn"/> where it never
    /// can, said why.
    /// </summary>
    Task<SentBack> GoOnWithFindingsAsync(ChatSendBackAsk ask, CancellationToken ct = default);

    /// <summary>
    /// Whether a conversation this runner holds is between turns (§6.1 (d)): its turn ended with nothing taking, opening or waiting. Null for one
    /// nothing here holds, whose record is read instead.
    /// </summary>
    bool? TurnEnded(string sessionId);
}
