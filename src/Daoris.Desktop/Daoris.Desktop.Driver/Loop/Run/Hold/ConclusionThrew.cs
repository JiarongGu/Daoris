namespace Daoris.Driver;

/// <summary>
/// A conclusion that throws with the person's words still held: their record is still running, so the say door refuses them, and
/// they are said unreached on it as a stop's are, never lost without a word.
/// </summary>
internal static class ConclusionThrew
{
    public static async Task<T> SayingAsync<T>(Func<Task<T>> conclude, int held, Action<Noted> said)
    {
        try
        {
            return await conclude().ConfigureAwait(false);
        }
        catch when (held > 0)
        {
            said(WordsNotes.Unreached(held, "never reached the session", UnreachedWhy.EndedFirst));
            throw;
        }
    }
}
