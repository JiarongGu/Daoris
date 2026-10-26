namespace Daoris.Driver;

/// <summary>
/// Why a record's words did not go on in its own conversation (ANSWER1a, D131 §2; MSG1b, D137 §2.2): a code and the note's
/// one line.
/// </summary>
/// <param name="Code">An identifier from <see cref="ContinueWhy"/>: what the machine log writes as <c>why</c>.</param>
/// <param name="Sentence">The note's line. It travels, so it never names an account nor quotes the agent (D131 §2).</param>
public sealed record ContinueReason(string Code, string Sentence) : IContinueReason
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
    public const string Account = ContinueCodes.Account;

    /// <summary>The person changed the adapter since.</summary>
    public const string Adapter = ContinueCodes.Adapter;

    /// <summary>No conversation id was kept: a park from before this build, or a wire that never said one.</summary>
    public const string Unkept = ContinueCodes.Unkept;

    /// <summary>Its tree is gone.</summary>
    public const string Tree = ContinueCodes.Tree;

    /// <summary>The adapter has no resume on its door.</summary>
    public const string Unable = ContinueCodes.Unable;

    /// <summary>The agent advertised neither <c>session/resume</c> nor <c>loadSession</c>.</summary>
    public const string Offered = ContinueCodes.Offered;

    /// <summary>
    /// The agent no longer has the conversation: the protocol door's <c>resource_not_found</c>. A note written before SESSCTX1c may
    /// say it of the native door's failed exit too, which is <see cref="Exited"/> since, and reads as it always did.
    /// </summary>
    public const string Gone = ContinueCodes.Gone;

    /// <summary>
    /// The native door's harness ended on a failed exit before it named its conversation (SESSCTX1c, D189's amendment), once more
    /// after the one retry as the same resume: a crash, a failed sign-in or a flag it rejected ends it the same way, so it proves
    /// nothing of the conversation's loss, apart from <see cref="Gone"/>.
    /// </summary>
    public const string Exited = ContinueCodes.Exited;

    /// <summary>
    /// The agent refused the resume on its wire otherwise. A note written before SESSCTX1c may say it of an adapter no longer here or
    /// of a resumed run that could not start, which a driven record holds for and fails as a start since (D189's amendment).
    /// </summary>
    public const string Refused = ContinueCodes.Refused;

    /// <summary>
    /// No words wait on the record: an answer a service from before ANSWER1b took had already ended it, or an ended record
    /// the person never wrote to (MSG1b).
    /// </summary>
    public const string Ended = ContinueCodes.Ended;

    /// <summary>
    /// The agent refused because another of its clients holds the conversation (MSG1b, D137 §2.2): <c>codex-acp</c>'s
    /// <c>data.reason: "thread_active_writer"</c>, read from that structured data and never from its sentence.
    /// </summary>
    public const string Elsewhere = ContinueCodes.Elsewhere;

    /// <summary>A teammate's record (D47 §6): its process and its conversation are on their machine and account. A never.</summary>
    public const string Teammate = ContinueCodes.Teammate;

    /// <summary>An intake, answered through its ask (INT4h). A never.</summary>
    public const string Intake = ContinueCodes.Intake;

    /// <summary>A session that stood down: its quest is someone else's, so it has nothing to go on with. A never.</summary>
    public const string StoodDown = ContinueCodes.StoodDown;

    /// <summary>
    /// The person started a conversation with them (MSG1f2): *Start a conversation with these words*, which takes them off
    /// the record naming that conversation. Never judged: only that press says it, on the went note it leaves.
    /// </summary>
    public const string Started = ContinueCodes.Started;

    /// <summary>
    /// The quest of the session that asked another repository and waited (D79) resumed in a new session once the answer came, handed
    /// the words said to the asker meanwhile (SAYHELD1): its own conversation was never handed the answer. Never judged: only that
    /// resume says it, on the went note it leaves.
    /// </summary>
    public const string Resumed = ContinueCodes.Resumed;

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
