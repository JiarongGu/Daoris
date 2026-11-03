namespace Daoris.Knowledge;

/// <summary>What the solicited-answer door did with a hand (COMMS1b, the comms design §4.2).</summary>
internal enum SolicitedFate
{
    /// <summary>The word was kept on the line's head now.</summary>
    Kept,

    /// <summary>The key's word already waits: <see cref="SolicitedOutcome.Session"/> holds it and <see cref="SolicitedOutcome.Word"/> is it.</summary>
    Waiting,

    /// <summary>The key's word left a record of the line: <see cref="SolicitedOutcome.SettledOn"/> names it; nothing was kept.</summary>
    Settled,

    /// <summary>Nothing was kept; <see cref="SolicitedOutcome.Refusal"/> says why.</summary>
    Refused,
}

/// <summary>Why the door kept nothing.</summary>
internal enum SolicitedRefusal
{
    None,

    /// <summary>The record named is not one of this machine's.</summary>
    NotFound,

    /// <summary>The line's head cannot take a word now, or the line cannot be read whole.</summary>
    CannotTake,

    /// <summary>The caller's own once-guard held: the answer went before, and no key shows it.</summary>
    NotHanded,
}

/// <summary>A solicited answer to hand to a line: the case's identity and words, and how the caller's door reads the hand.</summary>
internal sealed record SolicitedAsk(string Named, Session? Record, string Key, string Text, string Subject, string Reaches)
{
    /// <summary>
    /// A key whose word, waiting on the head, is this ask's too (a second hand of one opinion answers its first hand's retry as waiting); its
    /// settlement answers nothing, since the second hand exists because the first word left a record unanswered.
    /// </summary>
    public string? EarlierKey { get; init; }

    /// <summary>Set on a helper's or a partner's word: the record that answered (<see cref="SaidWord.Helper"/>).</summary>
    public string? Helper { get; init; }

    /// <summary>Set on an opinion's findings: the opinion (<see cref="SaidWord.By"/>).</summary>
    public string? By { get; init; }

    /// <summary>The ask's level kept beside the word; none for an ordinary one.</summary>
    public string? Level { get; init; }

    /// <summary>The driver that runs a working driven head hands the word for its run's next step.</summary>
    public bool Running { get; init; }

    /// <summary>The caller asks to be told a key the line settled (row p).</summary>
    public bool Settled { get; init; }

    /// <summary>
    /// The caller's once-guard, asked only where a new word would be kept (never for a retry): a sentence refuses the hand as
    /// <see cref="SolicitedRefusal.NotHanded"/>.
    /// </summary>
    public Func<Session, CancellationToken, Task<string?>>? BeforeKeep { get; init; }
}

internal sealed record SolicitedOutcome(SolicitedFate Fate, string Message)
{
    public SolicitedRefusal Refusal { get; init; }

    /// <summary>The line's head the word was kept on, or holds it.</summary>
    public Session? Session { get; init; }

    public SaidWord? Word { get; init; }

    public string? SettledOn { get; init; }
}

/// <summary>
/// The one door a solicited answer goes through (COMMS1b, the comms design §4.2): a helper's answer, a partner's and an opinion's findings,
/// kept once under <c>&lt;case&gt;:&lt;id&gt;</c> on the head of the line the say door reads, inside the caller's exclusive step. One walk
/// (<see cref="Lines.WalkAsync"/>), one settlement (<see cref="SaidKeys"/>), one table of the head's state.
/// </summary>
internal static class SolicitedAnswers
{
    public static async Task<SolicitedOutcome> KeepAsync(SessionStore sessions, SolicitedAsk ask, DateTimeOffset now, CancellationToken inside)
    {
        var named = ask.Record;
        if (named is not { Origin: null })
        {
            return Refused(SolicitedRefusal.NotFound, $"There is no session `{ask.Named}` of this machine's to hand {ask.Subject} to");
        }

        var line = await Lines.WalkAsync(sessions, named, inside).ConfigureAwait(false);

        // Row p: asked before any refusal but the record's absence, so a retry after the take is answered as kept whatever the line is doing
        // now. A word waiting on the named record is answered below; one waiting on the head is answered here, naming where.
        if (ask.Settled && !named.Said.Any(word => string.Equals(word.Key, ask.Key, StringComparison.Ordinal)))
        {
            if (line.Head is { } at && at.Said.FirstOrDefault(word => string.Equals(word.Key, ask.Key, StringComparison.Ordinal)) is { } there)
            {
                return Waiting(ask, at, there);
            }

            var (held, whole) = await SaidKeys.SettledOnLineAsync(sessions, named, line, ask.Key, now, inside).ConfigureAwait(false);
            if (!whole)
            {
                return Refused(SolicitedRefusal.CannotTake,
                    $"Session `{named.Id}`'s line runs past {Lines.Bound} parts, so whether {ask.Subject} was taken already is not read and no part takes it");
            }

            if (held is not null)
            {
                return new SolicitedOutcome(
                    SolicitedFate.Settled,
                    $"{Cap(ask.Subject)} is already settled: its word left session `{held.Record}` and nothing more was kept.")
                { SettledOn = held.Record };
            }
        }

        if (line.End == LineEnd.Cycle)
        {
            return Refused(SolicitedRefusal.CannotTake,
                $"Session `{named.Id}`'s line loops back on a part it already passed, so it has no newest part and no part takes {ask.Subject}");
        }

        if (line.Head is not { } head)
        {
            return Refused(SolicitedRefusal.CannotTake,
                $"Session `{named.Id}`'s line runs past {Lines.Bound} parts, so its newest part is not read and no part it left takes {ask.Subject}");
        }

        if (head.Said.FirstOrDefault(word => string.Equals(word.Key, ask.Key, StringComparison.Ordinal)
                || (ask.EarlierKey is not null && string.Equals(word.Key, ask.EarlierKey, StringComparison.Ordinal))) is { } waiting)
        {
            return Waiting(ask, head, waiting);
        }

        // The table of the head's state (§4.2). Parked and unanswered waits for the person; every other refusal sends the answer to the person.
        string? cannot = head switch
        {
            { Origin: not null } => $"There is no session `{head.Id}` of this machine's to hand {ask.Subject} to",
            { Ask: { } intake } => $"Session `{head.Id}` is the intake for ask `#{intake}`, which is answered through its ask",
            { Opinion: { } reads } => $"Session `{head.Id}` read the work for second opinion `{reads}`: a pass takes no words",
            { Serves: not null } => $"Session `{head.Id}` is a helper itself: it takes no words",
            { State: SessionState.StoodDown } => $"Session `{head.Id}` stood down, so it has nothing to go on with",
            { State: SessionState.AwaitingPerson, PersonSaid: false } =>
                $"Session `{head.Id}` waits on the person's answer: {ask.Reaches} reaches it at a turn's end, once it is answered",
            // A park the person answered takes the word beside their answer, so its go-on hands both in one prompt.
            { State: SessionState.AwaitingPerson } => null,
            // An open conversation between turns is working: the chat runner says the word as its next turn. One still being started is not open yet.
            { Kind: SessionKind.Chat, State: SessionState.Working } => null,
            { Kind: SessionKind.Driven, State: SessionState.Working } when ask.Running => null,
            { Active: true } =>
                $"Session `{head.Id}` is {Session.Spell(head.State)}: {ask.Reaches} reaches a running session at its turn's end, never into a step in flight",
            _ => null,
        };
        if (cannot is not null) return Refused(SolicitedRefusal.CannotTake, cannot);

        if (ask.BeforeKeep is { } guard && await guard(head, inside).ConfigureAwait(false) is { } once)
        {
            return Refused(SolicitedRefusal.NotHanded, once);
        }

        var word = new SaidWord(
            Guid.NewGuid().ToString("N")[..8], ask.Text.Trim(), now, [], Reopens: true, Helper: ask.Helper, By: ask.By,
            Level: ask.Level is { } said && SaidLevel.Known(said) ? said : null, Key: ask.Key);
        var kept = await sessions.KeepSaidAsync(head.Id, word, ct: inside).ConfigureAwait(false);
        return new SolicitedOutcome(SolicitedFate.Kept, $"Kept {ask.Subject} on session `{head.Id}`.") { Session = kept, Word = word };
    }

    private static SolicitedOutcome Waiting(SolicitedAsk ask, Session holder, SaidWord word) =>
        new(SolicitedFate.Waiting, $"{Cap(ask.Subject)} waits on session `{holder.Id}` already, as word `{word.Id}`: nothing more was kept.")
        {
            Session = holder,
            Word = word,
        };

    private static SolicitedOutcome Refused(SolicitedRefusal why, string message) =>
        new(SolicitedFate.Refused, message) { Refusal = why };

    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
