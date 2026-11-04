namespace Daoris.Knowledge;

/// <summary>
/// How a person's word is kept on a record (MSG1a, ANSWER1b): the word's id and file names, the answer line a park's note gains, and the
/// one keep every door that stores a person's word goes through.
/// </summary>
internal static class SaidKeeping
{
    /// <summary>The answer a park keeps where its go-aheads sent it on with no words of the person's (GOAHEAD2): what it goes on with.</summary>
    internal const string CarryOn = "carry on.";

    /// <summary>A new word's id: random, as a session's is, and never <see cref="SessionStore.AnswerWordId"/>.</summary>
    internal static string NewWordId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Each file's name alone (MSG1a, D137 §2.4): what is kept of a file given with the words is what a person reads, never
    /// where it is on this machine. Read after either separator, whatever machine it was written on.
    /// </summary>
    internal static IReadOnlyList<string> Names(IReadOnlyList<string>? files) =>
        (files ?? [])
            .Select(file => file?.Trim() ?? "")
            .Select(file => file[(file.LastIndexOfAny(['/', '\\']) + 1)..])
            .Where(name => name.Length > 0)
            .ToList();

    /// <summary>
    /// The note an answer leaves (ANSWER1b): what the session asked, then what it was told, since the conversation that
    /// goes on and a session that carries the quest on are both read from this record, and an answer without its
    /// question is half a conversation. A second answer adds its own line after the first (MSG1a).
    /// </summary>
    /// <remarks>
    /// Its lines with their codes beside the English (LANG1a, the language design §4 rows 64–65): the record's parts, or its
    /// note carried whole, or the ledger's own line where it had none; then <c>Answered:</c> and the person's words, a part of
    /// their own. Daoris's own words that answer a park (COWORK1b: a reopen, a go-on the person's press made) are a part by
    /// <c>daoris</c>, never the person's.
    /// </remarks>
    internal static (string Note, string Parts) AnsweredNote(Session parked, string said, string? from = null)
    {
        const string Asked = "It stopped to ask the person; its question is in its transcript.";
        var note = (parked.Note ?? Asked) + $"\n\nAnswered: {said}";
        NoteLine[] answered = [NoteLine.Coded(LedgerNoteCodes.Answered, "Answered:"), NoteLine.Said(said, from ?? "person")];
        var parts = parked.Note is null
            ? NoteParts.After(null, null, [NoteLine.Coded(LedgerNoteCodes.Parked, Asked), .. answered])
            : NoteParts.After(parked.NoteParts, parked.Note, answered);
        return (note, parts);
    }

    /// <summary>
    /// Keep <paramref name="said"/> as a park's answer, joining any before it (MSG1a), with its line on the note: inside the
    /// store's lock, by a caller that found the record parked there.
    /// </summary>
    internal static Task<Session?> KeepAnswerAsync(SessionStore sessions, Session parked, string said, DateTimeOffset now, CancellationToken inside)
    {
        var word = new SaidWord(NewWordId(), said, now, []);
        var (note, parts) = AnsweredNote(parked, said);
        return KeepOwnersWordAsync(sessions, parked, word, note, parts, inside);
    }

    /// <summary>
    /// Keep a word on a record inside its door's own step, the owner's words superseding (SUPERVISOR1a-i, the supervisor design §6.3):
    /// where the person's word lands on a park, every waiting <see cref="SaidFrom.Supervisor"/> word is taken back in the same step, so no
    /// read ever sees both and the park goes on with the owner's words alone. Every door that keeps a person's word keeps it here: the say,
    /// the answer, a reword and the go-aheads' carry-on; a sweep test holds that no other keeps one.
    /// </summary>
    internal static async Task<Session?> KeepOwnersWordAsync(
        SessionStore sessions, Session session, SaidWord word, string? note, string? parts, CancellationToken inside)
    {
        if (word.Persons && session.State == SessionState.AwaitingPerson
            && session.Said.Where(each => each.Supervisors).Select(each => each.Id).ToList() is { Count: > 0 } superseded)
        {
            await sessions.TakeSaidAsync(session.Id, superseded, inside).ConfigureAwait(false);
        }

        return await sessions.KeepSaidAsync(session.Id, word, note, inside, noteParts: parts).ConfigureAwait(false);
    }

    /// <summary>The ask a session's line is on: its entry's origin where that is <c>ask:&lt;id&gt;</c>, else none (COMMS1c).</summary>
    internal static async Task<string?> AskOfLineAsync(QuestStore quests, SessionStore sessions, Session session, CancellationToken ct)
    {
        var origin = await SessionEntries.OriginOfAsync(session, new StoreEntryFacts(quests, sessions), ct).ConfigureAwait(false);
        return origin.StartsWith("ask:", StringComparison.Ordinal) ? origin["ask:".Length..] : null;
    }
}
