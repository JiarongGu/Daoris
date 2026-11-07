namespace Daoris.Knowledge;

/// <summary>What the person said to a session, kept on the ask its work is for.</summary>
internal sealed class SessionAskWords(QuestStore quests, SessionStore sessions, AskStore? asks)
{
    /// <summary>
    /// Keep what the person said to a session on the ask its work is for: verbatim, with
    /// when, the session it is said to and the quest that session works. Beside the say door,
    /// never inside it: the say door calls both, and what an answer does to a parked session is not this
    /// method's to decide.
    /// </summary>
    /// <remarks>
    /// <para><b>The ask is derived, never passed.</b> An intake names its ask; a driven session's quest is
    /// asked by one when its sender is <c>ask #id</c>, a chain step included. A quest one repository asked
    /// of another, and a conversation on no quest, are on no ask, and the answer says so rather than filing
    /// the words under an ask they were not given on.</para>
    /// <para><b>In any state.</b> An answer is kept after the answer moved the record, and a message while
    /// the session runs; a record of another machine's is not this machine's person talking.</para>
    /// </remarks>
    /// <exception cref="ArgumentException">For <see cref="AskWordKind.Asked"/>: the ask's own sentence is its record's.</exception>
    public async Task<AskWordOutcome> KeepOnAskAsync(
        string sessionId, AskWordKind kind, string? words, DateTimeOffset now, CancellationToken ct = default)
    {
        if (kind == AskWordKind.Asked)
        {
            throw new ArgumentException("the ask's own sentence is kept when it is asked, never by a session", nameof(kind));
        }

        var text = words?.Trim() ?? "";
        if (text.Length == 0)
        {
            return new(AskWordRefusal.Empty, "There are no words to keep: the person said nothing.");
        }

        var session = await sessions.FindAsync(sessionId, ct).ConfigureAwait(false);
        if (session is null || session.Origin is not null)
        {
            return new(AskWordRefusal.NotFound, $"No session `{sessionId}` of this machine's.");
        }

        const string Own = "so nothing was kept on one; its own record holds what was said.";
        var quest = session.Quest is { } questId ? await quests.FindAsync(questId, ct).ConfigureAwait(false) : null;
        var askId = await SaidKeeping.AskOfLineAsync(quests, sessions, session, ct).ConfigureAwait(false);
        if (askId is null)
        {
            return new(AskWordRefusal.NoAsk, (session.Quest, quest) switch
            {
                (null, _) => $"Session `{session.Id}` is a conversation on no ask, {Own}",
                (_, null) => $"Session `{session.Id}` works quest `#{session.Quest}`, which is not held here, {Own}",
                _ => $"Session `{session.Id}` works quest `#{quest.Id}`, which `{quest.From}` asked and whose line is on no ask, {Own}",
            });
        }

        var word = new AskWord(kind, text, now, session.Id, session.Quest);
        if (asks is null || !await asks.RecordWordAsync(askId, word, now, ct).ConfigureAwait(false))
        {
            return new(AskWordRefusal.NoAsk, $"Session `{session.Id}` is on ask `#{askId}`, which is not held here, {Own}");
        }

        return new(
            AskWordRefusal.None,
            $"Kept on ask `#{askId}`, as said to session `{session.Id}`"
            + (session.Quest is { } on ? $" on quest `#{on}`." : "."),
            askId, word);
    }
}
