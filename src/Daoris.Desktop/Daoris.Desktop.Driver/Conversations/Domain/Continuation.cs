namespace Daoris.Driver;

/// <summary>
/// Why a record's words did not go on in its own conversation (ANSWER1a, D131 §2; MSG1b, D137 §2.2): a code and the note's
/// one line.
/// </summary>
/// <param name="Code">An identifier from <see cref="ContinueWhy"/>: what the machine log writes as <c>why</c>.</param>
/// <param name="Sentence">The note's line. It travels, so it never names an account nor quotes the agent (D131 §2).</param>
public sealed record ContinueReason(string Code, string Sentence)
{
    /// <summary>
    /// The facts its line names, as the page words it (LANG1a, D142): an adapter's change names both adapters, and a door
    /// without resume names its adapter. None for a reason whose line is fixed.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> Values { get; init; } = [];

    /// <summary>
    /// A coded line said after the reason's own wherever it is said (MSG1g): for <c>account</c>, why the record's own account
    /// could not carry the words (<see cref="ContinueWhy.AccountHeld"/>). Null for a reason that says all in its line.
    /// </summary>
    public Noted? Detail { get; init; }

    /// <summary>The reason's line, then its detail after a space, as one sentence ends and the next begins.</summary>
    public string Said => Detail is { } detail ? $"{Sentence}. {detail.Note.TrimEnd('.')}" : Sentence;

    /// <summary>
    /// A row of D137 §2.2 that never goes on (MSG1b): a teammate's record, an intake, a stand-down. Nothing carries the words
    /// on, whatever the quest, where every other reason carries them to a new session or leaves them waiting.
    /// </summary>
    public bool Never => Code is ContinueWhy.Teammate or ContinueWhy.Intake or ContinueWhy.StoodDown;

    /// <summary>
    /// Whether a driven record whose resume met this waits for the person rather than carrying on by itself (SESSCTX1c, D189's
    /// amendment of 2026-10-20): another client holding its conversation (<see cref="ContinueWhy.Elsewhere"/>) alone, since a second
    /// writer in its tree could redo its work. Every other reason carries a driven record on in a new part, `from` why <c>retry</c>,
    /// as D131 point 3 always did; a driven session runs without a person by design.
    /// </summary>
    public bool Holds => Code is ContinueWhy.Elsewhere;

    /// <summary>
    /// Whether this proves the conversation lost (SESSCTX1c part B, D189 point 3): the protocol door's <c>resource_not_found</c>
    /// (<see cref="ContinueWhy.Gone"/>), no id kept, a door that cannot resume, or an agent that offers no resume. A conversation and a
    /// closed quest's session go on by themselves in a new part handed the brief on these alone; every other reason waits for the
    /// person, since nothing else proves the conversation its agent could still go on with is gone.
    /// </summary>
    public bool Lost => Code is ContinueWhy.Gone or ContinueWhy.Unkept or ContinueWhy.Unable or ContinueWhy.Offered;
}

/// <summary>
/// The reasons the person's words do not go on in their record's own conversation (ANSWER1a, D131 §2; MSG1b, D137 §2.2),
/// each a code the machine log writes and a line the notes say. The three nevers come first, then the six judged before a
/// spawn, then the wire's.
/// </summary>
public static class ContinueWhy
{
    /// <summary>
    /// The start runs on another account than the record did: its own cannot run there, or the person chose a new session
    /// while it cooled (MSG1g, with why as its <see cref="ContinueReason.Detail"/>); or a selection nobody asked for it made.
    /// </summary>
    public const string Account = "account";

    /// <summary>The person changed the adapter since.</summary>
    public const string Adapter = "adapter";

    /// <summary>No conversation id was kept: a park from before this build, or a wire that never said one.</summary>
    public const string Unkept = "unkept";

    /// <summary>Its tree is gone.</summary>
    public const string Tree = "tree";

    /// <summary>The adapter has no resume on its door.</summary>
    public const string Unable = "unable";

    /// <summary>The agent advertised neither <c>session/resume</c> nor <c>loadSession</c>.</summary>
    public const string Offered = "offered";

    /// <summary>
    /// The agent no longer has the conversation: the protocol door's <c>resource_not_found</c>. A note written before SESSCTX1c may
    /// say it of the native door's failed exit too, which is <see cref="Exited"/> since, and reads as it always did.
    /// </summary>
    public const string Gone = "gone";

    /// <summary>
    /// The native door's harness ended on a failed exit before it named its conversation (SESSCTX1c, D189's amendment), once more
    /// after the one retry as the same resume: a crash, a failed sign-in or a flag it rejected ends it the same way, so it proves
    /// nothing of the conversation's loss, apart from <see cref="Gone"/>.
    /// </summary>
    public const string Exited = "exited";

    /// <summary>
    /// The agent refused the resume on its wire otherwise. A note written before SESSCTX1c may say it of an adapter no longer here or
    /// of a resumed run that could not start, which a driven record holds for and fails as a start since (D189's amendment).
    /// </summary>
    public const string Refused = "refused";

    /// <summary>
    /// No words wait on the record: an answer a service from before ANSWER1b took had already ended it, or an ended record
    /// the person never wrote to (MSG1b).
    /// </summary>
    public const string Ended = "ended";

    /// <summary>
    /// The agent refused because another of its clients holds the conversation (MSG1b, D137 §2.2): <c>codex-acp</c>'s
    /// <c>data.reason: "thread_active_writer"</c>, read from that structured data and never from its sentence.
    /// </summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>A teammate's record (D47 §6): its process and its conversation are on their machine and account. A never.</summary>
    public const string Teammate = "teammate";

    /// <summary>An intake, answered through its ask (INT4h). A never.</summary>
    public const string Intake = "intake";

    /// <summary>A session that stood down: its quest is someone else's, so it has nothing to go on with. A never.</summary>
    public const string StoodDown = "stood-down";

    /// <summary>
    /// The person started a conversation with them (MSG1f2): *Start a conversation with these words*, which takes them off
    /// the record naming that conversation. Never judged: only that press says it, on the went note it leaves.
    /// </summary>
    public const string Started = "started";

    /// <summary>
    /// The quest of the session that asked another repository and waited (D79) resumed in a new session once the answer came, handed
    /// the words said to the asker meanwhile (SAYHELD1): its own conversation was never handed the answer. Never judged: only that
    /// resume says it, on the went note it leaves.
    /// </summary>
    public const string Resumed = "resumed";

    /// <summary>A reason whose line is fixed: every code but <see cref="Adapter"/> and <see cref="Unable"/>, which name adapters.</summary>
    public static ContinueReason Of(string code) => new(code, code switch
    {
        Account => "its conversation stays with the account it ran on, and this start runs on another",
        Unkept => "Daoris kept no id for its conversation",
        Tree => "its tree is gone",
        Offered => "the agent offers no way to resume a conversation",
        Gone => "the agent no longer has its conversation",
        Exited => "its agent ended before it reopened its conversation",
        Refused => "its conversation could not be resumed",
        Ended => "its record had already ended",
        // Names no client: which one holds it is the agent's to know, and the note travels (D131 §2).
        Elsewhere => "its conversation is open in another client of its agent",
        // Names no machine: the record's origin is a teammate's, and the note travels.
        Teammate => "it ran on another machine, where its conversation is",
        Intake => "an intake is answered through its ask",
        StoodDown => "it stood down, so it has nothing to go on with",
        Started => "you started a conversation with them",
        Resumed => "its quest resumed in a new session once the question it waited on was answered",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "a reason whose line names an adapter is made by its own method"),
    });

    /// <summary>The adapter changed between the park and the answer.</summary>
    public static ContinueReason AdapterChanged(string ranOn, string startsOn) =>
        new(Adapter, $"it ran on `{ranOn}`, and starts here now run on `{startsOn}`") { Values = [("from", ranOn), ("to", startsOn)] };

    /// <summary>The adapter's door has no resume.</summary>
    public static ContinueReason CannotResume(string adapter) =>
        new(Unable, $"`{adapter}` cannot resume a conversation") { Values = [("adapter", adapter)] };

    /// <summary>
    /// <see cref="Account"/>, where the resume asked for the record's own account and it could not carry the words (MSG1g, D137
    /// §2.2): its line, then why, as a coded line of its own (<see cref="ResumeWords.Line"/>). The code stays <c>account</c>, so
    /// the log and every reader that words the reason read it as before.
    /// </summary>
    /// <param name="hold">What held the record's own account, by TOOL6e's codes.</param>
    /// <param name="chosen">The person chose a new session while it cooled.</param>
    public static ContinueReason AccountHeld(NextHold hold, bool chosen = false) =>
        Of(Account) with { Detail = ResumeWords.Line(hold, chosen) };
}

/// <summary>
/// Whether a record the person's words wait on resumes its own harness conversation (ANSWER1a, D131 §1–§2; MSG1b, D137
/// §2.2), parked or ended, judged before anything is spawned. Pure but for one look at whether the record's tree still
/// stands.
/// </summary>
/// <remarks>
/// <para><b>The same account, always.</b> The harness keeps a conversation in the configuration home of the account it
/// ran on, so another account would not find it, and a record names one account (D125 §7). So a resume asks for that
/// account (MSG1g, <see cref="HarnessRoster.ResumeAsync"/>): a cool-off holds the words for its reset, and an account that
/// cannot run there at all, or the person's choice of a new session, lands the start on another account, which is a
/// carry-on, said <c>account</c> with why.</para>
///
/// <para><b>What never goes on is said first</b> (D137 §2.2): a teammate's record, an intake and a stand-down. <b>Then the
/// record, then what the start runs as, then what is kept, then the door</b>, so the line said is the one a person can act
/// on first: an ended record before an account, an account before an id.</para>
/// </remarks>
public static class Continuations
{
    /// <summary>Null where the record resumes; else the first reason it does not.</summary>
    /// <param name="park">The record as the last run read it: its state, words, adapter, account and tree.</param>
    /// <param name="adapter">The adapter this machine's starts ride now.</param>
    /// <param name="doorResumes">
    /// Whether that adapter's door can resume at all: the protocol door's agent decides on its wire, so true for it;
    /// the native door only where its adapter knows the harness's own resume (<see cref="ISessionAdapter.Resumes"/>).
    /// </param>
    /// <param name="profile">The account the start would run on, as the selection resolved it; null for the tool's own home.</param>
    /// <param name="kept">The conversation id kept for the park, or null.</param>
    /// <param name="account">
    /// Why the record's own account cannot carry the words, where the resume asked for it and the selection said (MSG1g,
    /// <see cref="ResumeChoice.Elsewhere"/>); null where it can, or nobody asked. Said at the account's step, so an ended record
    /// and a changed adapter are still said first.
    /// </param>
    /// <param name="treeStands">
    /// Whether the tree the record ran in is still there, asked only at the tree's step: the disk's answer, handed in, so the
    /// judgement stays pure (STRUCT3h, D166's note). <c>Continuations.Judge</c> without it (<c>ContinuationJudge</c>) asks the disk.
    /// </param>
    public static ContinueReason? Judge(
        Func<string, bool> treeStands, PriorSession park, string adapter, bool doorResumes, string? profile, HarnessConversation? kept,
        ContinueReason? account = null)
    {
        // What never goes on (D137 §2.2), before anything else: the service refuses words to each, so only a record read
        // some other way reaches here, and none is ever this machine's to resume.
        if (park.Teammate) return ContinueWhy.Of(ContinueWhy.Teammate);
        if (park.Ask is { Length: > 0 }) return ContinueWhy.Of(ContinueWhy.Intake);
        if (string.Equals(park.State, "stood-down", StringComparison.OrdinalIgnoreCase)) return ContinueWhy.Of(ContinueWhy.StoodDown);

        if (!park.WordsWaiting) return ContinueWhy.Of(ContinueWhy.Ended);

        var ranOn = park.Adapter ?? kept?.Adapter;
        if (ranOn is { Length: > 0 } && !string.Equals(ranOn, adapter, StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.AdapterChanged(ranOn, adapter);
        }

        if (account is not null) return account;
        if (!string.Equals(park.Profile ?? "", profile ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.Of(ContinueWhy.Account);
        }

        if (park.Tree is not { Length: > 0 } tree || !treeStands(tree)) return ContinueWhy.Of(ContinueWhy.Tree);

        if (kept is null || !string.Equals(kept.Adapter, adapter, StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.Of(ContinueWhy.Unkept);
        }

        return doorResumes ? null : ContinueWhy.CannotResume(adapter);
    }

    /// <summary>What the record says while its resumed conversation runs: the conclusion's note replaces it.</summary>
    public const string Working = "resumes its own conversation with your answer, in the tree it worked in.";

    /// <summary><see cref="Working"/> with its code (LANG1a).</summary>
    public static Noted WorkingNoted => Noted.Of(NoteCodes.WorkingResumesAnswer, Working);

    /// <summary>The authors a word can have but the owner, in the order every sentence names them (SUPERVISOR1a-i, the design §6.1).</summary>
    public static readonly IReadOnlyList<(string Code, string Said)> Authors =
    [
        ("supervisor", "the supervisor's answer"), ("helper", "a helper's answer"), ("opinion", "another agent's findings"),
        ("daoris", "Daoris's words"),
    ];

    /// <summary>
    /// The authors among <paramref name="words"/> that are not the owner, by code in <see cref="Authors"/>' order: the supervisor's
    /// answer (a word the look let through, so one its memory authored), a helper's, an opinion's findings, Daoris's.
    /// </summary>
    public static IReadOnlyList<string> AuthorsOf(IEnumerable<SaidWordView> words)
    {
        var listed = words.ToList();
        return [.. Authors.Select(author => author.Code).Where(code => code switch
        {
            "supervisor" => listed.Any(word => word.Supervisors),
            "helper" => listed.Any(word => word.Helper is not null),
            "opinion" => listed.Any(word => word.By is not null),
            _ => listed.Any(word => word.Daoris),
        })];
    }

    /// <summary>The authors said in English, joined: "the supervisor's answer and a helper's answer".</summary>
    public static string AuthorsSaid(IReadOnlyList<string> codes)
    {
        var said = codes.Select(code => Authors.First(author => author.Code == code).Said).ToList();
        return said.Count <= 1 ? string.Concat(said) : string.Join(", ", said[..^1]) + " and " + said[^1];
    }

    /// <summary>
    /// What a park's record says while its conversation resumes with none of the owner's words (SUPERVISOR1a-i, D208; the supervisor
    /// design §6.1): the authors that are there, never "your answer", which they are not. A helper-only or Daoris-only park says so too.
    /// </summary>
    public static Noted WorkingWithOthersNoted(IReadOnlyList<string> authors) => Noted.Of(
        NoteCodes.WorkingResumesOthers, $"resumes its own conversation with {AuthorsSaid(authors)}, in the tree it worked in.",
        ("authors", authors));

    /// <summary>
    /// What an ended record's note gains as it goes on (MSG1b, D137 §2.3), after the service's own line saying when: the
    /// conclusion's note replaces the whole note, keeping what ended it only where the conclusion says so.
    /// </summary>
    public const string GoingOn = "It goes on with your words in its own conversation, in the tree it worked in.";

    /// <summary>
    /// The words a run that goes on is handed (SAIDBACK1d, D137 §2.4): the record's as it stands once the ledger's move is made,
    /// which no take-back can change after (the door refuses a record that runs), never those the look read, since a word
    /// taken back or reworded between the two must not go. The look's only where the record could not be read again.
    /// </summary>
    internal static IReadOnlyList<SaidWordView> HandedAfterMove(IReadOnlyList<SaidWordView> look, PriorSession? now) =>
        now is null ? look : now.Waiting;

    /// <summary>
    /// The words, each marked with the answer of its park's question it was pressed as (PARKVIEW1; Fable's read, Q4), by its id among
    /// <paramref name="choices"/> (<see cref="SessionEvents.ChoicesOf"/>): so it is handed as that answer chosen
    /// (<see cref="SaidWordView.Handed"/>) and its record says so again where the session takes it. A word with none stays as it is.
    /// </summary>
    internal static IReadOnlyList<SaidWordView> Marked(IReadOnlyList<SaidWordView> words, IReadOnlyDictionary<string, string> choices) =>
        choices.Count == 0
            ? words
            : [.. words.Select(word => word.Id is { } id && choices.TryGetValue(id, out var choice) ? word with { Choice = choice } : word)];

    /// <summary>
    /// Every list of words a go-on hands, filtered (SUPERVISOR1a-i, D208; the supervisor design §6.1, §6.3): a word claiming the
    /// supervisor is kept only where it is <paramref name="ticket"/>'s, the one the admission drew; every other such word, forged,
    /// arrived after planning or not admitted, is never handed. Every other word stays as it is.
    /// </summary>
    internal static IReadOnlyList<SaidWordView> Ticketed(IReadOnlyList<SaidWordView> words, SupervisorTicket? ticket) =>
        words.Any(word => word.Supervisors && (ticket is null || word.Id != ticket.Word))
            ? [.. words.Where(word => !word.Supervisors || (ticket is not null && word.Id == ticket.Word))]
            : words;

    /// <summary>
    /// The record a fallback hands its words on from (SAIDBACK1f, D137 §2.4): the look's, with the words it holds now, so one
    /// taken back or reworded since the look never goes to the new session. A park's answer is no said word, and the look's
    /// record stands where it could not be read again.
    /// </summary>
    internal static PriorSession HandedOn(PriorSession look, PriorSession? now) =>
        now is null || look.Parked ? look : look with { Said = now.Said ?? [] };

    /// <summary>
    /// What a resumed conversation carries after the words (<see cref="ResumeAsk"/>'s appendix): what its start did with its tree
    /// against the line first (LANDSYNC2, LANDSYNC2b; D87's 2026-10-15 amendment point 7), since that is where its next commit goes,
    /// then anything else the resume carries. A driven session's words going on and a conversation's own both say it so.
    /// </summary>
    /// <param name="inSync">What <c>StartInSyncAsync</c> told; null or empty for nothing.</param>
    /// <param name="after">What else the resume carries after the words; null or empty for nothing.</param>
    internal static string? AfterWords(string? inSync, string? after) => inSync is { Length: > 0 } told
        ? after is { Length: > 0 } more ? $"{told}\n\n{more}" : told
        : after;

    /// <summary>
    /// What a session going on in its own conversation is told of its go-aheads (AUTONOMY1c, D184 points 3 and 5): where its
    /// repository's setup as it stands now asks other go-aheads than the one <paramref name="record"/> started under, the change in
    /// a paragraph; nothing where they are the same. The conversation is not handed its instruction again, so without this a person
    /// who set go-aheads back to <c>named</c> would find it still asking none. A record from before the setup was told today's.
    /// </summary>
    internal static string? GoAheadsChanged(PriorSession record, ResolvedAccess now)
    {
        var was = record.Access?.GetValueOrDefault(AccessRules.GoAheads) ?? AccessRules.Pause(AccessRules.GoAheads).Default;
        var goAheads = now.Value(AccessRules.GoAheads);
        if (string.Equals(was, goAheads, StringComparison.Ordinal)) return null;
        return goAheads == AccessRules.Never
            ? "This repository's access setup has changed since you started: it now asks no go-ahead but what the quest or the "
              + "person's words demand. Where the work needs an act on a production system or outside this repository, do it without "
              + "asking and name it under **Readings** in your closing note; a push, a publish or a release stays as your instruction's "
              + "boundary says."
            : "This repository's access setup has changed since you started: ask the person a go-ahead again before an act outside "
              + "this repository or on a production system, or for one this repository or the quest says needs it.";
    }

    /// <summary>
    /// Where a quest's record goes back to when the driver moved it to working and every word was taken back before any run of it
    /// started (SAIDBACK1e, D137 §2.4), or the driver closed first (PARALLEL1f). An ended record goes back to how it ended, its note,
    /// its parts and its flags as the look read them; a park waits on the person again. A run that could not start is
    /// <see cref="Unstarted"/>'s (SESSCTX1c).
    /// </summary>
    internal static (string State, Noted Note, bool Interrupted, bool Limit) PutBack(PriorSession record)
    {
        if (record.Parked) return (record.State, record.AsNoted(), false, false);

        // Each flag only where the ledger takes it, on the state that says it.
        var stopped = string.Equals(record.State, "stopped", StringComparison.OrdinalIgnoreCase);
        var failed = string.Equals(record.State, "failed", StringComparison.OrdinalIgnoreCase);
        return (record.State, new Noted(record.Note ?? "", record.NoteParts ?? []), record.Interrupted && stopped, record.Limit && failed);
    }

    /// <summary>
    /// How a record ends whose resumed run could not start (SESSCTX1c, D189's amendment; the session-context design §2): a start
    /// failure, never a move. It ends <c>failed</c> with the spawn's own words, as a first start's failed spawn does, so the strikes
    /// count it (DRV6), and the words the move to working left on it wait to go again, said so. A park ends so too, since moved back
    /// to waiting on the person the ledger would clear its words (a park asks anew). Where its note already said its words wait,
    /// they failed twice running and wait for the person, as a failed turn's do (TURNFAIL1, <see cref="FailedTurnWords"/>).
    /// </summary>
    /// <param name="failure">Why its process did not start, in the program's own words.</param>
    internal static (Noted Note, WordsAfter Words) Unstarted(PriorSession record, string failure)
    {
        var words = record.NoteParts?.Any(part => part.Code == NoteCodes.EndedWordsWait.Code || part.Code == NoteCodes.EndedWordsWaitYou.Code) == true
            ? WordsAfter.Held
            : WordsAfter.Wait;
        return (Observation.Failure(failure).Then("\n\n", FailedTurnWords.Line(words)!), words);
    }

    /// <summary><see cref="GoingOn"/> with its code (LANG1a).</summary>
    public static Noted GoingOnNoted => Noted.Of(NoteCodes.WorkingGoesOn, GoingOn);

    /// <summary>
    /// What an ended record's note gains as it goes on with another agent's findings alone (XAGENT1e, D155 point 7): never "your
    /// words", which they are not.
    /// </summary>
    public const string GoingOnWithFindings =
        "It goes on with another agent's findings in its own conversation, in the tree it worked in, to check and answer each.";

    /// <summary>
    /// <see cref="GoingOnWithFindings"/> as its note's line: its English whole, with no code until the page words one (XAGENT1g),
    /// since a code the page's catalogues do not hold would be shown as a key.
    /// </summary>
    public static Noted GoingOnWithFindingsNoted => Noted.Said(GoingOnWithFindings, NoteBy.Before);

    /// <summary>
    /// The resumed run's first line in its record (D131 §1): the door it resumed on, and the harness's version where it
    /// moved since the record opened, since the record names the version it opened on and a newer one is never refused.
    /// </summary>
    /// <param name="answer">A park's answer; false for words said to a record that had ended (MSG1b), which are no answer.</param>
    /// <param name="findings">
    /// Whether another agent's findings are among them (XAGENT1e, D155 point 7): said as that agent's, never as the person's.
    /// </param>
    /// <param name="persons">Whether any of them are the person's words; with no findings among them, they all are.</param>
    public static string Opening(string adapter, string? now, string? then, bool answer = true, bool findings = false, bool persons = true) =>
        OpeningNoted(adapter, now, then, answer, findings, persons).Note;

    /// <summary>
    /// What an ended record's note gains as it goes on with a helper's answer alone (MIXAGENT2a-ii): never "your words", which
    /// they are not.
    /// </summary>
    public const string GoingOnWithHelper = "It goes on with a helper's answer in its own conversation, in the tree it worked in, to check against the code.";

    /// <summary><see cref="GoingOnWithHelper"/> as its note's line, uncoded as the findings' twin is until the page words one.</summary>
    public static Noted GoingOnWithHelperNoted => Noted.Said(GoingOnWithHelper, NoteBy.Before);

    /// <summary>
    /// What an ended record's note gains as it goes on with Daoris's own words alone (COWORK1b): a reopen told, a step's go-on;
    /// never "your words", which they are not.
    /// </summary>
    public const string GoingOnWithDaoris = "It goes on with Daoris's words in its own conversation, in the tree it worked in.";

    /// <summary><see cref="GoingOnWithDaoris"/> as its note's line, uncoded as the helper's twin is until the page words one.</summary>
    public static Noted GoingOnWithDaorisNoted => Noted.Said(GoingOnWithDaoris, NoteBy.Before);

    /// <summary>
    /// <see cref="Opening"/> with its lines by code (CONVNOTE2a, D152's CONVNOTE2a note): the adapter and who spoke as the
    /// lead's facts, and a version that moved as a part riding inside it, both versions facts. The English is byte for byte
    /// what <see cref="Opening"/> always was.
    /// </summary>
    /// <param name="helper">
    /// Whether a helper's answer is among them (MIXAGENT2a-ii): said as that agent's claim. Where findings are too, the findings'
    /// reason stands, as the ledger's line says them.
    /// </param>
    /// <param name="daoris">
    /// Whether Daoris's own words are among them (COWORK1b): alone, said as Daoris's; beside anyone else's, the other's reason stands,
    /// as the ledger's line says them.
    /// </param>
    /// <param name="supervisor">
    /// Whether the supervisor's answer is among them (SUPERVISOR1a-i): with none of the owner's words or findings, said as the
    /// supervisor's, never "your answer".
    /// </param>
    public static Noted OpeningNoted(
        string adapter, string? now, string? then, bool answer = true, bool findings = false, bool persons = true, bool helper = false,
        bool daoris = false, bool supervisor = false)
    {
        var (said, why) = (findings, persons, helper) switch
        {
            (false, false, _) when supervisor => ("the supervisor's answer is", ResumedWhy.Supervisor),
            (true, false, _) => ("another agent's findings are", ResumedWhy.Findings),
            (true, true, _) => answer
                ? ("your answer and another agent's findings are", ResumedWhy.AnswerAndFindings)
                : ("your words and another agent's findings are", ResumedWhy.WordsAndFindings),
            (false, false, true) => ("a helper's answer is", ResumedWhy.Helper),
            (false, true, true) when !answer => ("your words and a helper's answer are", ResumedWhy.WordsAndHelper),
            (false, false, false) when daoris => ("Daoris's words are", ResumedWhy.Daoris),
            _ => answer ? ("your answer is", ResumedWhy.Answer) : ("your words are", ResumedWhy.Words),
        };
        var lead = $"— {said} the next turn of its own conversation, resumed on `{adapter}`";
        if (now is not { Length: > 0 } || then is not { Length: > 0 } || string.Equals(now, then, StringComparison.Ordinal))
        {
            return Noted.Of(NoteCodes.ResumedOpening, lead + ".", ("adapter", adapter), ("why", why));
        }

        // The lead's text stops where the English goes on, so each part's text is inside the note.
        var moved = $"{now}; it opened on {then}";
        return new Noted(
            $"{lead} {moved}.",
            [NoteCodes.ResumedOpening.Part(lead, ("adapter", adapter), ("why", why)), NoteCodes.ResumedMoved.Part(moved, ("now", now), ("then", then))]);
    }

    /// <summary>
    /// The note an ended record keeps where its words cannot go on in it and nothing carries them on by itself (MSG1b, D137
    /// §2.2): a closed quest's, or a never. What ended it stays, then why; the words stay waiting as they were said.
    /// </summary>
    /// <remarks>Its line is a code after the record's earlier parts (LANG1a), the reason a value the page words.</remarks>
    public static Noted CannotNote(PriorSession record, ContinueReason why) =>
        After(record, Detailed(Noted.Of(
            NoteCodes.WentCannot, $"It cannot go on in this session, because {why.Sentence}.", NoteCodes.Reason(why, record.Adapter)), why));

    /// <summary>
    /// The note an ended record keeps where its words went to a new session (MSG1b, D137 §2.2): a taken quest carried on, or
    /// an open one started, handed them. What ended it stays, then why.
    /// </summary>
    public static Noted WentNote(PriorSession record, ContinueReason why) =>
        After(record, Detailed(Noted.Of(
            NoteCodes.WentNewSession, $"Your words went to a new session, because {why.Sentence}.", NoteCodes.Reason(why, record.Adapter)), why));

    /// <summary>
    /// What a record's conversation says where the agent its words would go on with is no longer one this machine has (SESSCTX1c,
    /// D189's amendment; the session-context design §2): they wait on it, and the hand-over is the way on, since only the person can
    /// choose the agent. The command is the terminal's; its agent is the person's to name.
    /// </summary>
    public static Noted AgentGone(string agent, string session) =>
        Noted.Of(
            NoteCodes.HeldAgentGone,
            $"It does not go on yet: `{agent}` is no longer an agent on this machine, so its words cannot go on with it. They wait on "
            + $"it: hand it over to another agent to go on with them, `daoris-driver sessions hand-over {session} --agent <agent>`.",
            ("agent", agent), ("session", session));

    /// <summary>The record's note, then a blank line and the new line; the line alone where the record said nothing.</summary>
    private static Noted After(PriorSession record, Noted line) =>
        record.Note is { Length: > 0 } ? record.AsNoted().Then("\n\n", line) : line;

    /// <summary>A line whose reason is <paramref name="why"/>, then the reason's detail as a coded line of its own (MSG1g).</summary>
    private static Noted Detailed(Noted line, ContinueReason why) => why.Detail is { } detail ? line.Then(" ", detail) : line;

    /// <summary>
    /// <c>session.reopened</c> (D137 §3.3, D94 §4): once per ended record whose words the driver takes up, from which state,
    /// whether its own conversation resumed and, where not, why by code. Never the words or an account. A park's answer stays
    /// <see cref="Answered"/>'s line. Written on the account lines' channel, which writes any catalogued line as it is given.
    /// </summary>
    /// <param name="from">The ended state it went on from, in the record's spelling.</param>
    /// <param name="why">Null where it resumed.</param>
    /// <param name="door">Where the words were said, <c>screen</c> or <c>terminal</c> (MSG1d); null where no door was kept.</param>
    /// <param name="kind">The record's kind: <c>driven</c>, or <c>chat</c> for an ended chat the person wrote to (MSG1c).</param>
    public static AccountLine Reopened(
        string session, string adapter, string from, ContinueReason? why, string? door = null, string kind = "driven") =>
        new("session.reopened",
        [
            ("session", session), ("kind", kind), ("adapter", adapter), ("from", from), ("resumed", why is null), ("why", why?.Code),
            ("door", door),
        ]);

    /// <summary>
    /// The driver's note in a record whose person's words went to a new session (MSG1b, D137 §3.1): its line, then the words'
    /// ids, that session and the reason's code (MSG1d), so the page links the session and words the reason itself.
    /// </summary>
    /// <param name="words">The ids of the words handed on; none for an answer a host from before <c>said</c> kept.</param>
    public static SessionEvent Went(IReadOnlyList<string> words, string to, ContinueReason why) => new()
    {
        Kind = SessionEventKind.Note,
        Text = $"— your words went to session `{to}`, because {why.Said}.",
        Words = words.Count > 0 ? words : null,
        To = to,
        Why = why.Code,
    };

    /// <summary>
    /// The driver's note in a record whose person's words cannot go on in it and that nothing carries on (MSG1b, D137 §2.2):
    /// its line, then the words' ids and the reason's code (MSG1d), which the page words as <c>work.say.cannot</c>.
    /// </summary>
    public static SessionEvent Cannot(IReadOnlyList<string> words, ContinueReason why) => new()
    {
        Kind = SessionEventKind.Note,
        Text = $"— It cannot go on in this session, because {why.Said}.",
        Words = words.Count > 0 ? words : null,
        Why = why.Code,
    };

    /// <summary>The note a fallback ends the park with: what it asked, as its record said, and why the answer went to a new session.</summary>
    public static Noted EndedNote(PriorSession park, ContinueReason why) =>
        (park.Note is null ? Noted.Of(NoteCodes.EndedParkedShort, "It stopped to ask you.") : park.AsNoted())
        .Then("\n\n", Detailed(Noted.Of(
            NoteCodes.WentCarriedOn, $"Carried on in a new session, because {why.Sentence}.", NoteCodes.Reason(why, park.Adapter)), why));

    /// <summary>The sentence the carrying-on session's note ends with (D131 §2), after a space.</summary>
    /// <param name="ranOn">The adapter the record it carries on ran on, which <c>elsewhere</c> names.</param>
    public static Noted CarriedOn(ContinueReason why, string? ranOn = null) =>
        Detailed(Noted.Of(NoteCodes.StartedFellBack, $"A new session, because {why.Sentence}.", NoteCodes.Reason(why, ranOn)), why);

    /// <summary>
    /// <c>session.answered</c> (D131 §2, D94 §4): once per answer the driver takes up, whether its own conversation resumed
    /// and, where not, why by code. Never the answer, the agent's words or an account. Written on the account lines'
    /// channel, which writes any catalogued line as it is given.
    /// </summary>
    /// <param name="session">The parked session the person answered.</param>
    /// <param name="why">Null where it resumed.</param>
    public static AccountLine Answered(string session, string adapter, ContinueReason? why) =>
        new("session.answered", [("session", session), ("adapter", adapter), ("resumed", why is null), ("why", why?.Code)]);

    /// <summary>
    /// <c>session.moved</c> (SESSCTX1c, D189 point 8; the session-context design §7): once per move to a new part, as
    /// <c>account.rotated</c> says a start moved off its scope's first account (D125 point 7): the new part and the one it continues,
    /// the agent each ran on, the account each by its profile name (null for the tool's own home), why by the link's code
    /// (<see cref="HandOverWhy"/>), and whether it was handed Daoris's brief. Never a word, the brief or an account's key.
    /// </summary>
    public static AccountLine Moved(
        string session, string continues, string? ranOn, string adapter, string? from, string? to, string why, bool briefed) =>
        new("session.moved",
        [
            ("session", session), ("continues", continues), ("ranOn", ranOn), ("adapter", adapter), ("from", from), ("to", to),
            ("why", why), ("briefed", briefed),
        ]);
}
