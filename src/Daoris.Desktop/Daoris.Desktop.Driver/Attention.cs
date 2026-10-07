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

    /// <summary>
    /// A quest parked on its failed sessions here (SESSUX1i, D126 §4.7): only the person's *Try again* starts it again.
    /// <see cref="AttentionEvent.Session"/> is its last session, which a notification opens, and
    /// <see cref="AttentionEvent.Quest"/> the quest.
    /// </summary>
    QuestParked,
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
    /// <summary>For <see cref="AttentionKind.QuestParked"/>: the quest that parked. Null for every other kind.</summary>
    public string? Quest { get; init; }

    /// <summary>For <see cref="AttentionKind.QuestParked"/>: how many sessions failed, as the planner counted to park it; null where unsaid.</summary>
    public int? Strikes { get; init; }

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
        AttentionKind.QuestParked => $"{Repository} — `#{Quest}` parked after " + Strikes switch
        {
            null => "its failed sessions",
            1 => "1 failed session",
            var count => $"{count} failed sessions",
        },
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
/// until its reset; nothing is wrong with the work, and it starts by itself then. A wait on accounts not signed in with none
/// cooling (UX6d1) is said once per set of accounts: it lasts until a sign-in, which only the person makes.</para>
///
/// <para><b>A quest's park is the fourth</b> (SESSUX1i, D126 §4.7): a quest the planner parked on its failed sessions
/// here, from the parks a door read for that look (<see cref="QuestParkReader"/>). Said once per park, a park being its
/// last session: a hold that hides it for a look is the same park, and *Try again* then new failures make a new one. Never
/// for the person's stop, which is no park. Its notice carries the last failure's note, so that failure's own end in the
/// same look is not said beside it.</para>
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
    // reset, and a look that planned no start says nothing of it, so the cool-off — not the look — is what is said once. A
    // wait on signed-out accounts (UX6d1) is said once per set of accounts, as the log's line is, and kept apart from a
    // cool-off of the tool's own sign-in, whose account is null too.
    private readonly Dictionary<string, string> _waits = new(StringComparer.OrdinalIgnoreCase);

    // The quests' parks already said or seen at the first look, by quest, with the last session each park was for
    // (SESSUX1i). Bounded by what the planner still considers. A null session is a park seen at a first look whose records
    // were not read, taken as said when they are.
    private Dictionary<string, string?>? _parks;

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
    /// <param name="parks">
    /// The quests' parks a door read for this look (<see cref="QuestParkReader.LookAsync"/>), or null where it read none:
    /// they had not changed, or the records did not answer. Null says nothing of a park, and forgets none.
    /// </param>
    public IReadOnlyList<AttentionEvent> Observe(TickReport report, IReadOnlyList<QuestPark>? parks = null)
    {
        var now = report.Active.ToDictionary(
            session => session.Id, session => session.State, StringComparer.Ordinal);

        var previous = _seen;
        _seen = now;

        // A wait whose cool-off was not said yet (TOOL4d, D125 §4) — recorded on the first look too, which says nothing.
        var waits = report.Waits.Where(Unsaid).ToList();
        // And a quest's park not said yet (SESSUX1i), the same way.
        var parked = UnsaidParks(report, parks);
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

        events.AddRange(parked.Select(park => new AttentionEvent(
            AttentionKind.QuestParked, park.Session ?? "", park.Repository, Note: park.Note)
        {
            Quest = park.Quest,
            Strikes = park.Strikes,
        }));

        // The last failure is said by its quest's park, which carries its note.
        var saidByPark = parked.Select(park => park.Session).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var ended in report.Concluded)
        {
            if (ended.ByPerson || saidByPark.Contains(ended.Session)) continue;
            events.Add(new AttentionEvent(
                AttentionKind.Ended, ended.Session, ended.Repository, ended.State, ended.Note));
        }

        return events;
    }

    /// <summary>
    /// The parks among <paramref name="parks"/> not said yet; each is marked said either way. The first look marks every
    /// quest the planner parked and says none.
    /// </summary>
    private List<QuestPark> UnsaidParks(TickReport report, IReadOnlyList<QuestPark>? parks)
    {
        if (_parks is null)
        {
            _parks = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var consideration in report.Considerations.Where(c => c.Verdict == StartVerdict.Exhausted))
            {
                _parks[consideration.Quest.Id] = null;
            }

            foreach (var park in parks ?? []) _parks[park.Quest] = park.Session;
            return [];
        }

        // A quest the planner no longer considers is done, declined or gone, and its park with it.
        var considered = report.Considerations.Select(c => c.Quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in _parks.Keys.Where(quest => !considered.Contains(quest)).ToList()) _parks.Remove(quest);

        var unsaid = new List<QuestPark>();
        foreach (var park in parks ?? [])
        {
            var said = _parks.TryGetValue(park.Quest, out var session) && (session is null || session == park.Session);
            _parks[park.Quest] = park.Session;
            if (!said) unsaid.Add(park);
        }

        return unsaid;
    }

    /// <summary>Whether this wait's cool-off, or its set of signed-out accounts, is not yet said; it is marked said either way.</summary>
    private bool Unsaid(AccountWait wait)
    {
        var (account, what) = wait.Until is { } until
            ? ($"{wait.Agent}/{wait.Account ?? ""}", until.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture))
            // No profile name has a colon, so this is no account's key.
            : ($"{wait.Agent}/:signed-out", string.Join(",", wait.SignedOut.Order(StringComparer.OrdinalIgnoreCase)).ToUpperInvariant());
        var said = _waits.TryGetValue(account, out var was) && was == what;
        _waits[account] = what;
        return !said;
    }
}

/// <summary>
/// The quests' parks as the doors read them (SESSUX1i, D126 §4.6, §4.7): what the watch says a park with, and what the
/// tick hands Overview's row. One reader for both doors, the shell's loop and the headless host, so they say one thing.
/// </summary>
/// <remarks>
/// <para><b>Read only when what the planner parked changed.</b> The records are every session ever run (D126 M9), and a
/// park lasts every look until *Try again*; reading them each look for the same answer would double the look's largest
/// read. A new park always changes the set: its quest left it to run the sessions that failed.</para>
///
/// <para><b>A read that fails is asked again at the next look</b>, and says nothing meanwhile: a park is never lost to one
/// refused read, nor said from a guess.</para>
///
/// <para>Called by one loop, one look at a time, so it holds no lock; <see cref="Latest"/> is read beside it.</para>
/// </remarks>
public sealed class QuestParkReader
{
    private string? _read;
    private volatile IReadOnlyList<QuestPark> _latest = [];

    /// <summary>The parks as last read, in the planner's order; none before any read.</summary>
    public IReadOnlyList<QuestPark> Latest => _latest;

    /// <summary>
    /// This look's parks, read from the records where the quests the planner parked changed since the last read; null where
    /// they did not, or where the records could not be read.
    /// </summary>
    /// <param name="forgivenAt">RETRY1's mark for a quest (<see cref="DriverConfig.ForgivenAt"/>): the planner counts from it.</param>
    /// <param name="records">The records door (<see cref="SessionRecords.ReadAsync"/>); a test hands in its own.</param>
    public async Task<IReadOnlyList<QuestPark>?> LookAsync(
        TickReport report, Func<string, int> forgivenAt, Func<CancellationToken, Task<string>> records,
        CancellationToken ct = default)
    {
        var parked = report.Considerations.Where(c => c.Verdict == StartVerdict.Exhausted).ToList();
        var signature = Considerations.Signature(parked);
        if (signature == _read) return null;

        IReadOnlyList<QuestPark> parks = [];
        if (parked.Count > 0)
        {
            try
            {
                parks = SessionGroups.Parks(SessionLook.From(await records(ct).ConfigureAwait(false), [], parked, forgivenAt));
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              || (error is TaskCanceledException && !ct.IsCancellationRequested))
            {
                return null;
            }
        }

        _read = signature;
        _latest = parks;
        return parks;
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
