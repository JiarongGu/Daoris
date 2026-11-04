namespace Daoris.Knowledge;

/// <summary>
/// The sentences and refusals the say, take-back and move doors share (MSG1a, D137): each phrased once so no two doors drift apart on
/// what the person is told.
/// </summary>
internal static class SaidAnswers
{
    /// <summary>The answer to a say whose key's word left a record of the line (row p): a success naming that record, with no word and nothing kept.</summary>
    internal static SessionSayOutcome SettledAnswer(string key, SettledWord held) => new(
        SessionSayRefusal.None,
        $"Already settled under key `{key}`: its word left session `{held.Record}` and nothing more was kept.",
        Session: null) { SettledOn = held.Record };

    /// <summary>
    /// The answer to a say whose key's word waits on the line's head, a record other than the one named (row d-ii, *O8-F1*): settled, naming
    /// the head where the word waits to go, with no word and nothing kept.
    /// </summary>
    internal static SessionSayOutcome WaitsAtHeadAnswer(string key, string head) => new(
        SessionSayRefusal.None,
        $"Already kept under key `{key}`: its word waits on session `{head}`, the newest part of its line, and nothing more was kept.",
        Session: null) { SettledOn = head };

    /// <summary>The refusal of a say to a record its line has left (row d-ii): the head named, beside its sentence, and nothing kept.</summary>
    internal static SessionSayOutcome ContinuedAnswer(Session session, Session head) => new(
        SessionSayRefusal.Continued,
        $"Session `{session.Id}` went on in session `{head.Id}`, the newest part of its line: words to it are kept there, so nothing was kept here.",
        Session: null) { Head = head.Id };

    /// <summary>
    /// The refusal of a say whose line could not be walked (row d-ii): it loops (<paramref name="end"/> a cycle), it goes on past
    /// <see cref="Lines.Bound"/> parts (the bound), or, with <paramref name="end"/> null, it runs back past them, so its settlement is not read.
    /// </summary>
    internal static SessionSayOutcome UnplacedAnswer(Session session, LineEnd? end) => new(
        SessionSayRefusal.Unplaced,
        end switch
        {
            LineEnd.Cycle =>
                $"Session `{session.Id}`'s line loops back on a part it already passed, so it has no newest part to keep words on, and nothing was kept.",
            LineEnd.Bound =>
                $"Session `{session.Id}`'s line goes on past {Lines.Bound} parts, more than Daoris follows, so its newest part is not read, and nothing was kept.",
            _ =>
                $"Session `{session.Id}`'s line runs back past {Lines.Bound} parts, more than Daoris follows, so whether these words were taken "
                + "already is not read, and nothing was kept.",
        },
        Session: null);

    /// <summary>The producer's key a move keeps its word under (LINESTOGETHER1b): its part and its word there, which no other move shares.</summary>
    internal static string MoveKey(string part, string word) => $"move:{part}:{word}";

    /// <summary>The answer to a move's retry (LINESTOGETHER1b): moved already, naming where its word waits or that it left, nothing kept.</summary>
    internal static SessionSayOutcome MovedAgain(string key, string record) => new(
        SessionSayRefusal.None,
        $"Already moved under key `{key}`: its word went to session `{record}`, and nothing more moved.",
        Session: null) { Again = true, MovedTo = record };

    /// <summary>
    /// What never goes on (D137 §2.2, MSG1a), judged before anything is read of the words, for the say door and the take-back
    /// alike: a teammate's record, an intake, a second opinion's reviewer, a helper and a stand-down. Null for any other.
    /// </summary>
    internal static SessionSayOutcome? Never(Session session)
    {
        var id = session.Id;
        if (session.Origin is { } origin)
        {
            return new SessionSayOutcome(
                SessionSayRefusal.NotOurs, $"Session `{id}` ran on `{origin}`, where its conversation is; it cannot go on here.",
                Session: null) { Origin = origin };
        }

        if (session.Ask is { } ask)
        {
            return new SessionSayOutcome(SessionSayRefusal.Intake, IntakeRefusal(id, ask), Session: null) { Ask = ask };
        }

        if (session.Opinion is { } opinion)
        {
            return new SessionSayOutcome(
                SessionSayRefusal.Opinion,
                $"Session `{id}` read another session's work for second opinion `{opinion}`: a pass takes one turn and no words, "
                + "and is never resumed. Ask again, with your words, for a fresh one.",
                Session: null) { Opinion = opinion };
        }

        // MIXAGENT2a-i: the helper's twin of the reviewer's refusal above: its answer comes back through its file.
        if (session.Serves is { } serves)
        {
            return new SessionSayOutcome(
                SessionSayRefusal.Helper,
                $"Session `{id}` is a helper for session `{serves}`: it takes one turn and no words, and is never resumed. "
                + "Ask another agent again, with your words, for a fresh one.",
                Session: null);
        }

        if (session.State == SessionState.StoodDown)
        {
            return new SessionSayOutcome(
                SessionSayRefusal.StoodDown,
                session.Quest is { } theirs
                    ? $"Session `{id}` stood down: `#{theirs}` is someone else's, so it has nothing to go on with."
                    : $"Session `{id}` stood down, so it has nothing to go on with.",
                Session: null) { Quest = session.Quest };
        }

        return null;
    }

    /// <summary>The say's sentence for words kept for a running session's run (COWORK1c).</summary>
    internal static string ForRunMessage(string id, string? from) =>
        $"Kept for session `{id}`, which runs: {(from == SaidFrom.Daoris ? "Daoris's words wait" : "your words wait")} on its record until its run takes them.";

    /// <summary>The say's sentence for words kept for a record that winds up or moves (FOUNDATION1o).</summary>
    internal static string ForNextMessage(string id, string? from) =>
        $"Kept for session `{id}`, which is ending: {(from == SaidFrom.Daoris ? "Daoris's words wait" : "your words wait")} on its record for it to go on with.";

    /// <summary>What the person is told their words will do; Daoris's own words are never called theirs (COWORK1b).</summary>
    internal static string KeptMessage(Session session, string? from = null)
    {
        var whose = from == SaidFrom.Daoris ? "Daoris's words" : "your words";
        return session.State == SessionState.AwaitingPerson && session.Quest is { } quest
            ? $"Kept for session `{session.Id}`: it carries on with `#{quest}` at the driver's next look."
            : session.Kind == SessionKind.Chat
                // A conversation is taken up by its runner the moment it can open, never by the driver's look.
                ? $"Kept for session `{session.Id}`: the same conversation goes on with {whose} as it opens again."
                : $"Kept for session `{session.Id}`: the same session goes on with {whose} at the driver's next look.";
    }

    private static string IntakeRefusal(string id, string ask) =>
        $"Session `{id}` is an intake — answer its ask `#{ask}` instead: publish it or close it.";
}
