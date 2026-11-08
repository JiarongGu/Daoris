namespace Daoris.Driver;

/// <summary>
/// The words said to a live conversation: the person's, framed for the conversation's harness and, once sent, part of the record; Daoris's own;
/// the ones kept on the record first; and the ones said at <c>now</c>. A message is composed here with what a conversation is handed once, ahead
/// of its first words.
/// </summary>
internal sealed class ChatWords(ChatContext context, ChatTold told, AttentionWatch answers)
{
    /// <summary>
    /// Send a person's message to a live chat, saying when it reaches it: <c>next-step</c> where its door took them into the turn on the wire,
    /// <c>turn-end</c> where they wait for the running turn, null where they are its turn now or its door carries only text. False when there is
    /// nothing listening. On either structured door a message sent mid-turn waits for its own turn.
    /// </summary>
    /// <param name="files">Kept for this conversation before the message is queued, so a refusal reaches the person while they are still looking.</param>
    /// <param name="preface">Where the person is, handed to the agent ahead of the words, or null.</param>
    /// <param name="level">Urgent, the default, at the running turn's next step where its door takes words then; ordinary as a turn of its own after it.</param>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    public bool Say(
        string sessionId, string message, out string? reaches, IReadOnlyList<ChatUpload>? files = null, string? preface = null,
        MessageLevel level = MessageLevel.Urgent)
    {
        reaches = null;
        sessionId = context.Pending.Current(sessionId);
        var (composed, extras) = Compose(sessionId, message, files, preface);
        var said = composed with { Level = level };

        if (context.Turned.TryGetValue(sessionId, out var chat))
        {
            // Before it is taken, so even a turn that ends at once is the answer the person waits on.
            var asked = answers.Asked(sessionId);
            var reach = chat.Turns.Take(said);
            reaches = Spelled(reach);
            if (reach is null) Untaken(sessionId, extras, asked);
            return reach is not null;
        }

        // Its start refused at the open and its replacement opening, the word joins the words moving there.
        if (context.Pending.For(sessionId) is { } moving && moving.Join(said))
        {
            reaches = Spelled(TurnReach.TurnEnd);
            return true;
        }

        if (SendText(sessionId, said)) return true;
        extras.Restore();
        return false;
    }

    /// <summary>
    /// A message no turn took: what it was handed once goes back for the words said again, and the person waits on no answer to it
    /// (<paramref name="asked"/>, what they waited on before it).
    /// </summary>
    private void Untaken(string sessionId, ChatExtras extras, bool asked)
    {
        extras.Restore();
        answers.Unasked(sessionId, asked);
    }

    /// <summary>
    /// Daoris's own words to a live chat: a turn, as the person's words make one, kept on the record as Daoris's (<see cref="DaorisWords.From"/>),
    /// never the person's. They take none of what a conversation hands with the person's first words, which stay for those words. False when
    /// nothing listens.
    /// </summary>
    /// <param name="key">The word's key (<see cref="DaorisWord"/>): one no fact of Daoris's is judged by, so it always goes.</param>
    public bool Tell(string sessionId, string message, string key)
    {
        sessionId = context.Pending.Current(sessionId);
        var said = new ChatMessage(message, []) { Daoris = new DaorisWord(key) };
        if (context.Turned.TryGetValue(sessionId, out var chat))
        {
            // As the person's words are: the answer to it is one the person waits on.
            var asked = answers.Asked(sessionId);
            if (chat.Turns.Take(said) is not null) return true;
            answers.Unasked(sessionId, asked);
            return false;
        }

        return SendText(sessionId, said);
    }

    /// <summary>
    /// The person's words kept on the conversation's record first, then taken by its turns under the record's id at the level they carry,
    /// through its one dispatcher (<see cref="ChatTurns.KeepAsync"/>): a shell that restarts before they go finds them waiting there. A
    /// text-only door has no turns: its words go at once and are kept nowhere. Null when there is nothing listening.
    /// </summary>
    /// <param name="key">The press's key, so a press that arrives twice keeps one word.</param>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    public async Task<ChatSaid?> SayKeptAsync(
        string sessionId, string message, IReadOnlyList<ChatUpload>? files, string? preface, MessageLevel level, string? key,
        CancellationToken ct = default)
    {
        sessionId = context.Pending.Current(sessionId);
        if (!context.Turned.TryGetValue(sessionId, out var chat))
        {
            if (level == MessageLevel.Now)
            {
                return await SayNowAsync(sessionId, message, files, preface).ConfigureAwait(false) is { } now
                    ? new ChatSaid(now.Reaches, null) { Now = now }
                    : null;
            }

            return Say(sessionId, message, out var reaches, files, preface, level) ? new ChatSaid(reaches, null) : null;
        }

        var (composed, extras) = Compose(sessionId, message, files, preface);
        var said = composed with { Level = level, Key = key };
        var asked = answers.Asked(sessionId);
        TurnKept? kept;
        try
        {
            kept = await chat.Turns.KeepAsync(new SaidDoor(context.Service, sessionId), said, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A keep that never answered took nothing: said again, the words carry what they were handed.
            Untaken(sessionId, extras, asked);
            throw;
        }

        if (kept is null) Untaken(sessionId, extras, asked);
        return kept switch
        {
            null => null,
            { Settled: true } => new ChatSaid(null, null) { Settled = true },
            // The record is no longer its line's head, which the turns answer: nothing is kept, and the caller says it there.
            { Head: { } head } => new ChatSaid(null, null) { Head = head },
            { Now: { } now } => new ChatSaid(now.Reaches, kept.Word) { Now = now },
            _ => new ChatSaid(Spelled(kept.Reach), kept.Word),
        };
    }

    /// <summary>
    /// The person's words at <c>now</c> to a live chat, carried out by its door: steered into the running turn where its agent steers, else at its
    /// next step or queued for the turn's end, never stopping the turn; or the next turn at once where none runs
    /// (<see cref="ChatTurns.TakeNowAsync"/>). A text-only door has no turn to steer: its words go as they always did. Null when there is
    /// nothing listening.
    /// </summary>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    public async Task<SentNow?> SayNowAsync(
        string sessionId, string message, IReadOnlyList<ChatUpload>? files = null, string? preface = null)
    {
        sessionId = context.Pending.Current(sessionId);
        var (said, extras) = Compose(sessionId, message, files, preface);
        if (context.Turned.TryGetValue(sessionId, out var chat))
        {
            // Before it is taken: the stopped turn's end is no answer while these words wait.
            var asked = answers.Asked(sessionId);
            var now = await chat.Turns.TakeNowAsync(said).ConfigureAwait(false);
            if (now is null) Untaken(sessionId, extras, asked);
            return now;
        }

        if (SendText(sessionId, said)) return new SentNow(null, Now: false, SendNowWhy.NoInterrupt);
        extras.Restore();
        return null;
    }

    /// <summary>
    /// The record a conversation's id answers for: the replacement its start moved to where its agent refused the choice at the open, else
    /// itself, as a conversation that went on in another is answered for today.
    /// </summary>
    public string CurrentOf(string sessionId) => context.Pending.Current(sessionId);

    /// <summary>Whether a conversation's start is moving its words to a replacement now: a word said to it joins them.</summary>
    public bool Replacing(string sessionId) => context.Pending.For(sessionId) is { IsMoving: true };

    /// <summary>
    /// Hand a conversation the workflow draft open in the person's editor with the message it is sent with: taken by that message as it is
    /// composed, its palette handed whole only where this conversation is not told it under the same digest.
    /// </summary>
    public void HandDraft(string sessionId, WorkflowDraftTold draft) => context.Drafts[context.Pending.Current(sessionId)] = draft;

    /// <inheritdoc cref="ChatTold.With"/>
    public ChatMessage WithProcess(string sessionId, ChatMessage message) => told.With(sessionId, message);

    /// <summary>
    /// The person's message as a live chat's door takes it: the files kept for this conversation, and the preface, the briefing and the process
    /// it is handed once, ahead of its first words. Nothing is kept for a chat nothing here holds.
    /// </summary>
    private (ChatMessage Message, ChatExtras Extras) Compose(string sessionId, string message, IReadOnlyList<ChatUpload>? files, string? preface)
    {
        var held = context.Turned.ContainsKey(sessionId) || context.Talking.ContainsKey(sessionId);
        IReadOnlyList<KeptFile> kept = held && files is { Count: > 0 } ? ChatFiles.Keep(context.Home, sessionId, files) : [];
        // A conversation started from an earlier one is handed Daoris's brief of that one's line with the person's first message.
        var extras = held ? ChatExtras.Take(context, sessionId) : ChatExtras.None(context, sessionId);
        var composed = new ChatMessage(message, kept)
        {
            Brief = extras.Brief, Preface = ChatMessage.Bound(preface), Briefing = extras.Browser, Draft = extras.Draft,
        };
        return (held ? WithProcess(sessionId, composed) : composed, extras);
    }

    /// <summary>
    /// A text-only door keeps the person's words where it keeps the agent's, the console: in the record they would be half a conversation,
    /// questions with no answers beside them. A file it is handed is named by its path, which any agent can read.
    /// </summary>
    private bool SendText(string sessionId, ChatMessage said)
    {
        var text = said.PromptWithFiles;
        var framed = context.Talking.TryGetValue(sessionId, out var adapter) ? adapter.FrameMessage(text) : text;
        return context.SendLine(sessionId, framed);
    }

    /// <summary>A word's reach in the spelling the record and the page use.</summary>
    public static string? Spelled(TurnReach? reach) => reach switch
    {
        TurnReach.NextStep => "next-step",
        TurnReach.TurnEnd => "turn-end",
        _ => null,
    };
}
