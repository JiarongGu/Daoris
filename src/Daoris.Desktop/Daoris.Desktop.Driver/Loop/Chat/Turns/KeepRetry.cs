namespace Daoris.Driver;

/// <summary>
/// A keep on a conversation's record that survives an answer lost after the record stored the word: every keep carries a key, and one
/// that threw or never answered is asked once more under it, so the record answers with the word it stored and it is held once.
/// </summary>
internal static class KeepRetry
{
    /// <summary>The message with its producer's key, or one of its own.</summary>
    public static ChatMessage Keyed(ChatMessage message) =>
        message.Key is null && message.Daoris?.Key is null ? message with { Key = $"keep/{Guid.NewGuid():N}" } : message;

    /// <summary>The record's answer to the keep, or null where neither of two asks got one.</summary>
    public static async Task<SayAnswer?> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await door.KeepAsync(message, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or ObjectDisposedException
                                              or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // Unanswered.
            }
        }

        return null;
    }
}
