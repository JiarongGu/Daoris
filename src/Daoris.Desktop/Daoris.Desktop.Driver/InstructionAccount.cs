namespace Daoris.Driver;

/// <summary>An instruction as the composer joined it (CONTEXT1): its text, which a session is handed, and its account.</summary>
public sealed record ComposedInstruction(string Text, InstructionAccount Account);

/// <summary>
/// What a session was handed, section by section (CONTEXT1, D143 point 1): the composer's account of the instruction it
/// composed, kept beside the instruction on the session's record. Not the words again: each section's size, where it came
/// from, and what its bound left out and why.
/// </summary>
/// <remarks>
/// <para><b>Codes where a screen words it, the driver's sentence where a terminal reads it</b> (D143 point 1, LANG1a): every
/// section, source, absence and cut is a code the page words in the reader's language, and each carries the driver's own
/// English (<see cref="HandedSection.Said"/>), which <c>daoris-driver trace</c> prints and a page shows, marked, for a code it
/// does not know.</para>
///
/// <para><b>The sizes add up by construction.</b> <see cref="TargetPrompt.Composed"/> builds the instruction from named pieces
/// and counts each piece as it joins them, so the handed sections' characters sum to <see cref="Chars"/>, the instruction's
/// length. What is handed beside the instruction (the permission rules) is a section with no characters.</para>
///
/// <para><b>Values are facts, never a machine path</b>: an id, a repository's name, a repository-relative file, a branch, a
/// language's code, a count, a moment.</para>
/// </remarks>
/// <param name="Chars">The instruction's length in characters, as it was handed.</param>
/// <param name="Sections">
/// The sections handed, in the instruction's order; then what was handed beside it; then the sections that could have been
/// handed and were not, each saying why (D143 point 3: a fact not kept is said missing, and an absence is said too).
/// </param>
public sealed record InstructionAccount(int Chars, IReadOnlyList<HandedSection> Sections)
{
    /// <summary>
    /// This account with what was handed beside the instruction (the permission rules), after the sections handed and before
    /// those that were not.
    /// </summary>
    public InstructionAccount Beside(HandedSection beside)
    {
        var handed = Sections.TakeWhile(section => section.None is null).ToList();
        return this with { Sections = [.. handed, beside, .. Sections.Skip(handed.Count)] };
    }
}

/// <summary>One section of what a session was handed (CONTEXT1): its size, its source, and what its bound left out.</summary>
/// <param name="Name">What part it is, one of <see cref="HandedSections"/>.</param>
/// <param name="Source">Where it came from, one of <see cref="HandedSources"/>.</param>
/// <param name="Said">The driver's English for it, as a terminal prints it.</param>
public sealed record HandedSection(string Name, string Source, string Said)
{
    /// <summary>Its characters in the instruction: 0 for a section not handed, null for what is handed beside it.</summary>
    public int? Chars { get; init; }

    /// <summary>What its source names: a quest's or an ask's id, a repository's name, a repository-relative file, a branch, a language's code.</summary>
    public string? From { get; init; }

    /// <summary>How many of its items it handed: requirements, words, go-aheads, indexes, plan steps, checkouts, rules.</summary>
    public int? Shown { get; init; }

    /// <summary>How many there were to hand.</summary>
    public int? Of { get; init; }

    /// <summary>When its source was set, where the source keeps it (a standing answer).</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>Why nothing of it was handed, one of <see cref="HandedNones"/>; null for a section that was.</summary>
    public string? None { get; init; }

    /// <summary>What was left out of it, each with why; null where nothing was.</summary>
    public IReadOnlyList<HandedCut>? Cuts { get; init; }
}

/// <summary>What a section's bound, or a fact about its source, left out (CONTEXT1).</summary>
/// <param name="Code">Why, one of <see cref="HandedCuts"/>.</param>
/// <param name="Said">The driver's English for it, as a terminal prints it.</param>
public sealed record HandedCut(string Code, string Said)
{
    /// <summary>How many were left out: items, or characters for one cut by its length. The catalogues call it <c>n</c>.</summary>
    public int? Count { get; init; }

    /// <summary>The bound that left them out, in characters or items.</summary>
    public int? Limit { get; init; }

    /// <summary>For words left out, when the first of them was said; for words not kept, from when they are.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>For words left out, when the last of them was said.</summary>
    public DateTimeOffset? To { get; init; }
}

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

    /// <summary>Never writing outside the repository, and what may be written where the person declared it.</summary>
    public const string Boundary = "boundary";

    /// <summary>The permission rules, handed beside the instruction rather than in it.</summary>
    public const string Rules = "rules";

    /// <summary>An intake's instruction, whose parts are not counted apart.</summary>
    public const string Intake = "intake";
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

    /// <summary>The driver's own words, the same for every session of its kind.</summary>
    public const string Driver = "driver";

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
    };
}
