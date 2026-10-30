namespace Daoris.Knowledge.Mcp;

/// <summary>A call the call layer refuses: its closed code (the foundation design §2.3) and the sentence its caller reads.</summary>
public sealed record CallRefusal(string Code, string Text);

/// <summary>
/// The call layer's table (FOUNDATION1r; the foundation design §2.3): which tools a named session is served, by where the connector
/// places it (<see cref="SessionWork"/>), over every tool the host registers. A tool <see cref="ToolClasses"/> does not class, and a work
/// the table does not know, refuse.
/// </summary>
/// <remarks>
/// <c>McpRoleFilterTests</c> holds every cell to <c>fixtures/call-table.json</c> over the host's registration. Each work keeps what it
/// was served before the table, at the filter and at the boxes that judged by the work alone, which keep their checks as their store's
/// guard: the channel's, Ask Daoris's checkouts, housekeeping and settings, and a brainstorm's turn. The foundation design §7 changes
/// two answers: a named session the connector cannot place is served the reads alone (row 10), and a reviewer and a brainstorm's voice
/// only what their handed rules allow (row 11).
/// </remarks>
public static class CallTable
{
    /// <summary>A named session the connector cannot place: no record of this machine's, or a work the table does not know.</summary>
    public const string Unplaced = "unplaced";

    /// <summary>A named session whose record could not be read.</summary>
    public const string Unread = "unread";

    /// <summary>A tool the table does not class.</summary>
    public const string UnknownTool = "unknown-tool";

    /// <summary>A tool the session's work is not served.</summary>
    public const string ForbiddenWork = "forbidden-work";

    /// <summary>Every column a named session is placed in: each work a record reads as, a brainstorm's two turns, and the two placements.</summary>
    public static IReadOnlyList<SessionWork> Columns { get; } =
    [
        .. ParticipantWork.Codes.Where(code => code != ParticipantWork.Brainstorm).Select(code => new SessionWork(code)),
        SessionWork.Generating, SessionWork.Synthesizing, SessionWork.Unplaced, SessionWork.Unread,
    ];

    /// <summary>
    /// What a partner is served (COAGENT1b, D199 point 4): the knowledge and reading tools, its lead's line, and its answer. The driver's
    /// <c>PartnerPermissions.Offered</c> names the same.
    /// </summary>
    public static IReadOnlyList<string> PartnerServed { get; } =
    [
        "knowledge_convergence", "knowledge_get", "knowledge_repositories", "knowledge_search", "line_read", "partner_answer", "registry",
    ];

    private static readonly HashSet<string> Partner = new(PartnerServed, StringComparer.Ordinal);

    /// <summary>
    /// What a second opinion's reviewer is served (the foundation design §7 row 11; D155): the knowledge, the quests and their handbacks
    /// it reads, and its one say. The driver's <c>OpinionPermissions.Offered</c> hands the same where its agent takes rules.
    /// </summary>
    public static IReadOnlyList<string> ReviewerServed { get; } =
    [
        "handback_read", "knowledge_convergence", "knowledge_get", "knowledge_repositories", "knowledge_search", "opinion_give", "quest_list",
        "registry",
    ];

    private static readonly HashSet<string> Reviewer = new(ReviewerServed, StringComparer.Ordinal);

    private static readonly HashSet<string> Turns = new([BrainstormBox.GiveTool, BrainstormBox.SynthesizeTool], StringComparer.Ordinal);

    /// <summary>Ask Daoris's own tools, whose boxes serve its record alone.</summary>
    private static readonly HashSet<string> AskDaorisOwn = new(
    [
        "checkout_read", "housekeeping_clean_up", "housekeeping_discard_branch", "housekeeping_inspect", "settings_cap", "settings_cooloff",
        "settings_language", "settings_notify", "settings_timeout",
    ], StringComparer.Ordinal);

    /// <summary>What a record that serves another is refused, a helper's as a partner's: it publishes, takes and closes no quest.</summary>
    private static readonly HashSet<string> Quests = new(["quest_publish", "quest_respond"], StringComparer.Ordinal);

    /// <summary>The ask's channel, which a helper and a reviewer do not have.</summary>
    private static readonly HashSet<string> Channel = new(["channel_post", "channel_read"], StringComparer.Ordinal);

    /// <summary>Whether <paramref name="work"/> is served <paramref name="tool"/>.</summary>
    public static bool Served(SessionWork work, string? tool) => Refusal(work, tool) is null;

    /// <summary>The refusal of <paramref name="tool"/> to <paramref name="work"/>, or null where it is served.</summary>
    public static CallRefusal? Refusal(SessionWork work, string? tool)
    {
        if (work == SessionWork.Unnamed) return null;
        var name = tool ?? "";
        if (ToolClasses.Of(tool) is not { } kind) return new(UnknownTool, Words.UnknownTool(name));
        // The foundation design §7 row 10: a named session that cannot be placed is served the reads, and every mutation refused.
        if (work == SessionWork.Unread) return kind == ToolClass.Read ? null : new(Unread, Words.Unread(name));

        return work switch
        {
            { Work: ParticipantWork.Brainstorm, Turn: SessionWork.Generator } => Turn(name, BrainstormBox.GiveTool, "generator"),
            { Work: ParticipantWork.Brainstorm, Turn: SessionWork.Synthesizer } => Turn(name, BrainstormBox.SynthesizeTool, "synthesis"),
            { Turn: not null } => Unknown(name, kind),
            { Work: ParticipantWork.Partner } => Partner.Contains(name) ? null : Forbidden(Words.Partner(name)),
            // §7 row 11: a reviewer is served what its handed rules offer, on every door.
            { Work: ParticipantWork.Review } => Reviewer.Contains(name) ? null : Forbidden(Words.Reviewer(name)),
            { Work: ParticipantWork.AskDaoris } => Ordinary(name, askDaoris: true),
            { Work: ParticipantWork.Help } => Serving(name) ?? Channelled(name) ?? Ordinary(name),
            { Work: ParticipantWork.Build or ParticipantWork.Design or ParticipantWork.Chat or ParticipantWork.Intake } => Ordinary(name),
            _ => Unknown(name, kind),
        };
    }

    private static CallRefusal Forbidden(string text) => new(ForbiddenWork, text);

    /// <summary>A pass's own result tool alone; the other turn's is refused in its box's words, anything else in the pass's.</summary>
    private static CallRefusal? Turn(string name, string own, string turn) =>
        name == own ? null
        : Turns.Contains(name) ? Forbidden($"This turn is a brainstorm's {turn}: answer through `{own}`. Nothing was kept.")
        : Forbidden(Words.Pass(name));

    /// <summary>What every work but a pass, a partner and a reviewer is refused: a turn's result tools, and Ask Daoris's own but to it.</summary>
    private static CallRefusal? Ordinary(string name, bool askDaoris = false) =>
        Turns.Contains(name) ? Forbidden("This connector serves no brainstorm turn Daoris started, so there is no answer to give here. Nothing was kept.")
        : !askDaoris && AskDaorisOwn.Contains(name) ? Forbidden(
            $"`{name}` is Ask Daoris's alone, and this session is no Ask Daoris conversation of this machine's. Nothing was done.")
        : null;

    private static CallRefusal? Serving(string name) =>
        Quests.Contains(name)
            ? Forbidden($"`{name}` is refused to a session that serves another: it publishes, takes and closes no quest. Say what you found in "
                        + "your answer to the session you serve.")
            : null;

    private static CallRefusal? Channelled(string name) =>
        Channel.Contains(name) ? Forbidden("A helper or a reviewer answers one question and has no channel: say it in your answer.") : null;

    /// <summary>A session the table cannot place: the reads alone.</summary>
    private static CallRefusal? Unknown(string name, ToolClass kind) => kind == ToolClass.Read ? null : new(Unplaced, Words.Unplaced(name));

    /// <summary>The sentences a refused caller reads.</summary>
    private static class Words
    {
        public static string UnknownTool(string name) =>
            $"`{name}` is no tool Daoris has classed for a session's connector, so no session it names may call it. Nothing was done.";

        public static string Unread(string name) =>
            $"`{name}` waits: Daoris could not read this session's record, so it cannot say the tool is this session's to call. Try again.";

        public static string Unplaced(string name) =>
            $"`{name}` was not run: Daoris cannot place the session this connector names, so it serves this session the reads alone "
            + $"({string.Join(", ", ToolClasses.Reads)}). Nothing was done.";

        public static string Partner(string name) =>
            $"`{name}` is not a partner's to call: a session Daoris started to write a piece asks nobody and acts on nothing outside its "
            + "copy. Leave your files in your copy, and say your answer with partner_answer.";

        public static string Reviewer(string name) =>
            $"`{name}` is not a second opinion's to call: a reviewer reads, gives its opinion with opinion_give, and changes nothing. "
            + "Say in your opinion what you would have done.";

        public static string Pass(string name) =>
            $"`{name}` is not a brainstorm pass's to call: a pass reads the room it was handed and gives its one answer through its turn's "
            + "result tool, and calls nothing else.";
    }
}
