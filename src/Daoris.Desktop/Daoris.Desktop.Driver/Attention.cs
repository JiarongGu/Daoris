namespace Daoris.Driver;

/// <summary>Why a person is being interrupted.</summary>
public enum AttentionKind
{
    /// <summary>A session stopped and can only be cleared by the person (D46's `awaiting-person`).</summary>
    Parked,

    /// <summary>A session ended, and the person did not ask for that.</summary>
    Ended,

    /// <summary>
    /// Starts wait because every account they may use is cooling (TOOL4d, D125 §4). Nothing about the work is wrong, and
    /// it starts by itself at the reset; it is said because the person may sign in to another account, or end the
    /// cool-off early, sooner. <see cref="AttentionEvent.Session"/> is empty: no session is concerned.
    /// </summary>
    Waiting,
}

/// <param name="State">The state it ended in, for <see cref="AttentionKind.Ended"/>; null for a park.</param>
/// <param name="Note">
/// What the session said about it — the analysis on a park, the conclusion's sentence on an end.
/// Absent is a state, not a gap: a session may park without explaining itself, and a surface that
/// required a note would have nothing to show for the one that did.
/// </param>
public sealed record AttentionEvent(
    AttentionKind Kind, string Session, string Repository, string? State = null, string? Note = null)
{
    /// <summary>
    /// The one line this is worth saying — a toast's title, and a headless console's line.
    /// </summary>
    /// <remarks>
    /// <b>Composed here so both doors say the same thing.</b> It is the driver's own sentence, which
    /// this platform has always rendered verbatim rather than translated (D24, and the reason
    /// `errors.DRIVER_REFUSED` is `{{message}}`): re-authoring these in two languages would mean two
    /// that drift, and a native toast has no catalogue to read from anyway.
    /// </remarks>
    public string Headline => Kind switch
    {
        AttentionKind.Parked => $"{Repository} — a session needs you",
        AttentionKind.Waiting => $"{Repository} — waits for an account",
        _ => $"{Repository} — a session {State}",
    };

    /// <summary>What it said about that, where it said anything. Null is a state, not a gap.</summary>
    public string? Detail => string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

    /// <summary>Both halves on one line, for a door that has only lines.</summary>
    public string Line => Detail is null ? Headline : $"{Headline}: {Detail}";
}

/// <summary>
/// What is worth interrupting a person for (working-surface design §4, SURF5b).
/// </summary>
/// <remarks>
/// <para><b>The judgement is here, and the delivery is not.</b> A toast is one way to say this; a
/// line on a headless machine's console is another, and both ask the same question (D50). Keeping the
/// decision in the library is also what makes it testable — a screen is not.</para>
///
/// <para><b>The two halves are found differently, and that is the design.</b> A <b>park</b> is a state
/// change on the record that nothing local performs — the session asks, through its own connector — so
/// it is seen by diffing what is active from tick to tick. An <b>end</b> is something this driver
/// concluded, so it is KNOWN rather than inferred, including whose decision it was.</para>
///
/// <para>🔴 <b>That asymmetry is what makes "never for an ending the person caused" structural.</b> An
/// end the driver did not conclude is one the person performed here — a resolve, an ended chat, a
/// stop — so it never reaches this at all and there is nothing to suppress. A toast telling somebody
/// what they just pressed is how people learn to dismiss toasts unread.</para>
///
/// <para><b>It says a thing once.</b> A parked session is parked on every tick until somebody answers
/// it; reporting that every poll interval would be the same failure in a different shape.</para>
///
/// <para><b>A wait for an account is the third thing</b> (TOOL4d, D125 §4): starts held because every account they may
/// use is cooling, from the report's <see cref="TickReport.Waits"/>. Said once per cool-off, since it lasts every look
/// until its reset; nothing is wrong with the work, and it starts by itself then.</para>
///
/// <para><b>A conversation's end is deliberately not reported</b>, though a conversation that PARKS
/// is — parks come from the tick and are blind to how the session was started. The reason is design
/// §4's own: this exists because nobody should have to watch an <i>unattended</i> session, and a
/// conversation is something the person is in, whose ending appears in the stream they are looking
/// at. `ChatRunner` concludes outside the tick and so never reaches here at all, which is the
/// mechanism; this paragraph is the intent, so the next reader can tell one from the other. If real
/// use shows a chat left running and crashing silently, the fix is to feed its `onEnded` in — the
/// repository is already remembered here.</para>
/// </remarks>
public sealed class AttentionWatch
{
    private Dictionary<string, string>? _seen;

    // The waits already said, by account, with the cool-off each was for (TOOL4d): a wait lasts every look until its
    // reset, and a look that planned no start says nothing of it, so the cool-off — not the look — is what is said once.
    private readonly Dictionary<string, DateTimeOffset> _waits = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The sessions whose state is being remembered — bounded by what is active.</summary>
    public IReadOnlyList<string> Watching => _seen is null ? [] : [.. _seen.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// What this tick is worth saying, in the order it was found.
    /// </summary>
    /// <remarks>
    /// <b>The first look is a baseline, never a backlog.</b> A machine that has just started has no
    /// previous view, and a session already parked then is one the person was told about on an
    /// earlier run — so the first observation reports nothing and only records what it saw. A shell
    /// that toasted every parked session on every launch is a shell people close.
    /// </remarks>
    public IReadOnlyList<AttentionEvent> Observe(TickReport report)
    {
        var now = report.Active.ToDictionary(
            session => session.Id, session => session.State, StringComparer.Ordinal);

        var previous = _seen;
        _seen = now;

        // A wait whose cool-off was not said yet (TOOL4d, D125 §4) — recorded on the first look too, which says nothing.
        var waits = report.Waits.Where(Unsaid).ToList();
        if (previous is null) return [];

        var events = new List<AttentionEvent>();
        events.AddRange(waits.Select(wait => new AttentionEvent(
            AttentionKind.Waiting, "", string.Join(", ", wait.Repositories), Note: wait.Sentence)));

        foreach (var session in report.Active)
        {
            if (!SessionStates.IsParked(session.State)) continue;
            if (previous.TryGetValue(session.Id, out var was) && SessionStates.IsParked(was)) continue;

            events.Add(new AttentionEvent(
                AttentionKind.Parked, session.Id, session.Repository, Note: session.Note));
        }

        foreach (var ended in report.Concluded)
        {
            if (ended.ByPerson) continue;
            events.Add(new AttentionEvent(
                AttentionKind.Ended, ended.Session, ended.Repository, ended.State, ended.Note));
        }

        return events;
    }

    /// <summary>Whether this wait's cool-off is not yet said; it is marked said either way.</summary>
    private bool Unsaid(AccountWait wait)
    {
        var account = $"{wait.Agent}/{wait.Account ?? ""}";
        var said = _waits.TryGetValue(account, out var until) && until == wait.Until;
        _waits[account] = wait.Until;
        return !said;
    }
}

/// <summary>The one spelling of the state that means "only the person can clear this" (D46).</summary>
/// <remarks>
/// A constant rather than a literal because three things compare against it — the watch, the shell's
/// surface and the ledger — and a fourth will; three copies of a string disagree eventually, and the
/// disagreement is silent.
/// </remarks>
public static class SessionStates
{
    public const string AwaitingPerson = "awaiting-person";

    public static bool IsParked(string? state) =>
        string.Equals(state, AwaitingPerson, StringComparison.OrdinalIgnoreCase);
}
