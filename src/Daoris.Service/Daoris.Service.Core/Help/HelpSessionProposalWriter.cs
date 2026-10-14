using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>session</c> kind's writer (SESSUX1h2a, D161's SESSUX1h1 note): sessions archived or unarchived, by id, or every
/// ended one archived, as the terminal's <c>sessions archive</c> does.
/// </summary>
internal sealed class HelpSessionProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// Write one proposal to archive or unarchive sessions: the door is <c>archive</c> or <c>unarchive</c>, the <c>target</c>
    /// the ids comma-joined with no spaces, or no target and the <c>value</c> <c>ended</c> for every ended session (only
    /// with <c>archive</c>). Whether a session exists, and which have ended, is the driver's to judge.
    /// </summary>
    /// <param name="sessions">The session ids, comma-separated; exactly one of this or <paramref name="ended"/>.</param>
    public (string? Id, string Message) Propose(string door, string? sessions, bool ended, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(sessions);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var ids = named?.Split(',').Select(part => part.Trim()).ToArray();
        var refused = door is not ("archive" or "unarchive")
            ? "a session proposal's door is archive or unarchive."
            : (named is null) == !ended
                ? "a session proposal names the sessions or says `ended`: name exactly one."
            : ended && door != "archive"
                ? "the ended sessions are archived, not unarchived: `ended` goes only with archive."
            : ids is null ? null
            : ids.Any(id => id.Length == 0) ? "an id is one word — an empty one is not."
            : ids.FirstOrDefault(id => !IsSessionId(id)) is { } path
                ? $"a session is named by its id, never by its path: `{path}`."
            : ids.Select(id => Word(id, "an id")).FirstOrDefault(said => said is not null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "session");
            writer.WriteString("door", door);
            Nullable(writer, "target", ids is null ? null : string.Join(',', ids));
            writer.WriteNull("workspace");
            Nullable(writer, "value", ended ? "ended" : null);
            writer.WriteNull("sentence");
        }, why, session, at);
    }

    // A session is named by its id; a teammate's is `<origin>/<id>` (SYNC4), the one form with a slash that §5.2 lets be archived
    // here: one slash, a word each side, no drive, no backslash, not rooted. Anything else is a path.
    private static bool IsSessionId(string id) =>
        id.IndexOf('\\') < 0 && !id.Contains(':') && !Path.IsPathRooted(id) && id.Split('/') is { Length: <= 2 } parts
        && parts.All(part => part.Length > 0);
}
