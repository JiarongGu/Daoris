namespace Daoris.Driver;

/// <summary>
/// A helper's answer reaches a conversation that asked (MIXAGENT2a-iii, D163 and its MIXAGENT2a-ii note; the mixed agents design
/// §3): the driver kept it on the asker's record marked as the helper's and left an event this runner hears. A conversation open
/// here says it as its next turn, or at its turn's end where it is talking; one that ended goes on with it through its own going-on
/// (<see cref="GoOnAsync"/>), which is the path for any word waiting on an ended record.
/// </summary>
public sealed partial class ChatRunner
{
    // The helper words this runner is already saying, by id: the event and the shell's come-up may both hear one.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _helperSaying = new(StringComparer.Ordinal);

    /// <summary>Whether this runner holds the conversation open, with its process or its turns.</summary>
    private bool Holds(string sessionId) => _context.Turned.ContainsKey(sessionId) || _context.Talking.ContainsKey(sessionId);

    /// <summary>
    /// Say the helper's answers waiting on an open conversation as its turns, once each (MIXAGENT2a-iii): in the order they were
    /// kept, each its own turn, queued behind a turn in flight by the turns' own queue. A word is taken off the record only once
    /// its turn is on the wire, so one that never went stays for the next try. Nothing for a record this runner does not hold.
    /// </summary>
    /// <param name="record">The record as already read, or null to read it now.</param>
    private async Task SayHelperWordsAsync(string sessionId, PriorSession? record = null)
    {
        if (_context.Closing || !Holds(sessionId)) return;
        record ??= await ReadAgainAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        if (record is not { Kind: "chat" } || record.Teammate) return;

        foreach (var word in record.Waiting.Where(word => word.Helper is not null && word.Id is not null))
        {
            if (_context.Closing || !Holds(sessionId)) return;
            if (!_helperSaying.TryAdd(word.Id!, 0)) continue;

            bool said;
            try
            {
                said = SayHelperWord(sessionId, word);
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
    private bool SayHelperWord(string sessionId, SaidWordView word)
    {
        var message = new ChatMessage(word.Text, []) { Helped = word, Delivered = () => _ = TakeOffAsync(sessionId, word.Id!) };
        if (_context.Turned.TryGetValue(sessionId, out var chat)) return chat.Turns.Take(message) is not null;
        if (!_context.Talking.TryGetValue(sessionId, out var adapter)) return false;

        if (!_processes.Send(sessionId, adapter.FrameMessage(message.Prompt))) return false;
        foreach (var recorded in Recorded(message)) _context.Record(sessionId, recorded);
        message.Delivered!();
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
            var (taken, message) = await _service.TakenAsync(sessionId, [word], by: null).ConfigureAwait(false);
            failed = taken ? null : message;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException)
        {
            failed = error.Message;
        }

        if (failed is not null) Note(sessionId, $"— a helper's answer could not be taken off its record: {failed}");
    }
}
