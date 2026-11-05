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

    /// <param name="input">The run's one queue of words, which writes them and keeps them until the harness names the conversation (D243).</param>
    /// <param name="wentOn">Told once the words it goes on with are on stdin.</param>
    public NativeChat(
        ChatAgent agent, DoorInput input, Action<SessionEvent> record,
        Action<ChatQueue> changed, Action? wentOn = null)
    {
        Interrupts = agent.FrameInterrupt() is not null;
        Turns = new ChatTurns(
            // A conversation going on waits for nothing here: its words wait on the run's input until its process is up.
            ready: () => Task.FromResult(true),
            take: async (message, sent) =>
            {
                var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_gate) _turn = ended;
                string line;
                IReadOnlyList<string> ids;
                if (message.GoesOn is { } goOn)
                {
                    // The words an ended chat goes on with: the driver's first line, then each word under the id its record gave it, then
                    // all of them as one message, joined by a blank line, each with where its files are kept. Said once: the run's retry
                    // replays the line, never the take.
                    foreach (var opening in goOn.Opening()) record(opening);
                    line = agent.FrameMessage(goOn.Prompt);
                    ids = goOn.Ids;
                }
                else
                {
                    foreach (var recorded in ChatRecorded.Recorded(message, Shown?.Invoke(message))) record(recorded);
                    // Each attached file named by its path, which the agent reads with its own tool; the record keeps the person's
                    // words, not these lines.
                    line = agent.FrameMessage(message.PromptWithFiles);
                    ids = message.Id is { } id ? [id] : [];
                }

                // The run's input writes it, and keeps it until the harness names the conversation it went to, so a run that tries the same
                // resume once more hands the next process the words the first never opened (D243). Written, or held for that process.
                var delivery = await input.SayAsync(new DoorWord(ids, line, Written: () =>
                {
                    sent();
                    if (message.GoesOn is not null) wentOn?.Invoke();
                }), CancellationToken.None).ConfigureAwait(false);
                if (delivery == Delivery.Refused)
                {
                    // Nothing is listening: the process is going, and its watch concludes the record. The words a conversation goes on
                    // with are said by its watch, which says why they could not go.
                    if (message.GoesOn is null)
                    {
                        var gone = WordsNotes.NotSent();
                        record(new SessionEvent { Kind = SessionEventKind.Note, Text = gone.Note, Parts = gone.Parts });
                    }

                    Gone();
                    return;
                }

                await ended.Task.ConfigureAwait(false);
            },
            interrupt: async () =>
            {
                if (agent.FrameInterrupt() is { } stop) await input.ControlAsync(stop, CancellationToken.None).ConfigureAwait(false);
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
