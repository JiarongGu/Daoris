using static Daoris.Knowledge.SaidAnswers;
using static Daoris.Knowledge.SaidKeeping;

namespace Daoris.Knowledge;

/// <summary>
/// What becomes of words already kept on a record: taken once a session took them, marked as written to a harness, taken back or reworded
/// before a session took them, and moved to the head of their line.
/// </summary>
internal sealed class SessionSaidMoves(QuestStore quests, SessionStore sessions)
{
    private static string Spell(SessionState state) => Session.Spell(state);

    /// <summary>
    /// Take the person's words off this machine's record once a session took them (MSG1a, D137 §2.4): the resumed run's
    /// first prompt went, or <paramref name="by"/>, the session a fallback handed them to, took them. By their ids, so a
    /// word said after the driver read the record stays for the next run; an id the record does not hold is passed over.
    /// </summary>
    /// <param name="by">The session that took them where it is not this one; it must be this machine's too.</param>
    /// <returns>The record as it now stands and the words taken, for the door to keep on the ask those said after it ended.</returns>
    public async Task<SessionTakeOutcome> TakeSaidAsync(
        string id, IReadOnlyCollection<string> ids, string? by, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return new(SessionSayRefusal.Empty, "Name the words taken: none were named, so nothing was taken.", Session: null, []);
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionTakeOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null, []);
            }

            if (session.Origin is { } origin)
            {
                return new SessionTakeOutcome(
                    SessionSayRefusal.NotOurs, $"Session `{id}` ran on `{origin}`; no words wait on it here.", Session: null, []);
            }

            if (by is not null && !string.Equals(by, id, StringComparison.Ordinal)
                && await sessions.FindAsync(by, inside).ConfigureAwait(false) is not { Origin: null })
            {
                return new SessionTakeOutcome(
                    SessionSayRefusal.NotFound, $"No session `{by}` of this machine's took them.", Session: null, []);
            }

            var taken = session.Said.Where(word => ids.Contains(word.Id, StringComparer.Ordinal)).ToList();
            var left = await sessions.TakeSaidAsync(id, ids, inside).ConfigureAwait(false);
            return new SessionTakeOutcome(
                SessionSayRefusal.None,
                taken.Count == 0
                    ? $"None of those words wait on session `{id}`; nothing was taken."
                    : $"Took {taken.Count} of the person's words off session `{id}`"
                      + (by is null || by == id ? "." : $": session `{by}` took them."),
                left, taken);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Mark the moment a session's dispatcher first wrote these waiting words to its harness (COWORK1c, the message-urgency design §6),
    /// before the wire: a shell that comes up again never sends a word with an attempt a second time, and says it may have reached the
    /// agent. Each word in its own statement, the first moment kept. A word no longer waiting is answered unmarked, never an error:
    /// it was taken, or taken back, meanwhile. No revision, since the words never travel.
    /// </summary>
    public async Task<SessionAttemptOutcome> MarkAttemptAsync(
        string id, IReadOnlyCollection<string> words, DateTimeOffset at, CancellationToken ct = default)
    {
        if (words.Count == 0)
        {
            return new(SessionSayRefusal.Empty, "Name the words attempted: none were named, so nothing was marked.", Session: null, []);
        }

        if (await sessions.FindAsync(id, ct).ConfigureAwait(false) is not { Origin: null })
        {
            return new(SessionSayRefusal.NotFound, $"No session `{id}` of this machine's.", Session: null, []);
        }

        var unmarked = new List<string>();
        foreach (var word in words)
        {
            if (!await sessions.MarkAttemptAsync(id, word, at, ct).ConfigureAwait(false)) unmarked.Add(word);
        }

        var session = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        return new(
            SessionSayRefusal.None,
            unmarked.Count == 0
                ? $"Marked {words.Count} word(s) on session `{id}` as written to its harness."
                : $"Marked {words.Count - unmarked.Count} word(s) on session `{id}`; {string.Join(", ", unmarked.Select(word => $"`{word}`"))} no longer wait.",
            session, unmarked);
    }

    /// <summary>
    /// Take one of the words waiting on this machine's record back before a session took it, or reword it (SAIDBACK1a, D137
    /// §2.4): with no <paramref name="text"/> the word leaves the record; with one, the old word leaves and the new words are
    /// kept last, under a new id, said now, with the old word's files' names and its <c>reopens</c>. The new words are the
    /// person's whoever said the old. Read, judged and written as one step (REV3), so a refused reword leaves the old word as
    /// it was, and the driver taking the record up cannot read it between the two writes.
    /// </summary>
    /// <remarks>
    /// <para><b>Refused as <see cref="SessionSayRefusal.Taken"/></b>, an answer and never an error: a word no longer on the
    /// record, and any word of a record that runs again (queued, starting, working), whose run may already be building its
    /// prompt from it. A park takes its words back, as it takes them (§2.2). What never goes on refuses as the say door does,
    /// before anything is read of the words.</para>
    ///
    /// <para><b>Nothing is kept on the ask</b>, unlike <see cref="TakeSaidAsync"/>'s door: nothing took the words. A park's
    /// answer line stays on its note, which travelled already; a reworded park's answer adds its own line, as a second
    /// answer does. Order among several waiting words is not kept: the new words wait last.</para>
    /// </remarks>
    /// <returns>The record as it now stands, the word taken off it, and a reword's new word.</returns>
    public async Task<SessionSayOutcome> TakeBackAsync(
        string id, string? word, string? text, DateTimeOffset now, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return new(SessionSayRefusal.Empty, "Name the word to take back: none was named, so nothing was taken back.", Session: null);
        }

        var named = word.Trim();
        var words = text?.Trim();
        if (words is { Length: 0 })
        {
            return new(
                SessionSayRefusal.Empty,
                $"There are no words to put in the place of word `{named}`: it waits as it was. Take it back instead to leave none.",
                Session: null);
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionSayOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            if (Never(session) is { } never) return never;

            var parked = session.State == SessionState.AwaitingPerson;
            if (session.Active && !parked)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Taken,
                    $"Session `{id}` is {Spell(session.State)}: its run may already have taken word `{named}`, so it cannot be taken back.",
                    Session: null);
            }

            if (session.Said.FirstOrDefault(each => string.Equals(each.Id, named, StringComparison.Ordinal)) is not { } old)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Taken,
                    $"Word `{named}` no longer waits on session `{id}`: it was taken, or taken back, already.",
                    Session: null);
            }

            var left = await sessions.TakeSaidAsync(id, [named], inside).ConfigureAwait(false);
            if (words is null)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.None,
                    $"Took word `{named}` back from session `{id}`"
                    + (left!.Said.Count == 0 ? ": no words wait on it now." : $": {left.Said.Count} still wait."),
                    left) { Taken = old };
            }

            var reworded = new SaidWord(NewWordId(), words, now, old.Files, old.Reopens);
            var (note, parts) = parked ? AnsweredNote(session, words) : (null, null);
            var kept = await KeepOwnersWordAsync(sessions, left!, reworded, note, parts, inside).ConfigureAwait(false);
            return new SessionSayOutcome(
                SessionSayRefusal.None,
                $"Reworded word `{named}` on session `{id}`: the new words wait last, as `{reworded.Id}`.",
                kept, reworded) { Taken = old };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Move one of the person's words waiting on a part's record to the head of the line <paramref name="to"/> is on (LINESTOGETHER1b, the
    /// review-flow design §2): taken off the part and kept at the head in one step, both or neither, its words as the person wrote them
    /// (D137), the head judged as the say door judges a record. Its key is <c>move:&lt;part&gt;:&lt;word&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>A retry is looked up first, before the word's absence or any refusal of either record as it stands, as the say door's settlement
    /// is: the key settled on any record (it names its source, so no other producer writes it), or waiting on a record of
    /// <paramref name="to"/>'s line.</para>
    ///
    /// <para>A word that answered its part's park was kept on the ask then, so the moved word reopens nothing and answers nothing there
    /// again.</para>
    /// </remarks>
    /// <param name="to">The record the page showed as the head, from which the walk goes on to the head now; none is the part's own line.</param>
    public async Task<SessionSayOutcome> MoveSaidAsync(string id, string? word, string? to, DateTimeOffset now, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return new(SessionSayRefusal.Empty, "Name the word to move: none was named, so nothing moved.", Session: null);
        }

        var named = word.Trim();
        var key = MoveKey(id, named);
        return await sessions.ExclusiveAsync(async inside =>
        {
            if (await sessions.FindAsync(id, inside).ConfigureAwait(false) is not { } part)
            {
                return new SessionSayOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            if (await sessions.SettledUnderAsync(key, (now - SaidKeys.Horizon).ToUnixTimeSeconds(), inside).ConfigureAwait(false) is { } left)
            {
                return MovedAgain(key, left.Record);
            }

            var start = string.IsNullOrWhiteSpace(to) ? part : await sessions.FindAsync(to.Trim(), inside).ConfigureAwait(false);
            if (start is null) return new SessionSayOutcome(SessionSayRefusal.NotFound, $"No session `{to!.Trim()}`.", Session: null);

            var line = await Lines.LineOfAsync(sessions, quests, start, inside).ConfigureAwait(false);
            if (await WaitingUnderAsync(start, line, key, inside).ConfigureAwait(false) is { } waits) return MovedAgain(key, waits);
            if (line.Head is not { } head) return UnplacedAnswer(start, line.End);
            if (Never(part) is { } never) return never;

            if (part.Said.FirstOrDefault(each => string.Equals(each.Id, named, StringComparison.Ordinal)) is not { } old)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Taken, $"Word `{named}` no longer waits on session `{id}`: it was taken, or taken back, already.", Session: null);
            }

            if (old.Attempt is not null)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Taken,
                    $"Word `{named}` on session `{id}` was already handed to its agent, which may have read it, so it was not moved.",
                    Session: null);
            }

            if (!old.Persons)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.NotYours,
                    $"Word `{named}` on session `{id}` is not yours but {(old.From == SaidFrom.Daoris ? "Daoris's" : old.Supervisors ? "the supervisor's" : "another agent's")}, "
                    + "so it was not moved: only your own words move.",
                    Session: null);
            }

            if (part.Active && part.State != SessionState.AwaitingPerson)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.NotHeld,
                    $"Session `{id}` is {Spell(part.State)}: word `{named}` waits for its run, which may take it at any moment, so it was not moved. "
                    + "Take it back from its queue instead.",
                    Session: null);
            }

            if (old.Files.Count > 0)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Files,
                    $"Word `{named}` on session `{id}` came with files, which are that session's, so it was not moved: say it to session `{head.Id}` "
                    + "with them instead.",
                    Session: null);
            }

            if (head.Id == part.Id)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.AtHead, $"Word `{named}` already waits on session `{id}`, the newest part of its line, so it was not moved.", Session: null);
            }

            if (Never(head) is { } refused) return refused;
            var parked = head.State == SessionState.AwaitingPerson;
            if (head.Active && !parked)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Running,
                    $"Session `{head.Id}`, the newest part of the line, is {Spell(head.State)}: words to a running session reach it through the driver "
                    + $"that runs it, so word `{named}` was not moved.",
                    Session: null) { MovedTo = head.Id };
            }

            await sessions.TakeSaidAsync(part.Id, [named], inside).ConfigureAwait(false);
            var moved = new SaidWord(NewWordId(), old.Text, now, [], Reopens: old.Reopens && !parked, Channel: old.Channel, Key: key, Level: old.Level);
            var (note, parts) = parked ? AnsweredNote(head, old.Text) : (null, null);
            var kept = await KeepOwnersWordAsync(sessions, head, moved, note, parts, inside).ConfigureAwait(false);
            return new SessionSayOutcome(
                SessionSayRefusal.None,
                $"Moved word `{named}` from session `{id}` to session `{head.Id}`, the newest part of its line, where it waits as `{moved.Id}`.",
                kept, moved) { Taken = old, MovedTo = head.Id, Answers = parked && old.Reopens };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The record of <paramref name="start"/>'s line a word under <paramref name="key"/> waits on (LINESTOGETHER1b): forward over the walk to
    /// its head, then back along <c>from</c>, at most <see cref="Lines.Bound"/> hops. Null where none holds one.
    /// </summary>
    private async Task<string?> WaitingUnderAsync(Session start, LineWalk forward, string key, CancellationToken inside)
    {
        bool Holds(Session record) => record.Said.Any(each => string.Equals(each.Key, key, StringComparison.Ordinal));
        if (forward.Records.FirstOrDefault(Holds) is { } ahead) return ahead.Id;

        var seen = new HashSet<string>(StringComparer.Ordinal) { start.Id };
        var at = start;
        for (var hops = 0; hops < Lines.Bound && at.From is { } from && seen.Add(from); hops++)
        {
            if (await sessions.FindAsync(from, inside).ConfigureAwait(false) is not { } before) break;
            if (Holds(before)) return before.Id;
            at = before;
        }

        return null;
    }
}
