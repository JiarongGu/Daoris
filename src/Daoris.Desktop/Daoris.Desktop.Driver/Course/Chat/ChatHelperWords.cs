namespace Daoris.Driver;

/// <summary>
/// A helper's answer reaches a conversation that asked (MIXAGENT2a-iii, D163 and its MIXAGENT2a-ii note; the mixed agents design
/// §3): the driver kept it on the asker's record marked as the helper's and left an event this runner hears. A conversation open
/// here says it as its next turn, or at its turn's end where it is talking; one that ended goes on with it through its own going-on
/// (<see cref="ChatRunner.GoOnAsync"/>), which is the path for any word waiting on an ended record. A class over the runner's
/// context (PART10h, D166).
/// </summary>
internal sealed class ChatHelperWords(ChatContext context)
{
    // The helper words this runner is already saying, by id: the event and the shell's come-up may both hear one.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _helperSaying = new(StringComparer.Ordinal);

    /// <summary>
    /// Say the helper's answers waiting on an open conversation as its turns, once each (MIXAGENT2a-iii): in the order they were
    /// kept, each its own turn, queued behind a turn in flight by the turns' own queue. A word is taken off the record only once
    /// its turn is on the wire, so one that never went stays for the next try. Nothing for a record this runner does not hold.
    /// </summary>
    /// <param name="record">The record as already read, or null to read it now.</param>
    public async Task SayHelperWordsAsync(string sessionId, PriorSession? record = null)
    {
        if (context.Closing || !context.Holds(sessionId)) return;
        record ??= await context.ReadAgainAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        if (record is not { Kind: "chat" } || record.Teammate) return;

        // COWORK1e: an answer the answers-alone bound holds for the person, or that they kept, is said only once they let it go on.
        var mark = context.Marks.Read(sessionId);
        foreach (var word in record.Waiting.Where(word => word.Helper is not null && word.Id is not null && !Kept(mark, word)))
        {
            if (context.Closing || !context.Holds(sessionId)) return;
            if (!_helperSaying.TryAdd(word.Id!, 0)) continue;

            bool said;
            try
            {
                said = await SayHelperWordAsync(sessionId, word).ConfigureAwait(false);
            }
            catch (Exception)
            {
                said = false;
            }

            // Not said, it waits on the record: the next word heard, or the next come-up, tries again.
            if (!said) _helperSaying.TryRemove(word.Id!, out _);
        }
    }

    /// <summary>
    /// One helper's answer as the conversation's next turn, recorded as Daoris's words around that agent's claim and never as the
    /// person's (<see cref="ChatMessage.Helped"/>). On the structured doors the turns queue it behind a turn in flight; a text-only
    /// door has no turns, so it is sent at once and recorded here, as a hand-over's first message is.
    /// </summary>
    /// <remarks>
    /// 🔴 COWORK1c (the message-urgency design §6's trap): its take-off is awaited and in order, never fired and forgotten. On the
    /// structured doors it is the turns' own, kept under the record's id, after every take-off before it; on a text-only door it is
    /// awaited here before the next answer is said.
    /// </remarks>
    private async Task<bool> SayHelperWordAsync(string sessionId, SaidWordView word)
    {
        // COWORK1d: ordinary unless its ask was urgent, which its word on the record says.
        var message = new ChatMessage(word.Text, [])
        {
            Helped = word, Id = word.Id, Kept = true, Level = word.Level == MessageLevel.Urgent ? MessageLevel.Urgent : MessageLevel.Ordinary,
        };
        if (context.Turned.TryGetValue(sessionId, out var chat))
        {
            chat.Turns.Keeps(new SaidDoor(context.Service, sessionId));
            // 🔴 COWORK1e (D188 point 4; the message-urgency design §2.3): the turns judge each answer as its turn comes, against its line
            // as the turns before it left it, so two answers said at once never both pass as the second.
            chat.Turns.Bounds(answer => HeldAloneAsync(sessionId, answer));
            return chat.Turns.Take(message) is not null;
        }

        if (!context.Talking.TryGetValue(sessionId, out var adapter)) return false;

        if (!context.Processes.Send(sessionId, adapter.FrameMessage(message.Prompt))) return false;
        foreach (var recorded in ChatRunner.Recorded(message)) context.Record(sessionId, recorded);
        await TakeOffAsync(sessionId, word.Id!).ConfigureAwait(false);
        return true;
    }

    /// <summary>Whether a word is among those the answers-alone bound holds for the person, or they kept (COWORK1e).</summary>
    private static bool Kept(GoOnMark? mark, SaidWordView word) =>
        mark is { Why: GoOnMarks.AnswersAlone or GoOnMarks.NotNow } && mark.Said.Contains(word.Id!, StringComparer.Ordinal);

    /// <summary>
    /// The answers-alone bound on an open conversation's turn (COWORK1e): its record and its line read now, as the turns before this one
    /// left them. Held, the answer is marked for the person (<see cref="AnswersAloneHold"/>), and said again only once they let it go on.
    /// </summary>
    private async Task<bool> HeldAloneAsync(string sessionId, ChatMessage answer)
    {
        if (answer.Helped is not { } word) return false;
        var json = await context.Service.SessionRecordsJsonAsync(CancellationToken.None).ConfigureAwait(false);
        if (ServiceClient.ReadRecord(json, sessionId) is not { } record) return false;
        if (!new AnswersAloneHold(context.Events, context.Marks).Holds(record, ServiceClient.ReadRecords(json), DateTimeOffset.UtcNow, [word]))
        {
            return false;
        }

        if (word.Id is { } id) _helperSaying.TryRemove(id, out _);
        return true;
    }

    /// <summary>
    /// The answer went to the conversation: off its record by its id (the taken door), so a later going-on never says it again.
    /// A refusal costs a line in its conversation, never the conversation.
    /// </summary>
    private async Task TakeOffAsync(string sessionId, string word)
    {
        string? failed;
        try
        {
            var (taken, message) = await context.Service.TakenAsync(sessionId, [word], by: null).ConfigureAwait(false);
            failed = taken ? null : message;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException)
        {
            failed = error.Message;
        }

        if (failed is not null) context.Note(sessionId, $"— a helper's answer could not be taken off its record: {failed}");
    }
}
