namespace Daoris.Driver;

/// <summary>
/// The rules a driven session waiting on its own helper is read by (HELPERWAIT1; D163's and D188's notes). A session whose agent asked a
/// helper and ended its turn so the helper could read waits on that helper, not on the person: it ends <c>completed</c> by
/// <see cref="NoteCodes.EndedAwaitsHelper"/> (<see cref="Observation.Conclude"/>), the helper's answer reopens it once (D188 point 4), and a
/// helper that ends with no answer brings it back to the person as its park would have (<see cref="Observation.HelperGone"/>).
/// </summary>
/// <remarks>
/// <b>Its own asks only.</b> An ask the person made from the terminal for a session is theirs, and a session's turn that ends while one reads
/// waited on nothing it asked: it parks as it would have. A question its last turn asked the person (<c>question_ask</c>) or a go-ahead it
/// asked that waits on them is a real question, and parks whatever helper it asked too.
/// </remarks>
public static class HelperWaits
{
    /// <summary>
    /// The asks <paramref name="session"/>'s agent made that are still open: not refused, and not handed. An ask answered but not yet
    /// handed is open, its answer on its way; one not judged yet, waiting for the person's yes or a slot, or started, is open.
    /// </summary>
    public static IReadOnlyList<HelperAsk> Open(string session, IEnumerable<HelperAsk> asks, Func<string, HelperState?> state) =>
        [.. asks.Where(ask => string.Equals(ask.Asker, session, StringComparison.Ordinal) && ask.AskedBy == HelperAsk.ByAgent
                              && state(ask.Id) is not ({ State: HelperStates.Refused } or { Handed: not null }))];

    /// <summary>
    /// Whether <paramref name="record"/> is a session of this machine's that ended waiting on its own helper: completed by that code,
    /// with nothing waiting on it, since words waiting go on (the helper's answer among them).
    /// </summary>
    public static bool Waits(PriorSession record) => HoldsTree(record) && !record.WordsWaiting;

    /// <summary>
    /// Whether <paramref name="record"/> ended waiting on its own helper and so still holds its tree (HELPERWAIT1; Codex's #5 on COWORKUX1):
    /// completed by that code, a record of this machine's, words waiting or not, since it goes on in that tree with them. What the tidy,
    /// the lock's let-go and the planner's further part read beside the live sessions, so no other part writes there and nothing clears it
    /// while its helper reads.
    /// </summary>
    public static bool HoldsTree(PriorSession record) =>
        !record.Teammate && string.Equals(record.State, "completed", StringComparison.OrdinalIgnoreCase)
        && Carries(record.NoteParts, NoteCodes.EndedAwaitsHelper);

    /// <summary>Whether a run's conclusion is the wait on its own helper (<see cref="Observation.AwaitsHelper"/>): its note's code.</summary>
    public static bool Ended(IReadOnlyList<NotePart>? parts) => Carries(parts, NoteCodes.EndedAwaitsHelper);

    /// <summary>
    /// Whether <paramref name="session"/> asked a go-ahead on its work's ask that still waits on the person (KNOWUSE1a, GOAHEAD2): a real
    /// question, which the person's answer wakes. Unread words, or a quest on no ask, read as none.
    /// </summary>
    public static bool AskedGoAhead(string session, AskWords? words) =>
        words?.GoAheads?.Any(goAhead => goAhead.Answer is null
                                        && goAhead.Asked.Any(request => string.Equals(request.Session, session, StringComparison.Ordinal))) == true;

    /// <summary>
    /// Why a session that waited on its helper comes back to the person: the refusal of its newest own ask that was refused, by its code
    /// and agent (<see cref="HelperNotes.Refused"/>); with none of its own on file any more, no answer came.
    /// </summary>
    public static Noted Why(string session, IEnumerable<HelperAsk> asks, Func<string, HelperState?> state)
    {
        var refused = asks
            .Where(ask => string.Equals(ask.Asker, session, StringComparison.Ordinal) && ask.AskedBy == HelperAsk.ByAgent)
            .Select(ask => (Ask: ask, State: state(ask.Id)))
            .Where(each => each.State is { State: HelperStates.Refused })
            .OrderByDescending(each => each.Ask.At)
            .Select(each => each.State)
            .FirstOrDefault();
        return HelperNotes.Refused(refused?.Code ?? HelperWhy.NoAnswer, refused?.Agent);
    }

    private static bool Carries(IReadOnlyList<NotePart>? parts, NoteCode code) =>
        parts?.Any(part => string.Equals(part.Code, code.Code, StringComparison.Ordinal)) == true;
}
