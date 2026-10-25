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
        // A record begun in the person's own sign-in is judged against no scope (POOL1a, D197 point 3; Fable's F1): it goes on there,
        // and the set, whose order names every account of Daoris's, never displaces it.
        if (kind == StartKind.Driven && account is not null && !InScope(account, scope)) return NextHold.Outside;
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

    /// <summary>
    /// The record an account's limit ended (LIMITRESUME1b, D206; the limit-resume design §3.3 step 2): its words go where the line goes,
    /// with a briefed part on another account, never waiting for its own to reset while another could take them (D137 point 7 amended).
    /// </summary>
    public bool Limited { get; init; }

    /// <summary>
    /// The record's own account cools and another account of the line's choices is ready (LIMITSWITCH1, D137's note of 2026-10-25, point 7
    /// amended): its words go on now in a briefed part there, as a limited record's do, though no limit ended it.
    /// </summary>
    public bool Switched { get; init; }

    /// <summary>
    /// The words wait: the record's own account cools, nobody chose a new session, no limit ended it, and no account of the line's
    /// choices is ready (LIMITSWITCH1), so they wait for the earliest reset of any of them, which <see cref="Selection"/> carries.
    /// </summary>
    public bool Waits => LineAccount.CoolingHolds(Own, new LineTrigger(NewSession, Limited)) && !Switched;

    /// <summary>
    /// What the account rule answered (FOUNDATION1a-i, <see cref="LineAccount.Decide"/>) for this resume, of which the flags above are the
    /// reading the callers have always made; null for a choice made by hand.
    /// </summary>
    public LineAnswer? Answer { get; init; }

    /// <summary>
    /// The own account's reading does not offer the record's asked model (D205 §3.5(a), MODELROLE1c2): it cannot carry the words, which
    /// go to a new record on another account of the same choice, the old record left as it was.
    /// </summary>
    public bool NotOffered { get; init; }

    /// <summary>
    /// Why the words go elsewhere, where they do (the reason <c>account</c>, with what held the account as its detail); null
    /// where the resume runs on its own account or waits for it.
    /// </summary>
    public ContinueReason? Elsewhere => NotOffered
        ? ContinueWhy.Of(ContinueWhy.Account) with { Detail = ResumeWords.NotOfferedLine }
        : Own is NextHold.Ready or NextHold.Near || Waits ? null : ContinueWhy.AccountHeld(Own, NewSession);

    /// <summary>
    /// The conversation's line while its words wait (CONVNOTE2g): <see cref="ResumeWords.NotYet"/> and why, by code
    /// (<see cref="ResumeWords.Waiting"/>), composed where the roster reads the names its English says; the site adds the door
    /// out of the wait (<see cref="ResumeWords.NotYetOn"/>). Null where the words do not wait, and where nothing composed it.
    /// </summary>
    public Noted? Waiting { get; init; }
}

/// <summary>
/// The chat runner's holds for a reason other than an account's cool-off (CONVNOTE2g2): each the reason's English, as the runner
/// returns it, and the conversation's line, <see cref="ResumeWords.NotYet"/>, why and that the words wait, by code, its English
/// the line the runner always wrote.
/// </summary>
public static class HeldWords
{
    private const string Tail = "Your words wait on it.";

    /// <summary>The repository has no checkout here any more.</summary>
    public static (string Why, Noted Line) NoCheckout(string repository)
    {
        var why = $"`{repository}` has no checkout on this machine any more";
        return (why, Whole(NoteCodes.HeldNoCheckout, why, ("repository", repository)));
    }

    /// <summary>A replay holds the repository's trees.</summary>
    public static (string Why, Noted Line) Replaying(string repository)
    {
        var why = TreeLock.Replaying(repository);
        return (why, Whole(NoteCodes.HeldReplaying, why, ("repository", repository)));
    }

    /// <summary>Ask Daoris is answering in another conversation of its room.</summary>
    public static (string Why, Noted Line) Answering(string session)
    {
        var why = AnsweringWhy(session);
        return (why, Whole(NoteCodes.HeldAnswering, why, ("session", session)));
    }

    /// <summary>The reason <see cref="Answering"/> says, which a new conversation's refusal says too.</summary>
    public static string AnsweringWhy(string session) =>
        $"Ask Daoris is answering in conversation `{session}`; once its turn ends, a word said here takes these with it";

    /// <summary>This machine's driver.json could not be read, the program's message after it as its words (it may hold a path).</summary>
    public static (string Why, Noted Line) ConfigUnread(string message)
    {
        var why = $"this machine's driver.json could not be read: {message}";
        var lead = $"{ResumeWords.NotYet}this machine's driver.json could not be read:";
        return (why, Noted.Of(NoteCodes.HeldConfigUnread, lead).Then(" ", Noted.Said(Trimmed(message), NoteBy.Program)).Then(". ", TailLine()));
    }

    /// <summary>A reason that is a program's message alone (the roster's refusal, the ledger's): its words, between lead and tail.</summary>
    public static (string Why, Noted Line) Program(string message) =>
        (message, Noted.Of(NoteCodes.HeldLead, ResumeWords.NotYet.TrimEnd())
            .Then(" ", Noted.Said(Trimmed(message), NoteBy.Program)).Then(". ", TailLine()));

    /// <summary>
    /// The roster's refusal as the reason (ROSTERREFUSE1): its coded parts between lead and tail where the selection carries them
    /// and they say the refusal to the byte, otherwise <see cref="Program"/>'s words.
    /// </summary>
    public static (string Why, Noted Line) Refused(HarnessSelection selection)
    {
        var words = Program(selection.Refusal!);
        if (selection.RefusalNote is not { } coded) return words;

        var line = Noted.Of(NoteCodes.HeldLead, ResumeWords.NotYet.TrimEnd()).Then(" ", coded).Then(" ", TailLine());
        return line.Note == words.Line.Note ? (words.Why, line) : words;
    }

    private static string Trimmed(string message) => message.TrimEnd('.', ' ');

    private static Noted TailLine() => Noted.Of(NoteCodes.HeldTail, Tail);

    private static Noted Whole(NoteCode code, string why, params (string Name, object? Value)[] values) =>
        Noted.Of(code, $"{ResumeWords.NotYet}{Trimmed(why)}. {Tail}", values);
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
    /// <param name="names">The owner's names by id, read as the wait is said (ACCT2b); null says the id.</param>
    public static string Waits(CoolingEntry cooling, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null) =>
        Waiting(cooling, zone, names).Note;

    /// <summary>
    /// <see cref="Waits"/> by code (CONVNOTE2g), its English the same to the byte: the account by its owner and its id, never by
    /// the name the person gave it, which only the English says (D152's UX7d-1 note: a conversation's events never leave the
    /// machine); until when as a moment the page writes in the reader's zone; why by a cool-off's reason. The tool's own
    /// sign-in is a line of its own, and its refresh another.
    /// </summary>
    /// <param name="lead">What leads the first line's English, part of its text: a conversation's line leads with <see cref="NotYet"/>.</param>
    public static Noted Waiting(
        CoolingEntry cooling, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null, string lead = "")
    {
        var said = $"{lead}{CoolingWords.Who(cooling, names)} is cooling until {CoolingWords.When(cooling.Until, zone)}, "
                   + $"{CoolingWords.Why(cooling)}, and its conversation is on that account, so your words wait to go on in it then.";
        (string Name, object? Value)[] facts =
            [("owner", cooling.Agent), ("until", NoteCodes.Moment(cooling.Until)), ("why", CoolingWhy.Of(cooling))];
        if (cooling.Account is { } account) return Noted.Of(NoteCodes.WaitsAccount, said, [("account", account), .. facts]);

        // UX6e2: the refresh is the Agents place's Read again, since opening it asks nothing (D150 §5.3).
        return Noted.Of(NoteCodes.WaitsOwn, said, facts).Then(" ", Noted.Of(
            NoteCodes.WaitsReadAgain,
            "If you have signed in to another account at your own terminal since, press Read again under Agents → the agent's page → Accounts."));
    }

    /// <summary>
    /// The hold where no account a line may run on is ready (<c>held.limit-waits</c>, D206 §3.3 step 3; LIMITSWITCH1 for words that wait): the
    /// earliest moment one is ready again, the cool-off's <c>until</c> with its margin, and whose account, by the person's name in the English
    /// and by its id in the values. The loop's <c>LimitResume.Waits</c> is its name there.
    /// </summary>
    /// <param name="lead">What leads the English, part of its text: a record's words waiting lead with <see cref="NotYet"/>.</param>
    public static Noted LimitWaits(CoolingEntry earliest, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null, string lead = "")
    {
        var said = $"{lead}Every account it may run on is at its limit: it goes on by itself once {CoolingWords.Who(earliest, names)} is ready again "
                   + $"at {CoolingWords.When(earliest.Until, zone)}.";
        (string Name, object? Value)[] facts = [("owner", earliest.Agent), ("until", NoteCodes.Moment(earliest.Until))];
        return earliest.Account is { } account
            ? Noted.Of(NoteCodes.HeldLimitWaits, said, [("account", account), .. facts])
            : Noted.Of(NoteCodes.HeldLimitWaitsOwn, said, facts);
    }

    /// <summary>
    /// The conversation's line while its words wait for its account (CONVNOTE2g, MSG1g): <see cref="NotYet"/>, why, and the door
    /// out of the wait, each a line by code and the English as it was always written, which the sessions listing and the driver
    /// read by its prefix and whole. A wait nothing coded keeps its English with no parts, never half of it worded (D152's UX7d-1
    /// note).
    /// </summary>
    public static Noted NotYetOn(ResumeChoice resume, Noted door) =>
        resume.Waiting is { } waiting
            ? waiting.Then(" ", door)
            : new($"{NotYet}{resume.Selection.Refusal} {door.Note}", []);

    /// <summary>
    /// How a conversation's line begins where something holds the person's words rather than refusing them (MSG1c, MSG1g):
    /// the chat runner's and the driver's. The sessions listing reads a chat's last line by it for what holds its words
    /// (MSG1f3), so the writers and the reader share this one spelling.
    /// </summary>
    public const string NotYet = "— it does not go on yet: ";

    /// <summary>The door out of the wait where a new session carries the words on: a taken or open quest's session (D137 §2.2).</summary>
    public static string NewSessionDoor(string session) =>
        $"To go on now in a new session instead, without that conversation: `daoris-driver sessions go-on-new {session}`.";

    /// <summary><see cref="NewSessionDoor"/> by code (CONVNOTE2g): the session a value, the command the catalogue's.</summary>
    public static Noted NewSessionDoorOf(string session) =>
        Noted.Of(NoteCodes.WaitsDoorNewSession, NewSessionDoor(session), ("session", session));

    /// <summary>
    /// The door out of the wait where nothing carries the words on by itself: a conversation, or a closed quest's session. The
    /// terminal's *Start a conversation with these words* (MSG1f3), which takes them off the session they were said to.
    /// </summary>
    public static string ChatDoor(string session) =>
        $"To start a conversation with these words now instead, without that one: `daoris-driver sessions start-from {session}`.";

    /// <summary><see cref="ChatDoor"/> by code (CONVNOTE2g): the session a value, the command the catalogue's.</summary>
    public static Noted ChatDoorOf(string session) => Noted.Of(NoteCodes.WaitsDoorChat, ChatDoor(session), ("session", session));

    /// <summary>
    /// The door out of the wait for Ask Daoris's own conversation (ASKHIST1): no repository holds it, so a new conversation starts
    /// from its words in Ask Daoris's history, handed its transcript.
    /// </summary>
    public const string HelpDoor =
        "To go on now instead, start a new conversation from this one in Ask Daoris's history: it is handed this one's words.";

    /// <summary><see cref="HelpDoor"/> by code (CONVNOTE2g).</summary>
    public static Noted HelpDoorOf() => Noted.Of(NoteCodes.WaitsDoorHelp, HelpDoor);

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

    /// <summary>The own account's reading does not offer the asked model (D205 §3.5(a)), naming neither, since the note travels.</summary>
    public static Noted NotOfferedLine => Noted.Of(NoteCodes.AccountResumeNotOffered, "That account does not offer the model this work asked for.");
}
