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
/// <para>What is kept is this store's; the file and its format are <see cref="GoOnFiles"/>' (STRUCT3h, D166's note).</para>
/// </remarks>
public sealed class GoOnMarks(string home)
{
    public const string Suffix = ".cannot.json";

    /// <summary>Where a session's mark is kept.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>
    /// Mark these words as judged unable to go on, replacing any mark before. An id that is not a session's keeps nothing,
    /// and a write that fails is lost, which costs one more try at the next look.
    /// </summary>
    public void Mark(string session, IEnumerable<string> said, ContinueReason why, DateTimeOffset at) => Mark(session, said, why.Code, at);

    /// <summary>
    /// The mark's code for words whose turn failed twice running (TURNFAIL1, <see cref="FailedTurnWords"/>): they could go on,
    /// and the driver stops trying them by itself until the person says more. No reason <see cref="ContinueWhy"/> names.
    /// </summary>
    public const string FailedTwice = "failed-twice";

    /// <summary>
    /// The mark's code for answers the answers-alone bound holds for the person (COWORK1e, D188 point 4; the message-urgency design §2.3,
    /// §8.3): going on with them would take their line on by answers alone a third time with nobody between (the bound's count,
    /// <see cref="AnswersAloneHold"/>).
    /// A wait the person answers, *Go on with this answer* or *Not now*; written with the agent that answered. No reason
    /// <see cref="ContinueWhy"/> names.
    /// </summary>
    public const string AnswersAlone = "answers-alone";

    /// <summary>
    /// The mark's code for answers the person kept with *Not now* (COWORK1e, §8.3): theirs, as an answer nobody takes is handed, so they go
    /// on only beside a word of the person's, which is tried unmarked. Findings among them go to the person.
    /// </summary>
    public const string NotNow = "not-now";

    /// <summary><see cref="Mark(string, IEnumerable{string}, ContinueReason, DateTimeOffset)"/> by a code of its own.</summary>
    /// <param name="agent">The agent whose answer an answers-alone mark holds (COWORK1e); null for every other mark.</param>
    public void Mark(string session, IEnumerable<string> said, string code, DateTimeOffset at, string? agent = null)
    {
        var ids = said.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (!SessionEventText.IsLocalId(session) || ids.Count == 0) return;

        try
        {
            GoOnFiles.WriteMark(PathOf(session), ids, code, at, agent);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: the next look tries the words again and marks them then.
        }
    }

    /// <summary>The mark kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
    public GoOnMark? Read(string session) => SessionEventText.IsLocalId(session) ? GoOnFiles.ReadMark(PathOf(session)) : null;

    /// <summary>Take a session's mark away: its words went on, or were taken. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        // A mark left behind judges only the words it names; a word said since is tried again all the same.
        if (SessionEventText.IsLocalId(session)) GoOnFiles.Delete(PathOf(session));
    }

    /// <summary>
    /// Every mark this home keeps, by session, in no order (COWORK1e): what the person's answers-alone waits are listed from, a read of the
    /// sessions folder's marks alone. One that does not read, and a folder not made yet, list nothing.
    /// </summary>
    public IReadOnlyList<SessionMark> All()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Sessions);
        IEnumerable<string> files;
        try
        {
            if (!Directory.Exists(folder)) return [];
            files = Directory.EnumerateFiles(folder, "*" + Suffix).ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var marks = new List<SessionMark>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var session = name[..^Suffix.Length];
            if (SessionEventText.IsLocalId(session) && GoOnFiles.ReadMark(file) is { } mark) marks.Add(new SessionMark(session, mark));
        }

        return marks;
    }

    /// <summary>The marks a look plans by: one per record the person's words wait on that has one, by session.</summary>
    public IReadOnlyDictionary<string, GoOnMark> For(IEnumerable<PriorSession> records)
    {
        var marks = new Dictionary<string, GoOnMark>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.Where(record => record.WordsWaiting))
        {
            if (Read(record.Session) is { } mark) marks[record.Session] = mark;
        }

        return marks;
    }

    /// <summary>
    /// Whether every word waiting on the record was judged unable to go on: then a look leaves it waiting. A word said since
    /// the mark, or no mark, is tried; a record with nothing waiting has nothing to judge.
    /// </summary>
    public static bool Judged(PriorSession record, GoOnMark? mark) =>
        mark is not null
        && record.Waiting is { Count: > 0 } waiting
        && waiting.All(word => word.Id is { } id && mark.Said.Contains(id, StringComparer.Ordinal));
}
