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
    /// A row of D137 §2.2 that never goes on (MSG1b): a teammate's record, an intake, a stand-down. Nothing carries the words
    /// on, whatever the quest, where every other reason carries them to a new session or leaves them waiting.
    /// </summary>
    public bool Never => Code is ContinueWhy.Teammate or ContinueWhy.Intake or ContinueWhy.StoodDown;
}

/// <summary>
/// The reasons the person's words do not go on in their record's own conversation (ANSWER1a, D131 §2; MSG1b, D137 §2.2),
/// each a code the machine log writes and a line the notes say. The three nevers come first, then the six judged before a
/// spawn, then the wire's.
/// </summary>
public static class ContinueWhy
{
    /// <summary>The start runs on another account than the park did: a limit cools it, a rotation or D130's list chose another.</summary>
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

    /// <summary>The agent no longer has the conversation: <c>resource_not_found</c>, or the native door ended before opening it.</summary>
    public const string Gone = "gone";

    /// <summary>The agent refused the resume otherwise, or the resumed run could not start.</summary>
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

    /// <summary>A reason whose line is fixed: every code but <see cref="Adapter"/> and <see cref="Unable"/>, which name adapters.</summary>
    public static ContinueReason Of(string code) => new(code, code switch
    {
        Account => "its conversation stays with the account it ran on, and this start runs on another",
        Unkept => "Daoris kept no id for its conversation",
        Tree => "its tree is gone",
        Offered => "the agent offers no way to resume a conversation",
        Gone => "the agent no longer has its conversation",
        Refused => "its conversation could not be resumed",
        Ended => "its record had already ended",
        // Names no client: which one holds it is the agent's to know, and the note travels (D131 §2).
        Elsewhere => "its conversation is open in another client of its agent",
        // Names no machine: the record's origin is a teammate's, and the note travels.
        Teammate => "it ran on another machine, where its conversation is",
        Intake => "an intake is answered through its ask",
        StoodDown => "it stood down, so it has nothing to go on with",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "a reason whose line names an adapter is made by its own method"),
    });

    /// <summary>The adapter changed between the park and the answer.</summary>
    public static ContinueReason AdapterChanged(string ranOn, string startsOn) =>
        new(Adapter, $"it ran on `{ranOn}`, and starts here now run on `{startsOn}`");

    /// <summary>The adapter's door has no resume.</summary>
    public static ContinueReason CannotResume(string adapter) => new(Unable, $"`{adapter}` cannot resume a conversation");
}

/// <summary>
/// Whether a record the person's words wait on resumes its own harness conversation (ANSWER1a, D131 §1–§2; MSG1b, D137
/// §2.2), parked or ended, judged before anything is spawned. Pure but for one look at whether the record's tree still
/// stands.
/// </summary>
/// <remarks>
/// <para><b>The same account, always.</b> The harness keeps a conversation in the configuration home of the account it
/// ran on, so another account would not find it, and a record names one account (D125 §7). A limit cooling the park's
/// account, a rotation and D130's list each land a start on another account, and each is a carry-on.</para>
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
    public static ContinueReason? Judge(
        PriorSession park, string adapter, bool doorResumes, string? profile, HarnessConversation? kept)
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

        if (!string.Equals(park.Profile ?? "", profile ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.Of(ContinueWhy.Account);
        }

        if (park.Tree is not { Length: > 0 } tree || !Directory.Exists(tree)) return ContinueWhy.Of(ContinueWhy.Tree);

        if (kept is null || !string.Equals(kept.Adapter, adapter, StringComparison.OrdinalIgnoreCase))
        {
            return ContinueWhy.Of(ContinueWhy.Unkept);
        }

        return doorResumes ? null : ContinueWhy.CannotResume(adapter);
    }

    /// <summary>What the record says while its resumed conversation runs: the conclusion's note replaces it.</summary>
    public const string Working = "resumes its own conversation with your answer, in the tree it worked in.";

    /// <summary>
    /// What an ended record's note gains as it goes on (MSG1b, D137 §2.3), after the service's own line saying when: the
    /// conclusion's note replaces the whole note, keeping what ended it only where the conclusion says so.
    /// </summary>
    public const string GoingOn = "It goes on with your words in its own conversation, in the tree it worked in.";

    /// <summary>
    /// The resumed run's first line in its record (D131 §1): the door it resumed on, and the harness's version where it
    /// moved since the record opened, since the record names the version it opened on and a newer one is never refused.
    /// </summary>
    /// <param name="answer">A park's answer; false for words said to a record that had ended (MSG1b), which are no answer.</param>
    public static string Opening(string adapter, string? now, string? then, bool answer = true)
    {
        var said = answer ? "your answer is" : "your words are";
        return now is { Length: > 0 } && then is { Length: > 0 } && !string.Equals(now, then, StringComparison.Ordinal)
            ? $"— {said} the next turn of its own conversation, resumed on `{adapter}` {now}; it opened on {then}."
            : $"— {said} the next turn of its own conversation, resumed on `{adapter}`.";
    }

    /// <summary>
    /// The note an ended record keeps where its words cannot go on in it and nothing carries them on by itself (MSG1b, D137
    /// §2.2): a closed quest's, or a never. What ended it stays, then why; the words stay waiting as they were said.
    /// </summary>
    public static string CannotNote(PriorSession record, ContinueReason why) =>
        $"{Before(record)}It cannot go on in this session, because {why.Sentence}.";

    /// <summary>
    /// The note an ended record keeps where its words went to a new session (MSG1b, D137 §2.2): a taken quest carried on, or
    /// an open one started, handed them. What ended it stays, then why.
    /// </summary>
    public static string WentNote(PriorSession record, ContinueReason why) =>
        $"{Before(record)}Your words went to a new session, because {why.Sentence}.";

    private static string Before(PriorSession record) => record.Note is { Length: > 0 } note ? $"{note}\n\n" : "";

    /// <summary>
    /// <c>session.reopened</c> (D137 §3.3, D94 §4): once per ended record whose words the driver takes up, from which state,
    /// whether its own conversation resumed and, where not, why by code. Never the words or an account. A park's answer stays
    /// <see cref="Answered"/>'s line. Written on the account lines' channel, which writes any catalogued line as it is given.
    /// </summary>
    /// <param name="from">The ended state it went on from, in the record's spelling.</param>
    /// <param name="why">Null where it resumed.</param>
    public static AccountLine Reopened(string session, string adapter, string from, ContinueReason? why) =>
        new("session.reopened",
            [("session", session), ("kind", "driven"), ("adapter", adapter), ("from", from), ("resumed", why is null), ("why", why?.Code)]);

    /// <summary>The note a fallback ends the park with: what it asked, as its record said, and why the answer went to a new session.</summary>
    public static string EndedNote(PriorSession park, ContinueReason why) =>
        $"{park.Note ?? "It stopped to ask you."}\n\nCarried on in a new session, because {why.Sentence}.";

    /// <summary>The sentence the carrying-on session's note ends with (D131 §2).</summary>
    public static string CarriedOn(ContinueReason why) => $" A new session, because {why.Sentence}.";

    /// <summary>
    /// <c>session.answered</c> (D131 §2, D94 §4): once per answer the driver takes up, whether its own conversation resumed
    /// and, where not, why by code. Never the answer, the agent's words or an account. Written on the account lines'
    /// channel, which writes any catalogued line as it is given.
    /// </summary>
    /// <param name="session">The parked session the person answered.</param>
    /// <param name="why">Null where it resumed.</param>
    public static AccountLine Answered(string session, string adapter, ContinueReason? why) =>
        new("session.answered", [("session", session), ("adapter", adapter), ("resumed", why is null), ("why", why?.Code)]);
}
