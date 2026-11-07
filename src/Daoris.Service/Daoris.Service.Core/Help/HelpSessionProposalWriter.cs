using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>session</c> kind's writer: sessions archived or unarchived, by id, or every
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
    /// <param name="helper">
    /// The person's helper doors: <c>helper</c> asks one session's helper a question, naming the answered ask it asks
    /// again after and whether its answer is the person's, and <c>pass</c> sends an answer kept for the person to the session that
    /// asked; the sessions then name exactly one. Null for archive and unarchive.
    /// </param>
    public (string? Id, string Message) Propose(
        string door, string? sessions, bool ended, string why, string? session, DateTimeOffset at, SessionHelperProposal? helper = null)
    {
        var named = Blank(sessions);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var ids = named?.Split(',').Select(part => part.Trim()).ToArray();
        var asked = door is "helper" or "pass";
        var refused = door is not ("archive" or "unarchive" or "helper" or "pass")
            ? "a session proposal's door is archive, unarchive, helper or pass."
            : (named is null) == !ended
                ? "a session proposal names the sessions or says `ended`: name exactly one."
            : ended && door != "archive"
                ? "the ended sessions are archived, not unarchived: `ended` goes only with archive."
            : ids is null ? null
            : ids.Any(id => id.Length == 0) ? "an id is one word — an empty one is not."
            : ids.FirstOrDefault(id => !IsSessionId(id)) is { } path
                ? $"a session is named by its id, never by its path: `{path}`."
            : ids.Select(id => Word(id, "an id")).FirstOrDefault(said => said is not null)
            ?? (asked ? Helping(door, ids, helper) : helper is null ? null : "only the helper and pass doors take a question, an agent, an ask or `toMe`.");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "session");
            writer.WriteString("door", door);
            Nullable(writer, "target", ids is null ? null : string.Join(',', ids));
            writer.WriteNull("workspace");
            Nullable(writer, "value", ended ? "ended" : door == "helper" ? Blank(helper?.Question) : null);
            writer.WriteNull("sentence");
            if (!asked) return;
            Nullable(writer, "agent", Blank(helper?.Agent));
            Nullable(writer, "ask", Blank(helper?.Ask)?.TrimStart('#'));
            writer.WriteBoolean("toMe", helper?.ToMe == true);
        }, why, session, at);
    }

    // The helper door asks one session's helper one question within the box's bound; the pass door names the ask to pass.
    private static string? Helping(string door, string[]? ids, SessionHelperProposal? helper)
    {
        if (ids is not { Length: 1 }) return $"the {door} door names exactly one session.";
        var (question, agent, ask) = (Blank(helper?.Question), Blank(helper?.Agent), Blank(helper?.Ask)?.TrimStart('#'));
        if (door == "pass")
        {
            return ask is null ? "the pass door names the ask to pass."
                : question is not null || agent is not null || helper!.ToMe ? "the pass door takes the session and the ask alone."
                : Word(ask, "an ask's id");
        }

        return question is null ? "the helper door asks a question, and none was given."
            : question.Length > HelperBox.MaxQuestion ? $"a question is at most {HelperBox.MaxQuestion} characters, and this is {question.Length}."
            : agent is not null && Word(agent, "an agent") is { } why ? why
            : ask is not null ? Word(ask, "an ask's id")
            : null;
    }

    // A session is named by its id; a teammate's is `<origin>/<id>`, the one form with a slash that may be archived
    // here: one slash, a word each side, no drive, no backslash, not rooted. Anything else is a path.
    private static bool IsSessionId(string id) =>
        id.IndexOf('\\') < 0 && !id.Contains(':') && !Path.IsPathRooted(id) && id.Split('/') is { Length: <= 2 } parts
        && parts.All(part => part.Length > 0);
}
