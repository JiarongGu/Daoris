using static Daoris.Knowledge.SaidAnswers;
using static Daoris.Knowledge.SaidKeeping;

namespace Daoris.Knowledge;

/// <summary>
/// The say door: what the person (or Daoris, or the supervisor) said to a session, kept on its record to go on with,
/// and the supervisor's answer to a park and the restore of a park its release did not hand.
/// </summary>
/// <param name="home">This machine's home, where a park's question is filed, or null on a host with none.</param>
internal sealed class SessionSays(QuestStore quests, SessionStore sessions, AskStore? asks, string? home)
{
    private static string Spell(SessionState state) => Session.Spell(state);

    /// <summary>
    /// Keep what the person said to a parked or ended session of this machine's, for it to go on with:
    /// verbatim, in order after the words already waiting, with its own id, when, and its files' names. The record
    /// does not move. A park takes the words as its answer, said on its note as an answer is; an ended record keeps them
    /// for the driver's next look, which takes it out of its ended state with them (<see cref="SessionAdvances.AdvanceAsync"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>What never goes on is refused, before anything is kept</b>: a teammate's record, whose process and
    /// conversation are on their machine; an intake, answered through its ask; a session that stood down, whose quest is
    /// someone else's. So is a running session, which hears words through the driver that runs it, never its
    /// record.</para>
    ///
    /// <para><b>Words follow the line's head</b>: inside the step, after the settlement
    /// and before every refusal above, a record its line has left is refused <see cref="SessionSayRefusal.Continued"/> naming the head
    /// (<see cref="Lines.WalkAsync"/>), so no part opens between the judgement and the keep; a line that loops or runs past
    /// <see cref="Lines.Bound"/> is refused <see cref="SessionSayRefusal.Unplaced"/>. A key's word waiting at the head answers a retry named
    /// to an earlier part settled; one waiting on a part the line has left is answered by the door as it stands.</para>
    ///
    /// <para><b>The words are this machine's alone</b>, like a transcript: on an ended record nothing that travels changes,
    /// so no revision is written. A park's answer line is on its note, which travels.</para>
    ///
    /// <para><b>Keeping them on the ask is the door's</b>, beside this and never inside it: a park's answer at
    /// once, words to an ended record once the session took them.</para>
    /// </remarks>
    /// <param name="walksHead">Whether the door walks the line's head (row d-ii): always, but for the landing measurement's copy of the door as it stood before the row.</param>
    /// <param name="files">What the person gave with the words: only each one's name is kept, never where it is.</param>
    /// <param name="channel">The channel message the words are: kept on the word, so its door tells the ask nothing more.</param>
    /// <param name="from">
    /// Their author where that is not the person (<see cref="SaidFrom"/>): Daoris's own words are kept as Daoris's, never
    /// read as the person's. An author this build does not know is refused before anything is kept.
    /// </param>
    /// <param name="key">
    /// The producer's key: where a word with it already waits on the record, nothing more
    /// is kept and the answer is that word, so a producer's retry keeps one word. Once a session took it, the key is the producer's
    /// own to remember.
    /// </param>
    /// <param name="level">When its sender asked it to reach a working session (<see cref="SaidLevel"/>); one this build does not know is refused.</param>
    /// <param name="running">
    /// The driver that runs the session asks for its run: a running record keeps the word,
    /// which that driver's one dispatcher hands its run and takes off once on the wire. Without it a running record refuses as before,
    /// since only that driver hands a running session its words.
    /// </param>
    /// <param name="decision">With <c>from: supervisor</c> only: the decision whose release this is.</param>
    /// <param name="question">With <c>from: supervisor</c> only: the recorded question it answers.</param>
    /// <param name="choice">With <c>from: supervisor</c> only: the offered label it chose, from which the service composes the text.</param>
    /// <param name="ending">
    /// The shell asks for a record that winds up or moves, its dispatcher closed: a running
    /// record keeps the word as one said once it ended, for its next part to go on with, where the shell held it before. Without it a running
    /// record refuses as before.
    /// </param>
    public async Task<SessionSayOutcome> SayAsync(
        bool walksHead, string id, string? text, IReadOnlyList<string>? files, DateTimeOffset now, CancellationToken ct = default, string? channel = null,
        string? from = null, string? key = null, string? level = null, bool running = false,
        string? decision = null, string? question = null, string? choice = null, bool settled = false, bool ending = false)
    {
        if (from == SaidFrom.Supervisor)
        {
            return await SuperviseAsync(id, text, files, now, channel, key, level, decision, question, choice, ct).ConfigureAwait(false);
        }

        // The person's press names its question alone; a decision and a choice stay the supervisor's, and no other author presses.
        if (decision is not null || choice is not null || (question is not null && from is not null))
        {
            return new(
                SessionSayRefusal.From,
                "A decision and a choice are the supervisor's alone, and a question is named by the person's press alone: "
                + "words from any other author name none, so nothing was kept.",
                Session: null);
        }

        if (question is not null && home is null)
        {
            return new(
                SessionSayRefusal.NoHome,
                "This host keeps no home, so no park's question to check a press against: nothing was kept.",
                Session: null);
        }

        var words = text?.Trim() ?? "";
        if (words.Length == 0)
        {
            return new(SessionSayRefusal.Empty, "There are no words to keep: the person said nothing.", Session: null);
        }

        if (!SaidFrom.Known(from))
        {
            return new(
                SessionSayRefusal.From,
                $"Words from `{from}` are not kept: this service knows no such author, and kept as the person's they would be read as theirs.",
                Session: null);
        }

        if (!SaidLevel.Known(level))
        {
            return new(
                SessionSayRefusal.Level,
                $"Words at `{level}` are not kept: this service knows the levels {SaidLevel.Ordinary}, {SaidLevel.Urgent} and {SaidLevel.Now}.",
                Session: null);
        }

        // Read, judged and written as one step: the driver taking the record up and the words arriving could
        // otherwise both read it waiting, and the words land on a record already going on.
        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionSayOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            // The line's head, read inside this step, so no part opens between this
            // judgement and the keep below. One probe a hop on `sessions_from`, and one on `sessions_quest` for a driven part with none.
            var line = walksHead
                ? await Lines.WalkAsync(sessions, session, inside).ConfigureAwait(false)
                : new LineWalk(session, [session], LineEnd.Head);

            // Row p (the design §3.4 rule 5): asked first, before any refusal but the record's absence, so a retry after the take is answered
            // as kept whatever the record is doing now (a re-say at the head, a record running again, a record the line has left). A word still
            // waiting on the record named is answered by the door as it stands, below: only a key that left a record of the line, or from row
            // d-ii one whose word waits on the line's head (*O8-F1*), is settled. A word waiting on a record the line has left is neither.
            if (settled && key is not null && !session.Said.Any(each => string.Equals(each.Key, key, StringComparison.Ordinal)))
            {
                if (line.Head is { } at && at.Id != session.Id && at.Said.Any(each => string.Equals(each.Key, key, StringComparison.Ordinal)))
                {
                    return WaitsAtHeadAnswer(key, at.Id);
                }

                var (held, whole) = await SaidKeys.SettledOnLineAsync(sessions, session, line, key, now, inside).ConfigureAwait(false);
                if (!whole) return UnplacedAnswer(session, line.End == LineEnd.Bound ? LineEnd.Bound : null);
                if (held is not null) return SettledAnswer(key, held);
            }

            // Row d-ii: words follow the line's head, so a record the line has left keeps nothing, whoever says it and whatever it is now.
            if (line.Head is not { } head) return UnplacedAnswer(session, line.End);
            if (head.Id != session.Id) return ContinuedAnswer(session, head);

            if (Never(session) is { } never) return never;

            var parked = session.State == SessionState.AwaitingPerson;
            // A running record keeps words only for its own run, asked by the driver that runs it.
            // A record that winds up or moves keeps the word for its next part, as an ended record keeps one.
            var forNext = session.Active && !parked && ending && !running;
            var forRun = session.Active && !parked && !forNext;
            if (forRun && !running)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Running,
                    $"Session `{id}` is {Spell(session.State)}: words to a running session reach it through the driver that "
                    + "runs it, not its record.",
                    Session: null);
            }

            // A word with this producer's key already waits, so this say is its producer's retry.
            if (key is not null && session.Said.FirstOrDefault(each => string.Equals(each.Key, key, StringComparison.Ordinal)) is { } waiting)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.None,
                    $"Already kept for session `{id}` under key `{key}`, as word `{waiting.Id}`: nothing more was kept.",
                    session, waiting) { Again = true };
            }

            // After the retry, so a double press is answered with its word whatever the question did since; before the keep, in this step.
            if (question is not null && QuestionBox.Read(home!, id) is var onFile
                && (onFile is null || !string.Equals(onFile.Id, question, StringComparison.Ordinal)))
            {
                var current = onFile is null ? "No question is on file now" : $"The question now on file is `{onFile.Id}`: \"{onFile.Question}\"";
                return new SessionSayOutcome(
                    SessionSayRefusal.QuestionChanged,
                    $"The question on file for session `{id}` is not `{question}`: nothing was kept. {current}.",
                    Session: null);
            }

            // A word for a running record's run reopens nothing and answers no park, so no note is written and nothing travels.
            var word = new SaidWord(
                NewWordId(), words, now, Names(files), Reopens: !parked && !forRun, Channel: channel, From: from, Key: key, Level: level,
                Question: question);
            var (note, parts) = parked ? AnsweredNote(session, words, from) : (null, null);
            var kept = await KeepOwnersWordAsync(sessions, session, word, note, parts, inside).ConfigureAwait(false);
            var message = forRun ? ForRunMessage(id, from) : forNext ? ForNextMessage(id, from) : KeptMessage(session, from);
            return new SessionSayOutcome(SessionSayRefusal.None, message, kept, word)
            {
                Answers = parked,
            };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Keep the supervisor's answer on a park, the release's one conditional step:
    /// its text composed here from <paramref name="choice"/> (<see cref="SupervisorWord.Handed"/>), its key the release's receipt, and
    /// every check made inside the store's one exclusive step, so nothing is kept where any fails.
    /// </summary>
    /// <remarks>
    /// <para><b>A claim, never an authority.</b> Any caller can reach this door; what makes a word the supervisor's is the driver's
    /// memory of the id its own release is answered with, never this word's fields. So nothing here grants: a word kept by another
    /// caller is unattributed by the driver, quarantined and taken back. No answered line is written on the note, so a forged word
    /// leaves nothing that asserts authority; the supervisor's line is the driver's, for an attributed word.</para>
    ///
    /// <para><b>The key's retry compares</b> its author, decision, question, choice and text, so a lost response is recovered with the
    /// word already kept and a different say under the same key is refused (<see cref="SessionSayRefusal.KeyMismatch"/>). A word claiming
    /// the decision under another key is never the release's (<see cref="SessionSayRefusal.DecisionTaken"/>).</para>
    /// </remarks>
    private async Task<SessionSayOutcome> SuperviseAsync(
        string id, string? text, IReadOnlyList<string>? files, DateTimeOffset now, string? channel, string? key, string? level,
        string? decision, string? question, string? choice, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(text) || files is { Count: > 0 })
        {
            return new(
                SessionSayRefusal.SupervisorText,
                "The supervisor's answer is composed by this service from its choice alone: a say naming words or files of its own keeps nothing.",
                Session: null);
        }

        if (string.IsNullOrWhiteSpace(decision) || string.IsNullOrWhiteSpace(question) || string.IsNullOrEmpty(choice)
            || string.IsNullOrWhiteSpace(key) || channel is not null || level is not null)
        {
            return new(
                SessionSayRefusal.From,
                "Words from the supervisor are kept only with their decision, question, choice and key, and no channel or level: nothing was kept.",
                Session: null);
        }

        if (home is null)
        {
            return new(
                SessionSayRefusal.NoHome,
                "This host keeps no home, so no park's question to check: the supervisor's answer is a desktop's alone, and nothing was kept.",
                Session: null);
        }

        var handed = SupervisorWord.Handed(choice);
        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionSayOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            if (Never(session) is { } never) return never;

            // The retry first: a lost response is answered with the word its receipt kept, whatever moved since.
            if (session.Said.FirstOrDefault(each => string.Equals(each.Key, key, StringComparison.Ordinal)) is { } again)
            {
                return again is { From: SaidFrom.Supervisor } && again.Decision == decision && again.Question == question
                       && again.Choice == choice && again.Text == handed
                    ? new SessionSayOutcome(
                        SessionSayRefusal.None,
                        $"Already kept for session `{id}` under this key, as word `{again.Id}`: nothing more was kept.",
                        session, again) { Again = true }
                    : new SessionSayOutcome(
                        SessionSayRefusal.KeyMismatch,
                        $"A different word already waits on session `{id}` under this key: a retry is the same say, so nothing was kept.",
                        Session: null);
            }

            if (session.Said.Any(each => each.Supervisors && each.Decision == decision))
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.DecisionTaken,
                    $"A word claiming decision `{decision}` already waits on session `{id}` under another key: it was never this release's, "
                    + "so nothing was kept.",
                    Session: null);
            }

            var refused = await SupervisedParkAsync(session, question, choice, inside).ConfigureAwait(false);
            if (refused is not null) return refused;

            var word = new SaidWord(
                NewWordId(), handed, now, [], From: SaidFrom.Supervisor, Key: key, Decision: decision, Question: question, Choice: choice);
            // The release's own keep, never a person's word: no answered line, and nothing taken back (an owner's word refused it above).
            var kept = await sessions.KeepSaidAsync(id, word, note: null, inside).ConfigureAwait(false);
            return new SessionSayOutcome(
                SessionSayRefusal.None,
                $"Kept the supervisor's answer for session `{id}`: it carries on with `#{session.Quest}` at the driver's next look, "
                + "unless the owner says otherwise first.",
                kept, word);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Put a park the driver took up back as it stood, less the supervisor's word it admitted:
    /// a failure between the admission and the wire hands nothing, so the record returns to <c>awaiting-person</c> with
    /// <paramref name="note"/>, the note as the look read it, <b>keeping every other waiting word</b>: a helper's, an opinion's,
    /// Daoris's, and any word of the owner's that arrived after the admission. One exclusive step. Unlike a move to
    /// <c>awaiting-person</c>, which clears the words for a park that asks anew, nothing is lost.
    /// </summary>
    /// <param name="takeBack">The admitted supervisor's words to take off; a word of any other author named here is left.</param>
    public async Task<SessionAdvanceOutcome> RestoreParkAsync(
        string id, IReadOnlyCollection<string> takeBack, string? note, string? noteParts, DateTimeOffset now, CancellationToken ct = default) =>
        await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is not { Origin: null })
            {
                return new SessionAdvanceOutcome(SessionAdvanceRefusal.NotFound, $"No session `{id}` of this machine's.", Session: null);
            }

            // Only the record the driver took up and has not started: any other state is not this restore's to move.
            if (session.State != SessionState.Working)
            {
                return new SessionAdvanceOutcome(
                    SessionAdvanceRefusal.InvalidMove,
                    $"Session `{id}` is {Spell(session.State)}, not the working record a go-on took up, so it was not restored.",
                    Session: null);
            }

            var taken = session.Said
                .Where(word => word.Supervisors && takeBack.Contains(word.Id, StringComparer.Ordinal))
                .Select(word => word.Id)
                .ToList();
            if (taken.Count > 0) await sessions.TakeSaidAsync(id, taken, inside).ConfigureAwait(false);
            var restored = await sessions
                .SetStateAsync(id, SessionState.AwaitingPerson, note, evidence: null, transcript: null, now, inside, noteParts: noteParts)
                .ConfigureAwait(false);
            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.None,
                $"Session `{id}` waits on the owner again: the supervisor's answer was not handed, and {restored!.Said.Count} word(s) still wait.",
                restored);
        }, ct).ConfigureAwait(false);

    /// <summary>
    /// Why the supervisor's answer may not land on <paramref name="session"/>, or null:
    /// read inside the say's step, the question from this service's own file and the go-aheads from the ask's record.
    /// </summary>
    private async Task<SessionSayOutcome?> SupervisedParkAsync(Session session, string question, string choice, CancellationToken inside)
    {
        var id = session.Id;
        SessionSayOutcome Refused(SessionSayRefusal refusal, string message) => new(refusal, message, Session: null);

        if (session.State != SessionState.AwaitingPerson)
        {
            return Refused(SessionSayRefusal.NotParked, $"Session `{id}` is {Spell(session.State)}, not parked: the supervisor answers only a park.");
        }

        if (session.Quest is null)
        {
            return Refused(SessionSayRefusal.NoQuest, $"Session `{id}` holds no quest, so no ask whose go-aheads the supervisor must leave open.");
        }

        if (session.Said.Any(each => each.Persons))
        {
            return Refused(SessionSayRefusal.OwnerAnswered, $"The owner's own words wait on session `{id}`: theirs win, so nothing was kept.");
        }

        if (QuestionBox.Read(home!, id) is not { } filed || !string.Equals(filed.Id, question, StringComparison.Ordinal))
        {
            return Refused(SessionSayRefusal.QuestionChanged, $"The question on file for session `{id}` is not `{question}`: nothing was kept.");
        }

        // The agent's mark, read again here from the file, so a question marked after the keeping is still the owner's.
        if (filed.Owner is { } owner)
        {
            return Refused(SessionSayRefusal.OwnerQuestion, $"Session `{id}`'s question is marked the owner's ({owner}): the supervisor answers none of it.");
        }

        if (!filed.Choices.Any(offered => string.Equals(offered.Label, choice, StringComparison.Ordinal)))
        {
            return Refused(SessionSayRefusal.NotOfferedChoice, $"\"{choice}\" is not one of the answers session `{id}` offered, exactly.");
        }

        if (await AskOfLineAsync(quests, sessions, session, inside).ConfigureAwait(false) is not { } askId)
        {
            return Refused(SessionSayRefusal.NoQuest, $"Session `{id}`'s quest is on no ask, so no go-aheads the supervisor could leave to the owner.");
        }

        if (asks is null || await asks.FindAsync(askId, inside).ConfigureAwait(false) is not { } ask)
        {
            return Refused(SessionSayRefusal.GoAheadUnread, $"Ask `#{askId}`'s go-aheads cannot be read here, so nothing was kept.");
        }

        return ask.GoAheads.Any(held => held.Answer is null)
            ? Refused(SessionSayRefusal.GoAheadOpen, $"A go-ahead on ask `#{askId}` is open: go-aheads are the owner's alone, so nothing was kept.")
            : null;
    }
}
