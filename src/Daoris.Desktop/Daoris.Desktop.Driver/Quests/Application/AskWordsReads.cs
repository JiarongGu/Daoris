namespace Daoris.Driver;

/// <summary>
/// The person's words on an ask read from the service, under the name its callers use (STRUCT3f, as D166's STRUCT3a note cuts
/// a mixed type): <c>AskWords.ReadAsync(...)</c> for a session's start, the opinion gate and the tests. The reading asks the
/// service, so it is the Application's; the words and how they are written are <see cref="AskWords"/>' and
/// <see cref="AskWordsText"/>', pure.
/// </summary>
public static class AskWordsReads
{
    extension(AskWords)
    {
        /// <summary>
        /// The person's words on the ask a quest was asked by, read from the ask for a session's start (DRIFT1b): null for a
        /// quest no ask asked, and never a failure. A read that does not answer is <see cref="AskWords.Unread"/>, and the start
        /// goes on with its instruction saying so: the words are what a session is handed, never what holds it.
        /// </summary>
        /// <exception cref="OperationCanceledException">The driver is closing: a start it ends is not one to compose.</exception>
        public static async Task<AskWords?> ReadAsync(ServiceClient service, string? sender, CancellationToken ct)
        {
            if (AskWords.AskOf(sender) is not { } ask) return null;
            try
            {
                return await service.FindAskAsync(ask, ct).ConfigureAwait(false) is { } held ? AskWords.Of(held) : AskWords.Unread(ask);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // A client's own time-out is a cancellation the driver did not ask for: the service did not answer.
                return AskWords.Unread(ask);
            }
        }
    }
}
