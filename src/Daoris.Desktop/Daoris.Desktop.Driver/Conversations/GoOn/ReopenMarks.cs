namespace Daoris.Driver;

/// <summary>
/// The reopens a session was told of (FABRIC-CLOSE1c8, §E5): <c>&lt;home&gt;/sessions/&lt;session&gt;.reopened.json</c>, beside its
/// transcript and its go-on mark, each by the quest and the reopen's machine and sequence, so a look and a driver started again tell
/// the session that said a reopened <c>ready</c> once.
/// </summary>
/// <remarks>
/// <para><b>Written once the say door kept the words</b>, never before: a crash between the two may tell it twice, which D173's
/// note accepts. A write that fails is lost, and the telling's own memory holds the once until the driver starts again.</para>
///
/// <para><b>One per quest</b>: a later reopen of the same quest replaces the one before, which a later submission answered, so
/// nothing reads it again. This machine's, never the record's, as the go-on mark beside it; a file that does not read is no mark.</para>
///
/// <para>What is kept is this store's; the file and its format are <see cref="GoOnFiles"/>' (STRUCT3h, D166's note).</para>
/// </remarks>
public sealed class ReopenMarks(string home)
{
    public const string Suffix = ".reopened.json";

    /// <summary>Where a session's marks are kept.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>Whether <paramref name="session"/> was told of this reopen of <paramref name="quest"/>; never for an id that is not a session's.</summary>
    public bool Told(string session, string quest, QuestReopenView reopen) =>
        SessionEventText.IsLocalId(session)
        && GoOnFiles.ReadTold(PathOf(session)).Any(told => told.Names(quest, reopen));

    /// <summary>
    /// Mark <paramref name="session"/> told of this reopen of <paramref name="quest"/>, replacing the quest's mark before. An id that
    /// is not a session's keeps nothing, and a write that fails is lost.
    /// </summary>
    public void Mark(string session, string quest, QuestReopenView reopen, DateTimeOffset at)
    {
        if (!SessionEventText.IsLocalId(session)) return;

        var path = PathOf(session);
        var kept = GoOnFiles.ReadTold(path).Where(told => !string.Equals(told.Quest, quest, StringComparison.OrdinalIgnoreCase));
        try
        {
            GoOnFiles.WriteTold(path, [.. kept, new ReopenTold(quest, reopen.Machine, reopen.Sequence, at)]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: the telling's own memory holds the once until the driver starts again.
        }
    }
}

/// <summary>One reopen a session was told of (<see cref="ReopenMarks"/>): the quest, the reopen by its machine and sequence, and when.</summary>
public sealed record ReopenTold(string Quest, string Machine, long Sequence, DateTimeOffset At)
{
    /// <summary>Whether this is the mark of <paramref name="reopen"/> on <paramref name="quest"/>.</summary>
    public bool Names(string quest, QuestReopenView reopen) =>
        string.Equals(Quest, quest, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Machine, reopen.Machine, StringComparison.Ordinal) && Sequence == reopen.Sequence;
}
