namespace Daoris.Driver;

/// <summary>
/// One conversation on the native door's structured wire: its turns one at a time, each a line on the harness's stdin, each ended by the
/// <c>result</c> the capture reads.
/// </summary>
/// <remarks>
/// <para><b>A message said mid-turn waits for the turn's end</b>, as the protocol door's does: a harness may fold a line written mid-turn
/// into the turn still running, and the record would take it in the middle of that turn.</para>
///
/// <para><b>The person's words are recorded, then sent</b>: the harness can answer before a record written afterwards lands, and the answer
/// would sit above the question.</para>
/// </remarks>
internal sealed class NativeChat : ITurnedChat
{
    IChatTurns ITurnedChat.Turns => Turns;

    private readonly object _gate = new();
    private TaskCompletionSource? _turn;

    /// <summary>
    /// What a message shows of the words a keyed open moved to this part (<see cref="ChatContext.TakeShown"/>): each moved word's own
    /// event, recorded before the write in place of the message's one joined event; null for every other message.
    /// </summary>
    public Func<ChatMessage, IReadOnlyList<SessionEvent>?>? Shown { get; init; }

    /// <param name="ready">
    /// What a conversation going on waits for before its words go: its process tracked, so a send reaches it. Null for a new conversation,
    /// whose first message comes from the person once it is running.
    /// </param>
    /// <param name="wentOn">Told once the words it goes on with are on stdin.</param>
    public NativeChat(
        string sessionId, ChatAgent agent, SessionProcesses processes, Action<SessionEvent> record,
        Action<ChatQueue> changed, Task<bool>? ready = null, Action? wentOn = null)
    {
        Interrupts = agent.FrameInterrupt() is not null;
        Turns = new ChatTurns(
            ready: () => ready ?? Task.FromResult(true),
            take: (message, sent) =>
            {
                var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_gate) _turn = ended;
                string line;
                if (message.GoesOn is { } goOn)
                {
                    // The words an ended chat goes on with: the driver's first line, then each word under the id its record gave it, then
                    // all of them as one message, joined by a blank line, each with where its files are kept. Said once, though a resume
                    // tried once more takes them again.
                    if (!goOn.Spoken)
                    {
                        foreach (var opening in goOn.Opening()) record(opening);
                    }

                    line = agent.FrameMessage(goOn.Prompt);
                }
                else
                {
                    foreach (var recorded in ChatRecorded.Recorded(message, Shown?.Invoke(message))) record(recorded);
                    // Each attached file named by its path, which the agent reads with its own tool; the record keeps the person's
                    // words, not these lines.
                    line = agent.FrameMessage(message.PromptWithFiles);
                }

                if (!processes.Send(sessionId, line))
                {
                    // Nothing is listening: the process is going, and its watch concludes the record. The words a conversation goes on
                    // with are said by its watch, which tries the same resume once more or says why it could not.
                    if (message.GoesOn is null)
                    {
                        var gone = WordsNotes.NotSent();
                        record(new SessionEvent { Kind = SessionEventKind.Note, Text = gone.Note, Parts = gone.Parts });
                    }

                    Gone();
                    return Task.CompletedTask;
                }

                sent();
                if (message.GoesOn is not null) wentOn?.Invoke();
                return ended.Task;
            },
            interrupt: () =>
            {
                if (agent.FrameInterrupt() is { } stop) processes.Send(sessionId, stop);
                return Task.CompletedTask;
            },
            changed);
    }

    public ChatTurns Turns { get; }

    /// <summary>
    /// Whether its harness takes a stop that keeps the conversation: an <c>interrupt</c> control request. The keeping stop is offered only
    /// where it does.
    /// </summary>
    public bool Interrupts { get; }

    /// <summary>The turn's end is in the record: the next message may go.</summary>
    public void TurnEnded()
    {
        TaskCompletionSource? turn;
        lock (_gate)
        {
            turn = _turn;
            _turn = null;
        }

        turn?.TrySetResult();
    }

    /// <summary>The harness has gone: nothing waiting is sent, and a turn waiting on its end stops waiting.</summary>
    /// <param name="personStopped">The person stopped it: what was kept for its turns leaves its record with it.</param>
    public void Gone(bool personStopped = false)
    {
        Turns.Gone(personStopped);
        TurnEnded();
    }

    /// <summary>The turns already asked for, then its run's close, the end of input: the harness winds up and exits on its own.</summary>
    public async Task FinishAsync(Func<Task> close)
    {
        await Turns.FinishAsync().ConfigureAwait(false);
        await close().ConfigureAwait(false);
    }
}
