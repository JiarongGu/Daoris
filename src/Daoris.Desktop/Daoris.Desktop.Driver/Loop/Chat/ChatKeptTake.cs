namespace Daoris.Driver;

/// <summary>
/// The words kept on a conversation's record before its door was open, handed to its turns once it is: each under its record id, so it is
/// marked attempted before the wire and taken off once written, and a shell cut off under one settles it as uncertain, never sent again.
/// A word the record names already attempted is left for that settling. With no turns (a start that failed) the words wait on the
/// record for the conversation's go-on, which is where they were kept.
/// </summary>
internal sealed class ChatKeptTake(ChatContext context, ChatWords words)
{
    /// <returns>How many words its turns took.</returns>
    public async Task<int> TakeAsync(string sessionId, CancellationToken ct)
    {
        sessionId = context.Pending.Current(sessionId);
        if (!context.Turned.TryGetValue(sessionId, out var chat)) return 0;

        PriorSession? record;
        try
        {
            record = await context.Service.RecordAsync(sessionId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Nothing read is nothing taken: the words wait on their record, which holds them.
            return 0;
        }

        if (record is null) return 0;
        var door = new SaidDoor(context.Service, sessionId);
        var taken = 0;
        foreach (var word in record.Waiting.Where(each => each.Id is not null && each.Attempt is null))
        {
            var (composed, extras) = words.Compose(sessionId, word.Text, null, null);
            var message = composed with
            {
                Id = word.Id, Kept = true, Key = word.Key, Level = word.Level ?? MessageLevel.Urgent,
                Files = ChatFiles.Kept(context.Home, sessionId, word.Files),
            };
            context.ShowMoved(sessionId, word.Id!, MovedWords.Of(word));
            if (chat.Turns.TakeMoved(door, message) is not null)
            {
                taken++;
                continue;
            }

            // Nothing more is heard: the word waits on its record, and what it carried goes back for the words said again.
            if (context.ShownMoved.TryGetValue(sessionId, out var held)) held.TryRemove(word.Id!, out _);
            extras.Restore();
        }

        return taken;
    }
}
