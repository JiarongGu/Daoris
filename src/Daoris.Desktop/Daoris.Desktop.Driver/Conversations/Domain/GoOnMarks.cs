namespace Daoris.Driver;

/// <summary>
/// The marks a record's words could not go on with (MSG1b, closing MSG1a's open point):
/// <c>&lt;home&gt;/sessions/&lt;session&gt;.cannot.json</c>, beside the session's transcript and conversation id. Written where
/// a reopen could not take the words and nothing carries them on by itself (a closed quest, a never; a chat's, MSG1c), so
/// a later look leaves them waiting as said instead of trying them again.
/// </summary>
/// <remarks>
/// <para><b>By the words' ids.</b> The service keeps the words and says nothing of a failed try (MSG1a), and a word said
/// after the mark is tried again, since what held the record (an account, its tree) may have moved. So a record is judged
/// only while every word waiting on it was marked (<see cref="Judged"/>).</para>
///
/// <para><b>This machine's, never the record's</b>, like the conversation id beside it. A file that does not read is no
/// mark: one more try, never the words lost.</para>
///
/// <para>The store's name, its file's place and its rule are Conversations' Domain, since the plan's Domain asks
/// <see cref="Judged"/>; its reads and writes (<c>Mark</c>, <c>Read</c>, <c>Clear</c>, <c>For</c>) are the Application's
/// extension members over the Infrastructure's files (STRUCT3h, D166's note), named on it as before.</para>
/// </remarks>
public sealed class GoOnMarks(string home)
{
    public const string Suffix = ".cannot.json";

    /// <summary>Where a session's mark is kept.</summary>
    public string PathOf(string session) => Path.Combine(home, "sessions", session + Suffix);

    /// <summary>
    /// Whether every word waiting on the record was judged unable to go on: then a look leaves it waiting. A word said since
    /// the mark, or no mark, is tried; a record with nothing waiting has nothing to judge.
    /// </summary>
    public static bool Judged(PriorSession record, GoOnMark? mark) =>
        mark is not null
        && record.Waiting is { Count: > 0 } waiting
        && waiting.All(word => word.Id is { } id && mark.Said.Contains(id, StringComparer.Ordinal));
}
