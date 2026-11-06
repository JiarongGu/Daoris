namespace Daoris.Driver;

/// <summary>
/// What a claimed piece of news says to the person (FOUNDATION1 m-ii): the attention event the shell's notifier and a headless console already
/// read, made from the journal's row and the session's record. The row never carries a repository or a word of a note, so they are read from
/// the record when the piece is shown, after its claim.
/// </summary>
public static class NewsAttention
{
    /// <summary>
    /// The record <paramref name="told"/> is shown with, or null where it cannot be read this moment: the piece is then told by its id alone,
    /// since the claim was made, and a second telling would break the once.
    /// </summary>
    public static async Task<SessionView?> ReadAsync(ServiceClient service, NewsTold told, CancellationToken ct)
    {
        try
        {
            return await service.FindSessionAsync(told.Session, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>The event for <paramref name="told"/>; its repository and note empty where <paramref name="record"/> is null.</summary>
    public static AttentionEvent Of(NewsTold told, SessionView? record) =>
        new(told.Kind, told.Session, record?.Repository ?? "", told.State, record?.Note)
        {
            NoteParts = record?.NoteParts,
            Workspace = told.Workspace,
        };
}
