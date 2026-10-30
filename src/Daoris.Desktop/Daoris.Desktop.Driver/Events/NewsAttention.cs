namespace Daoris.Driver;

/// <summary>
/// What a claimed piece of news says to the person (FOUNDATION1 m-ii): the attention event the shell's notifier and a headless console already
/// read, made from the journal's row and the session's record. The row never carries a repository or a word of a note, so they are read from
/// the record when the piece is shown, after its claim.
/// </summary>
public static class NewsAttention
{
    /// <summary>The event for <paramref name="told"/>; its repository and note empty where the record cannot be read this moment.</summary>
    public static async Task<AttentionEvent> OfAsync(ServiceClient service, NewsTold told, CancellationToken ct)
    {
        SessionView? record = null;
        try
        {
            record = await service.FindSessionAsync(told.Session, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // A record that does not answer is told by its id alone: the claim was made, and a second telling would break the once.
        }

        return new AttentionEvent(told.Kind, told.Session, record?.Repository ?? "", told.State, record?.Note)
        {
            NoteParts = record?.NoteParts,
            Workspace = told.Workspace,
        };
    }
}
