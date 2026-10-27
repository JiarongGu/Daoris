namespace Daoris.Driver;

/// <summary>What a clear is asked of (HIST1c, D153 point 1; the history-clearing design §1.1).</summary>
public enum HistoryScope
{
    /// <summary>A workspace's finished history: every unit in it that may go, the home's left-over files, and its intake's room where it keeps no ask.</summary>
    Workspace,

    /// <summary>One closed quest's work.</summary>
    Quest,

    /// <summary>One ask's work, whole or not at all.</summary>
    Ask,

    /// <summary>One closed quest's failed sessions of this machine's.</summary>
    Failed,
}

// Every constant of HistoryKinds, HistoryWords, HistoryWaits, HistoryStands and HistoryAwaitedBy is held, with the service's
// spellings, to one table: the service suite's fixtures/history-words.json, which HistoryWordsTwinTests reads (HIST1m).

/// <summary>The kinds of unit, as the service's history door spells them (HIST1b).</summary>
public static class HistoryKinds
{
    public const string Quest = "quest";
    public const string Ask = "ask";
    public const string Failed = "failed";

    public static bool Is(string? kind) => kind is Quest or Ask or Failed;
}

/// <summary>
/// The words a unit, or a piece of one, is kept by (the history-clearing design §1.2): the service's, which judge the records
/// (HIST1b), and this machine's, which judge the trees, the landings and the processes (HIST1c). A door says each in its own
/// words by the word, never by the sentence beside it.
/// </summary>
public static class HistoryWords
{
    public const string Unknown = "unknown";
    public const string Open = "open";
    public const string Asked = "asked";
    public const string Live = "live";
    public const string NeedsYou = "needs-you";
    public const string Awaited = "awaited";
    public const string Unpushed = "unpushed";
    public const string NotOurs = "not-ours";

    /// <summary>A session's tree is still on this machine: the clean-up or *Discard tree* frees it (D88).</summary>
    public const string TreeHere = "tree-here";

    /// <summary>A landing's branch for one of its sessions still stands: the clean-up frees it once it has merged.</summary>
    public const string LandingStands = "landing-stands";
}

/// <summary>
/// Which of a word's sentences is meant, where one word says several things (the design §1.2's table): the variant a catalogue
/// keeps beside the word's own sentence. Read from the facts the word names, never from the service's sentence.
/// </summary>
public static class HistoryContexts
{
    /// <summary><c>unknown</c> of a quest, where the word's own sentence is an ask's.</summary>
    public const string Quest = "quest";

    /// <summary><c>open</c>: the quest is taken, where the word's own sentence is an open one's.</summary>
    public const string Taken = "taken";

    /// <summary><c>open</c>: failed sessions of a quest still open or taken, whose strikes are counted from them (D58).</summary>
    public const string Failed = "failed";

    /// <summary><c>live</c>: a teammate's record still reads as running on their machine.</summary>
    public const string Teammate = "teammate";

    /// <summary><c>live</c>: its automatic landing still runs.</summary>
    public const string Landing = "landing";

    /// <summary><c>needs-you</c>: a done held for the person's yes.</summary>
    public const string Held = "held";

    /// <summary><c>needs-you</c>: a conflict nobody dismissed.</summary>
    public const string Conflict = "conflict";

    /// <summary><c>needs-you</c>: an ask proposed or open.</summary>
    public const string Ask = "ask";

    /// <summary><c>needs-you</c>: a rule proposal a session of the work made.</summary>
    public const string Proposal = "proposal";

    /// <summary><c>needs-you</c>: a rule proposal for the ask.</summary>
    public const string ProposalAsk = "proposalAsk";

    /// <summary><c>awaited</c>: an open quest a session of the work published.</summary>
    public const string Published = "published";

    /// <summary><c>awaited</c>: a chain's next step, still open, builds on its work.</summary>
    public const string Chain = "chain";
}

/// <summary>
/// What waits on the person, as the service names it beside <c>needs-you</c> (HIST1l): the service judged it from the records it
/// read, so the driver says the sentence meant from these and not from a second read, which may have moved since.
/// </summary>
public static class HistoryWaits
{
    /// <summary>A session of the work parked to ask the person: the word's own sentence.</summary>
    public const string Parked = "parked";

    /// <summary>A done held for the person's yes.</summary>
    public const string Held = "held";

    /// <summary>A conflict nobody dismissed.</summary>
    public const string Conflict = "conflict";

    /// <summary>The ask, proposed or open.</summary>
    public const string Ask = "ask";

    /// <summary>A rule proposal nobody settled: a session's where the refusal names one, else the ask's.</summary>
    public const string Proposal = "proposal";
}

/// <summary>
/// How the work in progress stands, as the service names it beside <c>open</c> (HIST1m), for the reason <see cref="HistoryWaits"/>
/// is read: the service judged it from the quest it read, and a second read here may find it taken or closed since.
/// </summary>
public static class HistoryStands
{
    /// <summary>The quest is still open: the word's own sentence.</summary>
    public const string Open = "open";

    /// <summary>The quest is taken.</summary>
    public const string Taken = "taken";
}

/// <summary>
/// The open work that names a unit, as the service names it beside <c>awaited</c> (HIST1m): judged from the quests the service
/// read, so a quest that had its answer or closed between the two reads is not said in another's sentence.
/// </summary>
public static class HistoryAwaitedBy
{
    /// <summary>An open question a session of the work published.</summary>
    public const string Question = "question";

    /// <summary>A taken quest that asked it and waits on its answer: the word's own sentence.</summary>
    public const string Asker = "asker";

    /// <summary>A chain's open next step, which builds on its work.</summary>
    public const string Step = "step";
}

/// <summary>
/// Why a unit stays on this machine, or a piece of it is kept: its word, the variant of its sentence, what it names, and the
/// sentence a terminal prints (the service's for the records' half, the driver's for this machine's).
/// </summary>
public sealed record HistoryKeep(string Word, string Message)
{
    /// <summary>One of <see cref="HistoryContexts"/>, or null for the word's own sentence.</summary>
    public string? Context { get; init; }

    public string? Quest { get; init; }

    public string? Ask { get; init; }

    public string? Session { get; init; }

    /// <summary>The machine a teammate's record ran on.</summary>
    public string? Machine { get; init; }

    public string? Workspace { get; init; }

    /// <summary>The repository a landing's standing branch is in.</summary>
    public string? Repository { get; init; }

    /// <summary>A landing's standing branch.</summary>
    public string? Branch { get; init; }
}

/// <summary>One unit as both halves judged it: what it takes, by id, what that holds on the disk, and why it stays, if it does.</summary>
public sealed record HistoryUnitPlan(string Kind, string Id, string? Workspace)
{
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>Of <see cref="Quests"/>, those a remote numbered: forgotten here, and kept by the team (§3.2).</summary>
    public IReadOnlyList<string> Forgotten { get; init; } = [];

    public IReadOnlyList<string> Asks { get; init; } = [];

    /// <summary>This machine's session records it takes.</summary>
    public IReadOnlyList<string> Sessions { get; init; } = [];

    /// <summary>This machine's copies of a teammate's records it takes, keyed <c>origin/id</c>.</summary>
    public IReadOnlyList<string> Teammates { get; init; } = [];

    /// <summary>The runs among <see cref="Quests"/> that are bound here, whose bindings go with the unit (WORKFLOW1e3); never on the wire.</summary>
    public IReadOnlyList<string> Bindings { get; init; } = [];

    /// <summary>Why the whole unit stays, the first piece a person meets; null when it may go.</summary>
    public HistoryKeep? Keep { get; init; }

    /// <summary>Pieces listed and kept while the unit goes: a teammate's failed session.</summary>
    public IReadOnlyList<HistoryKeep> Kept { get; init; } = [];

    /// <summary>What the home holds of it: its sessions' files and its quests' and ask's kept files.</summary>
    public HistoryBytes Bytes { get; init; } = HistoryBytes.None;

    public bool Clearable => Keep is null;

    public HistoryUnitName Name => new(Kind, Id);
}

/// <summary>The first press's answer: every unit, and for a workspace, the reading.</summary>
public sealed record HistoryPlan(HistoryScope Scope, string Id, IReadOnlyList<HistoryUnitPlan> Units)
{
    public HistoryReading? Reading { get; init; }
}

/// <summary>What the second press did (the design §5): what went, what changed since the list and stayed, and what the home freed.</summary>
public sealed record HistoryOutcome(HistoryScope Scope, string Id)
{
    /// <summary>How many units the press sent.</summary>
    public int Listed { get; init; }

    /// <summary>Each unit cleared, as the service judged it where it cleared it.</summary>
    public IReadOnlyList<HistoryUnitPlan> Cleared { get; init; } = [];

    /// <summary>Each unit the list said may go that stayed, with what keeps it now.</summary>
    public IReadOnlyList<HistoryUnitPlan> Changed { get; init; } = [];

    public int Quests => Cleared.Sum(unit => unit.Quests.Count);

    public int Asks => Cleared.Sum(unit => unit.Asks.Count);

    public int Sessions => Cleared.Sum(unit => unit.Sessions.Count);

    public int Teammates => Cleared.Sum(unit => unit.Teammates.Count);

    public int Forgotten => Cleared.Sum(unit => unit.Forgotten.Count);

    /// <summary>Left-over files a workspace's clear took (§2.3).</summary>
    public int LeftOver { get; init; }

    /// <summary>Whether a workspace's clear took its intake's room.</summary>
    public bool Intake { get; init; }

    /// <summary>What the home freed, in bytes: the units', the left-over files' and the room's.</summary>
    public long Bytes { get; init; }

    /// <summary>Files the disk would not let go of: left over now, for the next clear of a workspace to take.</summary>
    public int Failed { get; init; }

    /// <summary>Whether it took anything at all: a clear that took nothing writes no log line.</summary>
    public bool Took => Cleared.Count > 0 || LeftOver > 0 || Intake;
}
