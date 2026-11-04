namespace Daoris.Driver;

/// <summary>
/// Another agent's findings handed to a conversation as a turn: sent back to the one running, or the ones that make a conversation that ended
/// go on.
/// </summary>
internal sealed class ChatSendBack(IExecutorWiring wiring, ChatRunBinds chatRuns)
{
    private ChatContext Context => wiring.Context;

    /// <inheritdoc cref="IChatSendBacks.SendBackAsync"/>
    /// <remarks>
    /// The person's turn count reads their words in the line's history here and their words queued or on the wire in the turns, compared in
    /// one operation of the keep (<see cref="ChatTurns.SendBackAsync"/>). The word the hand kept leaves the record once its turn is on the wire.
    /// </remarks>
    public async Task<SentBack> SendBackAsync(ChatSendBackAsk ask, CancellationToken ct = default)
    {
        if (Context.Closing || !Context.Turned.TryGetValue(ask.Session, out var chat)) return new(SendBack.NotHere);
        chat.Turns.Keeps(new SaidDoor(Context.Service, ask.Session));
        var outcome = await chat.Turns.SendBackAsync(
                ask.Pressed, () => ChatPresses.TurnsOf(Context.Events, ask.Line),
                async token => await ask.Hand(ask.Session, token).ConfigureAwait(false) is { } word ? Findings(word, ask.Answered) : null, ct)
            .ConfigureAwait(false);
        return new(outcome);
    }

    /// <inheritdoc cref="IChatSendBacks.GoOnWithFindingsAsync"/>
    public async Task<SentBack> GoOnWithFindingsAsync(ChatSendBackAsk ask, CancellationToken ct = default)
    {
        if (Context.Closing) return new(SendBack.Waiting, "the application is closing, so no conversation goes on now.");
        if (Context.Turned.ContainsKey(ask.Session) || Context.Talking.ContainsKey(ask.Session) || Context.GoingOn.ContainsKey(ask.Session))
        {
            return new(SendBack.Waiting, $"conversation `{ask.Session}` runs here, or is going on already.");
        }

        var goOn = wiring.Of<ChatGoOn>();
        var record = await Context.ReadAgainAsync(ask.Session, ct).ConfigureAwait(false);
        if (record is not { Kind: "chat" }) return new(SendBack.CannotGoOn, ContinueWhy.Of(ContinueWhy.Refused).Code);
        if (goOn.CannotGoOnWithFindings(record) is { } never) return new(SendBack.CannotGoOn, never.Code);
        if (await ask.Hand(ask.Session, ct).ConfigureAwait(false) is null) return new(SendBack.Refused);

        var started = await goOn.GoOnWithFindingsAsync(ask.Session, ask.Answered, ct).ConfigureAwait(false);
        // Held, the findings wait on its record: the next word, a wake or the next look goes on with them; refused, the go-on marked why.
        return started.SessionId is not null ? new(SendBack.Reserved) : new(SendBack.Waiting, started.Message);
    }

    /// <summary>Whether a conversation's spawn is handed <c>opinion_answer</c>: its chat run's process names a second opinion.</summary>
    public Task<bool> AnswersFindingsAsync(string sessionId, string tree, DriverConfig config) => chatRuns.AnswersAsync(sessionId, tree, config);

    /// <summary>
    /// Another agent's findings as a conversation's turn: Daoris's words around that agent's claims, kept on the record under the word's id
    /// with its <c>by</c>, never the person's; ordinary, so never steered into a turn in flight; with the answering turn's completion.
    /// </summary>
    public static ChatMessage Findings(SaidWordView word, Func<bool, Task> answered) =>
        new(word.Text, []) { Helped = word, Id = word.Id, Kept = true, Level = MessageLevel.Ordinary, Answering = answered };
}
