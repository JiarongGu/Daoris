namespace Daoris.Driver;

/// <summary>
/// Which account a resume asks for, and what holds it (MSG1g, D137 §2.2's account paragraph and point 7): its record's own,
/// since a harness keeps a conversation in the configuration home of the account it ran on (D131 §1), whatever the walk would
/// choose for a start (D130). What holds it is said by TOOL6e's codes (<see cref="NextHold"/>). Pure.
/// </summary>
/// <remarks>
/// <para><b>What cannot run there at all is said first</b>: an account gone from this machine, one the scope no longer uses,
/// one kept for conversations, one its provider refused. Each would still be unable once a cool-off passed, so waiting for it
/// would wait for nothing: the words are carried on at once. Then a cool-off, which passes by itself: the words wait for its
/// reset (D125 §2). Then a sign-in the agent says is gone, which only a probe tells, so the roster asks it of an account still
/// ready.</para>
///
/// <para><b>A conversation's account is asked for as its picker names one</b> (D130 point 4's exception): the record does not
/// keep whether it was picked, and a conversation may run on any account the person picks, so neither the list nor a kept
/// account stops one going on. A driven session is never picked, so both do.</para>
/// </remarks>
public static class ResumeAccount
{
    /// <summary>What holds the record's own account from carrying its words; <see cref="NextHold.Ready"/> where nothing does.</summary>
    /// <param name="account">The account the record ran on, or null for the tool's own sign-in.</param>
    /// <param name="state">That account as the roster finds it: cooling or refused before any probe, signed out where a probe said so.</param>
    /// <param name="present">The agent's accounts on this machine.</param>
    public static NextHold Judge(
        string? account, RotationScope scope, StartKind kind, AccountState state, IReadOnlyCollection<string> present)
    {
        if (account is not null && !present.Contains(account, StringComparer.OrdinalIgnoreCase)) return NextHold.Missing;
        if (kind == StartKind.Driven && !InScope(account, scope)) return NextHold.Outside;
        if (kind == StartKind.Driven && account is not null && AccountRotation.KeptOf(scope) is { } kept
            && string.Equals(kept, account, StringComparison.OrdinalIgnoreCase))
        {
            return NextHold.Kept;
        }

        return state.Readiness switch
        {
            AccountReadiness.Refused => NextHold.Refused,
            AccountReadiness.Cooling => NextHold.Cooling,
            AccountReadiness.SignedOut => NextHold.SignedOut,
            _ => NextHold.Ready,
        };
    }

    /// <summary>
    /// Whether the scope uses the account (D130 §3.1): its list names it, or, with no list, it is the scope's one account,
    /// the tool's own sign-in where the scope names none.
    /// </summary>
    public static bool InScope(string? account, RotationScope scope) =>
        scope.List.Count > 0
            ? account is not null && scope.List.Contains(account, StringComparer.OrdinalIgnoreCase)
            : string.Equals(account ?? "", scope.Begins ?? "", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The account a resume runs on, and what held its record's own (MSG1g): what <see cref="HarnessRoster.ResumeAsync"/> answers,
/// and what the driver and the chat runner act on.
/// </summary>
/// <param name="Selection">
/// What the start takes: its record's own account where that carries the words; while it cools and nobody chose otherwise, a
/// hold carrying its cool-off; else the walk's pick, as any start (D130 §16.3), or, where nothing walked, a refusal.
/// </param>
/// <param name="Own">What held the record's own account; <see cref="NextHold.Ready"/> where the resume runs on it.</param>
public sealed record ResumeChoice(HarnessSelection Selection, NextHold Own)
{
    /// <summary>The person chose a new session while its account cooled (*Go on in a new session*).</summary>
    public bool NewSession { get; init; }

    /// <summary>The words wait for the record's own account's reset: it cools, and nobody chose a new session.</summary>
    public bool Waits => Own == NextHold.Cooling && !NewSession;

    /// <summary>
    /// Why the words go elsewhere, where they do (the reason <c>account</c>, with what held the account as its detail); null
    /// where the resume runs on its own account or waits for it.
    /// </summary>
    public ContinueReason? Elsewhere => Own is NextHold.Ready or NextHold.Near || Waits ? null : ContinueWhy.AccountHeld(Own, NewSession);
}

/// <summary>
/// What a resume's account says (MSG1g): the hold's sentence and its doors, on this machine only, since they name the account
/// and a command; and the coded line a note adds after the reason <c>account</c>, which names no account, since the note
/// travels (D125 §3.6).
/// </summary>
public static class ResumeWords
{
    /// <summary>
    /// Why the words wait, for a start's hold and the record's conversation: the account, its reset and why it lasts until then,
    /// and that the conversation is there. The tool's own sign-in ends its cool-off on a refresh (D125 §3.7), said as a hold says it.
    /// </summary>
    public static string Waits(CoolingEntry cooling, TimeZoneInfo zone)
    {
        var said = $"{CoolingWords.Who(cooling)} is cooling until {CoolingWords.When(cooling.Until, zone)}, {CoolingWords.Why(cooling)}, "
                   + "and its conversation is on that account, so your words wait to go on in it then.";
        return cooling.Account is null
            ? $"{said} If you have signed in to another account at your own terminal since, refresh Settings → Agents."
            : said;
    }

    /// <summary>The door out of the wait where a new session carries the words on: a taken or open quest's session (D137 §2.2).</summary>
    public static string NewSessionDoor(string session) =>
        $"To go on now in a new session instead, without that conversation: `daoris-driver sessions go-on-new {session}`.";

    /// <summary>The door out of the wait where nothing carries the words on by itself: a conversation, or a closed quest's session.</summary>
    public static string ChatDoor(string repository) =>
        $"To start a conversation with these words now instead, without that one: `daoris-driver chat --repository {repository}`.";

    /// <summary>
    /// The line a note adds after the reason <c>account</c>: why the record's own account could not carry the words, coded
    /// (LANG1a). Null for a hold that carries nothing on: ready runs there, and a cool-off nobody chose to leave waits.
    /// </summary>
    public static Noted? Line(NextHold hold, bool chosen = false) => (hold, chosen) switch
    {
        (NextHold.Cooling, true) => Noted.Of(NoteCodes.AccountResumeNewSession, "You chose a new session over waiting for that account."),
        (NextHold.SignedOut, _) => Noted.Of(NoteCodes.AccountResumeSignedOut, "That account is not signed in any more."),
        (NextHold.Refused, _) => Noted.Of(NoteCodes.AccountResumeRefused, "Its provider refused that account."),
        (NextHold.Missing, _) => Noted.Of(NoteCodes.AccountResumeGone, "That account is not on this machine any more."),
        (NextHold.Outside, _) => Noted.Of(NoteCodes.AccountResumeOutside, "This work no longer uses that account."),
        (NextHold.Kept, _) => Noted.Of(NoteCodes.AccountResumeKept, "That account is kept for conversations, which driven work never runs on."),
        _ => null,
    };
}
