namespace Daoris.Driver;

/// <summary>An instruction as the composer joined it (CONTEXT1): its text, which a session is handed, and its account.</summary>
public sealed record ComposedInstruction(string Text, InstructionAccount Account);

/// <summary>
/// The sections an instruction is composed of (CONTEXT1), in the order an instruction holds them. A TWIN with the page's
/// catalogues (<c>work.handed.section.*</c>), held both ways by <c>HandedCodesTests</c> and the page's <c>handed.test.ts</c>.
/// </summary>
public static class HandedSections
{
    /// <summary>Who the agent is and the quest: its id, who asked, its title and its body.</summary>
    public const string Quest = "quest";

    /// <summary>What the quest carries beside its words: links, files, the quest it follows and its next step.</summary>
    public const string Carried = "carried";

    public const string Requirements = "requirements";

    /// <summary>The person's words on the ask the quest was asked by.</summary>
    public const string Words = "words";

    public const string GoAheads = "go-aheads";

    public const string Standing = "standing";

    /// <summary>The person's words to an earlier session on the quest after it ended, handed to a first start.</summary>
    public const string WrittenTo = "written-to";

    /// <summary>For a resume: the question an earlier session asked another repository, and its answer.</summary>
    public const string Answered = "answered";

    /// <summary>For a carry-on: what the session before left, its record's end, its tree, its plan and its last words.</summary>
    public const string CarryOn = "carry-on";

    /// <summary>How to take the quest and close it, or carry it on.</summary>
    public const string Close = "close";

    public const string Language = "language";

    public const string Map = "map";

    public const string Landing = "landing";

    /// <summary>The other checkouts the session may read and not change.</summary>
    public const string Reading = "reading";

    /// <summary>Where to look before asking.</summary>
    public const string Look = "look";

    /// <summary>The indexes the session's tree keeps, where the look starts.</summary>
    public const string Indexes = "indexes";

    /// <summary>That the person's words met second-hand are only a reading.</summary>
    public const string Attributed = "attributed";

    /// <summary>Asking another repository, and stopping for the person.</summary>
    public const string Asking = "asking";

    /// <summary>How the closing note keeps what needs the person apart from the readings, each naming its source.</summary>
    public const string Closing = "closing";

    public const string Proposing = "proposing";

    /// <summary>
    /// A set-up step's own instruction (REVIEWENV1c2): the environment it shows its chain's work in, as the review rule set on this
    /// machine declares it, and how to show it and say it. Counted with what the quest carries before this section was its own.
    /// </summary>
    public const string SetUp = "set-up";

    /// <summary>Never writing outside the repository, and what may be written where the person declared it.</summary>
    public const string Boundary = "boundary";

    /// <summary>The permission rules, handed beside the instruction rather than in it.</summary>
    public const string Rules = "rules";

    /// <summary>An intake's instruction, whose parts are not counted apart.</summary>
    public const string Intake = "intake";

    // ——— The brief a new part is handed of its line (SESSCTX1b, D189 point 4; the session-context design §3.2, §3.4): a
    // conversation's first prompt, or a section of a driven carry-on's instruction, counted by its own sections.

    /// <summary>The brief's opening: the session it continues, its agent, why it moved, and the request as first written.</summary>
    public const string BriefOpening = "brief-opening";

    /// <summary>The words the session took, each marked by who said it.</summary>
    public const string BriefWords = "brief-words";

    /// <summary>Each turn's outcome: its last message and what it did.</summary>
    public const string BriefTurns = "brief-turns";

    /// <summary>The work's state as the brief was composed: the tree, the plan, the last checks, what is still asked.</summary>
    public const string BriefState = "brief-state";

    /// <summary>The outgoing agent's own note, as its claim.</summary>
    public const string BriefNote = "brief-note";

    /// <summary>The session's other parts, beside its line.</summary>
    public const string BriefParts = "brief-parts";

    /// <summary>What the brief's bound left out, by count.</summary>
    public const string BriefLeft = "brief-left";
}

/// <summary>Where a section came from (CONTEXT1): <c>work.handed.source.*</c>.</summary>
public static class HandedSources
{
    /// <summary>The quest, as the service answered it.</summary>
    public const string Quest = "quest";

    /// <summary>The ask's record on the service: the person's words and the go-aheads.</summary>
    public const string Ask = "ask";

    /// <summary>What the person set on this machine for the session's repository: a standing answer, a session language.</summary>
    public const string Repository = "repository";

    /// <summary>What the person set on this machine for the session's workspace: a session language.</summary>
    public const string Workspace = "workspace";

    /// <summary>The person's words to an earlier session on the quest.</summary>
    public const string Written = "written";

    /// <summary>The earlier session's record on this quest, and the tree it worked in.</summary>
    public const string Record = "record";

    /// <summary>The session's own tree: its code map, its indexes.</summary>
    public const string Tree = "tree";

    /// <summary>How the session's tree lands, from this machine's landing rule.</summary>
    public const string Landing = "landing";

    /// <summary>The checkouts the person declared this repository may read or change.</summary>
    public const string Checkouts = "checkouts";

    /// <summary>
    /// The driver's own words, the same for every session of its kind. Named apart from the <c>Driver</c> class (LOOPOUT1):
    /// the structure gate reads names, and nothing outside the loop names the loop.
    /// </summary>
    public const string DriverWords = "driver";

    /// <summary>This machine's permission rules.</summary>
    public const string Permissions = "permissions";
}

/// <summary>Why nothing of a section was handed (CONTEXT1): <c>work.handed.none.*</c>.</summary>
public static class HandedNones
{
    /// <summary>Its source held nothing to hand: a quest with no requirement, a tree with no code map.</summary>
    public const string Empty = "empty";

    /// <summary>No ask asked the quest, so there are no words of the person's and no go-aheads on one.</summary>
    public const string NoAsk = "no-ask";

    /// <summary>The service answered none: unread, or a host from before them.</summary>
    public const string NotAnswered = "not-answered";

    /// <summary>The person set none on this machine.</summary>
    public const string NotSet = "not-set";

    /// <summary>The session's agent takes no rules from Daoris, so none were handed.</summary>
    public const string Agent = "agent";
}

/// <summary>
/// What was left out of a section, and why (CONTEXT1): <c>work.handed.cut.*</c>, each entry saying exactly the values
/// <see cref="Values"/> names for it.
/// </summary>
public static class HandedCuts
{
    /// <summary>Requirements past the instruction's bound on them.</summary>
    public const string RequirementsBound = "requirements-bound";

    /// <summary>The older of the person's words, past the bound on them.</summary>
    public const string WordsOlder = "words-older";

    /// <summary>Words each cut at the bound on one word.</summary>
    public const string WordsLong = "words-long";

    /// <summary>The words could not be read for this start.</summary>
    public const string WordsUnread = "words-unread";

    /// <summary>Words said before Daoris kept them on the ask, which nothing back-fills.</summary>
    public const string WordsNotKept = "words-not-kept";

    /// <summary>Go-aheads past the instruction's bound on them.</summary>
    public const string GoAheadsBound = "go-aheads-bound";

    /// <summary>Acts each cut at the bound on what one touches.</summary>
    public const string ActsLong = "acts-long";

    /// <summary>A standing answer's characters past the bound on it.</summary>
    public const string StandingLong = "standing-long";

    /// <summary>Plan steps past the bound on a carry-on's plan.</summary>
    public const string PlanBound = "plan-bound";

    /// <summary>Indexes past the most the look names, counted rather than named.</summary>
    public const string IndexesBound = "indexes-bound";

    /// <summary>Files not on this machine, named without a path.</summary>
    public const string FilesElsewhere = "files-elsewhere";

    /// <summary>This machine's rules file could not be read, so only the defaults were handed.</summary>
    public const string RulesUnread = "rules-unread";

    /// <summary>A brief's words past the bound on them, the oldest after the request first (SESSCTX1b).</summary>
    public const string BriefWordsOlder = "brief-words-older";

    /// <summary>A brief's turns' outcomes past what its bound leaves them, the oldest first (SESSCTX1b).</summary>
    public const string BriefTurnsOlder = "brief-turns-older";

    /// <summary>A brief's items each clipped at its own bound (SESSCTX1b): a word, a message, the request, a plan step, an output.</summary>
    public const string BriefClipped = "brief-clipped";

    /// <summary>Entries of a brief's lists left out past their bound (SESSCTX1b): what is uncommitted, the commits.</summary>
    public const string BriefList = "brief-list";

    /// <summary>The session's other parts past the bound on them (SESSCTX1b).</summary>
    public const string BriefPartsBound = "brief-parts-bound";

    /// <summary>The values each cut's entry says, by name: <c>n</c> and <c>limit</c> numbers, <c>from</c> and <c>to</c> moments.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Values { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [RequirementsBound] = ["n", "limit"],
        [WordsOlder] = ["n", "limit", "from", "to"],
        [WordsLong] = ["n", "limit"],
        [WordsUnread] = [],
        [WordsNotKept] = ["from"],
        [GoAheadsBound] = ["n", "limit"],
        [ActsLong] = ["n", "limit"],
        [StandingLong] = ["n", "limit"],
        [PlanBound] = ["n", "limit"],
        [IndexesBound] = ["n", "limit"],
        [FilesElsewhere] = ["n"],
        [RulesUnread] = [],
        [BriefWordsOlder] = ["n", "limit"],
        [BriefTurnsOlder] = ["n", "limit"],
        [BriefClipped] = ["n"],
        [BriefList] = ["n"],
        [BriefPartsBound] = ["n", "limit"],
    };
}
