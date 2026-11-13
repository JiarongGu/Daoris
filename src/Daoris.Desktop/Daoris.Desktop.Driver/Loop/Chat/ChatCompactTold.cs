namespace Daoris.Driver;

/// <summary>
/// What a conversation is told when its harness compacted its context: the requests it sent that nothing has answered, which a summary
/// loses, so it files none again. A turn of Daoris's own, taken by the conversation's turns like any word of Daoris's, and only where a
/// request is open: a compaction that lost nothing is told nothing.
/// </summary>
internal sealed class ChatCompactTold(
    string session, ServiceClient service, string home, Func<ChatMessage, bool> take, Action<string> failed,
    Func<IReadOnlyDictionary<string, PriorSession>, Task<IReadOnlyList<BriefWait>>>? waits = null)
{
    private int _count;

    /// <summary>The harness compacted: the open requests are read and told, a failure said and never thrown.</summary>
    internal void Compacted() => _ = Guard();

    /// <summary>Tells the conversation its open requests; false where none is open, the records could not be read or nothing took the word.</summary>
    public async Task<bool> TellAsync(CancellationToken ct = default)
    {
        var json = await LineCountsRead.RecordsJsonAsync(service, ct).ConfigureAwait(false);
        if (json is null) return false;
        var records = ServiceClient.ReadRecords(json);
        var open = await (waits?.Invoke(records) ?? ChatOpenTold.OpenAsync(service, home, records, session, ct)).ConfigureAwait(false);
        if (open.Count == 0) return false;

        var text = ChatOpenTold.List(
            $"Your context was compacted. This conversation has {(open.Count == 1 ? "a request" : $"{open.Count} requests")} open. "
            + "File no request again that is among them:", open);
        var episode = Interlocked.Increment(ref _count);
        return take(new ChatMessage(text, []) { Daoris = new DaorisWord(DaorisKeys.Compact(session, episode)) });
    }

    private async Task Guard()
    {
        try
        {
            await TellAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or IOException or OperationCanceledException
                                          or System.Text.Json.JsonException or InvalidOperationException)
        {
            failed(error.Message);
        }
    }
}
