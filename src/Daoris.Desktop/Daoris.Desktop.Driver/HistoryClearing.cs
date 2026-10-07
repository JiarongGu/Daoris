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

    /// <summary>Why the whole unit stays, the first piece a person meets; null when it may go.</summary>
    public HistoryKeep? Keep { get; init; }

    /// <summary>Pieces listed and kept while the unit goes: a teammate's failed session.</summary>
    public IReadOnlyList<HistoryKeep> Kept { get; init; } = [];

    /// <summary>What the home holds of it: its sessions' files and its quests' and ask's kept files.</summary>
    public HistoryBytes Bytes { get; init; } = HistoryBytes.None;

    public bool Clearable => Keep is null;

    public HistoryUnitName Name => new(Kind, Id);
}

/// <summary>What a clear would take now (the design §2.4).</summary>
public sealed record HistoryTakes(int Quests, int Asks, int Sessions, int Teammates, long Bytes)
{
    public static HistoryTakes None { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>
/// What the home keeps of one workspace's finished work, and what a clear would free (the design §2.4). Counts and bytes only:
/// never a path or a title.
/// </summary>
public sealed record HistoryReading(string Workspace)
{
    /// <summary>Its closed quests held here.</summary>
    public int Quests { get; init; }

    /// <summary>Its asks closed, or whose work closed.</summary>
    public int Asks { get; init; }

    /// <summary>This machine's session records of that work.</summary>
    public int Sessions { get; init; }

    /// <summary>This machine's copies of a teammate's records of that work.</summary>
    public int Teammates { get; init; }

    /// <summary>What that work holds on the disk, by kind.</summary>
    public HistoryBytes Bytes { get; init; } = HistoryBytes.None;

    /// <summary>The intake's room, in bytes.</summary>
    public long Intake { get; init; }

    /// <summary>What a clear would take now, the left-over files and an intake's room it frees included.</summary>
    public HistoryTakes Takes { get; init; } = HistoryTakes.None;

    /// <summary>How many units are kept, by the word that keeps each.</summary>
    public IReadOnlyDictionary<string, int> KeptBy { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>This machine's conversations in it that served no quest, which only *Delete…* takes (D126 §5.4).</summary>
    public int Conversations { get; init; }

    public long ConversationBytes { get; init; }

    /// <summary>For the whole home: files of records no store has any more (§2.3), which every workspace's clear takes.</summary>
    public int LeftOver { get; init; }

    public long LeftOverBytes { get; init; }

    /// <summary>For the whole home: the machine log's size, which a clear never touches (§7).</summary>
    public long Log { get; init; }
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

/// <summary>What a clear works with: the service, the home, the driver's file and this machine's processes.</summary>
public sealed record HistoryWorld(ServiceClient Service, string Home, string ConfigPath, SessionProcesses Processes)
{
    /// <summary>The loop's record of conversations, so it forgets a cleared one's numbering; null removes the files alone.</summary>
    public SessionEvents? Events { get; init; }

    /// <summary>Where <c>history.cleared</c> goes; null writes none.</summary>
    public MachineLog? Log { get; init; }

    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>How long nothing must have touched a file before it is left over (§2.3): an hour.</summary>
    public TimeSpan Untouched { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How the clear removes a file, or a folder whole, of the home (HIST1j): from the disk. A test hands one that refuses a path,
    /// as a process holding a transcript makes the disk refuse it, so what a clear frees is held on every platform.
    /// </summary>
    public Action<string, bool> Remover { get; init; } = SessionHomeFiles.FromDisk;
}

/// <summary>
/// Clearing finished history from this machine, this machine's half (HIST1c, D153; the history-clearing design §2, §4–§5,
/// §6.3, §6.5), for both doors: the screen's <c>HISTORY_PLAN</c> and <c>HISTORY_CLEAR</c>, and the terminal's history verbs.
/// </summary>
/// <remarks>
/// <para><b>Listed first, then pressed</b> (D88): <see cref="PlanAsync"/> asks the service's judgement (HIST1b's
/// <c>GET /api/history</c>), judges this machine's half of each unit the service would clear (a process still running, an
/// automatic landing still trying, a tree still here, a landing's branch still standing), and reads what the home keeps;
/// it changes nothing. <see cref="ClearAsync"/> sends exactly the units the list held, judges each again by both halves,
/// asks the service to clear what may go, then removes what the home kept of each cleared session through
/// <see cref="SessionHomeFiles"/>, the helper D126's delete calls too, and tidies what names it.</para>
///
/// <para><b>Never</b> a tree or a branch, an open or taken quest, what waits on the person, the team's copy on a remote,
/// the machine log, the usage, the accounts, or a folder Daoris did not write (§1.3). <b>Nothing clears by itself</b>
/// (§7): only a press calls this.</para>
/// </remarks>
public static partial class HistoryClearing
{
    /// <summary>The scope's word: <c>workspace</c>, <c>quest</c>, <c>ask</c> or <c>failed</c>.</summary>
    public static string Word(HistoryScope scope) => scope.ToString().ToLowerInvariant();

    /// <summary>The first press (§5 step 1): every unit with what it takes and why it would stay, and a workspace's reading.</summary>
    /// <exception cref="DriverException">The service did not answer, or has no history door, in its words.</exception>
    public static async Task<HistoryPlan> PlanAsync(HistoryWorld world, HistoryScope scope, string id, CancellationToken ct = default)
    {
        var named = Named(scope, id);
        var units = await world.Service.HistoryAsync(Query(scope, named), ct).ConfigureAwait(false);
        var facts = await Facts.ReadAsync(world.Service, ct).ConfigureAwait(false);
        var machine = new Machine(world);
        var planned = units.Select(unit => Judge(unit, facts, machine)).ToList();
        return new HistoryPlan(scope, named, planned)
        {
            Reading = scope == HistoryScope.Workspace ? Read(world, named, planned, facts, machine) : null,
        };
    }

    /// <summary>
    /// The second press (§5): exactly <paramref name="units"/>, the units the list said may go, each judged again by the
    /// service's half and then this machine's; what may go cleared by the service, its sessions' files removed and what names
    /// it tidied; one log line for the press. A unit that changed since is kept and counted, never refused for the rest.
    /// </summary>
    /// <param name="door"><see cref="PluginEvents.Screen"/> or <see cref="PluginEvents.Terminal"/>, which the log line names.</param>
    /// <exception cref="DriverException">The service did not answer, or has no history door, before anything was removed.</exception>
    public static async Task<HistoryOutcome> ClearAsync(
        HistoryWorld world, HistoryScope scope, string id, IReadOnlyList<HistoryUnitName> units, string door, CancellationToken ct = default)
    {
        var named = Named(scope, id);
        var listed = Listed(scope, named, units);

        // 1. Judged again: the service's half, then this machine's.
        var facts = await Facts.ReadAsync(world.Service, ct).ConfigureAwait(false);
        var machine = new Machine(world);
        var judged = (await AgainAsync(world, scope, named, listed, ct).ConfigureAwait(false))
            .Select(unit => Judge(unit, facts, machine))
            .ToList();
        var changed = judged.Where(unit => !unit.Clearable).ToList();
        var going = judged.Where(unit => unit.Clearable).ToList();

        // 2. The records, each unit in one transaction of the service's; what it keeps is the service's to remove.
        var failed = new List<string>();
        var answered = new List<HistoryUnitPlan>();
        if (going.Count > 0)
        {
            foreach (var answer in await world.Service.ClearHistoryAsync([.. going.Select(unit => unit.Name)], ct).ConfigureAwait(false))
            {
                var before = going.FirstOrDefault(unit => Same(unit.Name, answer.Unit.Name));
                if (!answer.Cleared)
                {
                    changed.Add(Plan(answer.Unit, answer.Unit.Refusal is { } refusal
                        ? Said(refusal, answer.Unit.Kind, facts)
                        : new HistoryKeep(HistoryWords.Unknown, answer.Message), facts, machine.Files));
                    continue;
                }

                // The kept files the service removed with the records (HIST1j): what this press measured of them, less what is still
                // on the disk, so one the disk kept frees nothing whether or not the service said so. What it says it could not
                // remove is failed, and left over for the next clear of a workspace (§2.3).
                failed.AddRange(answer.FailedQuests.Select(quest => $"{QuestFolder}/{quest}"));
                failed.AddRange(answer.FailedAsks.Select(ask => $"{AskFolder}/{ask}"));
                var kept = Math.Max(0, (before?.Bytes.Kept ?? 0) - KeptFiles(machine.Files, answer.Unit.Quests, answer.Unit.Asks).Kept);
                answered.Add(Plan(answer.Unit, keep: null, facts, machine.Files) with { Bytes = HistoryBytes.None with { Kept = kept } });
            }
        }

        // 3. What the home kept of each cleared session, after the service's yes: measured as it goes, so only what went is counted.
        var gone = answered.SelectMany(unit => unit.Sessions.Concat(unit.Teammates)).Distinct(StringComparer.Ordinal).ToList();
        var removed = machine.Files.Remove(gone, world.Events);
        failed.AddRange(removed.Failed);
        var cleared = answered
            .Select(unit => unit with
            {
                Bytes = unit.Sessions.Aggregate(unit.Bytes, (sum, session) => sum + removed.Bytes.GetValueOrDefault(session, HistoryBytes.None)),
            })
            .ToList();

        // 4. What names it, read against the records as they stand after the clear; nothing that reads them where they did not answer.
        var after = await Facts.TryReadAsync(world.Service, ct).ConfigureAwait(false);
        Tidy(world, scope, named, cleared, facts, after, failed);
        var (leftOver, leftOverBytes) = scope == HistoryScope.Workspace && after is not null
            ? RemoveLeftOver(world, after, new Machine(world), failed)
            : (0, 0L);
        var (intake, intakeBytes) = scope == HistoryScope.Workspace && after is not null
            ? RemoveRoom(world, named, after, machine.Files, failed)
            : (false, 0L);

        var outcome = new HistoryOutcome(scope, named)
        {
            Listed = listed.Count,
            Cleared = cleared,
            Changed = changed,
            LeftOver = leftOver,
            Intake = intake,
            Bytes = cleared.Sum(unit => unit.Bytes.Total) + leftOverBytes + intakeBytes,
            Failed = failed.Count,
        };

        // 5. One line for the press, counts only (§6.5); a clear that took nothing writes nothing.
        if (outcome.Took)
        {
            world.Log?.Info("history.cleared",
                ("scope", Word(scope)),
                ("quests", outcome.Quests),
                ("asks", outcome.Asks),
                ("sessions", outcome.Sessions + outcome.Teammates),
                ("forgotten", outcome.Forgotten),
                ("bytes", outcome.Bytes),
                ("kept", outcome.Changed.Count),
                ("door", door));
        }

        return outcome;
    }

    /// <summary>The scope's id as the door names it: a workspace by its name, the default one for none; a quest or an ask without its <c>#</c>.</summary>
    private static string Named(HistoryScope scope, string id) =>
        scope == HistoryScope.Workspace ? RemoteTarget.Workspace(id) : (id ?? "").Trim().TrimStart('#').Trim();

    /// <summary>The service's door's one scope, escaped.</summary>
    private static string Query(HistoryScope scope, string id) => scope switch
    {
        HistoryScope.Workspace => $"workspace={Uri.EscapeDataString(id)}",
        HistoryScope.Ask => $"ask={Uri.EscapeDataString(id)}",
        HistoryScope.Failed => $"quest={Uri.EscapeDataString(id)}&failed=true",
        _ => $"quest={Uri.EscapeDataString(id)}",
    };

    private static HistoryScope ScopeOf(string kind) => kind switch
    {
        HistoryKinds.Ask => HistoryScope.Ask,
        HistoryKinds.Failed => HistoryScope.Failed,
        _ => HistoryScope.Quest,
    };

    private static string KindOf(HistoryScope scope) => scope switch
    {
        HistoryScope.Ask => HistoryKinds.Ask,
        HistoryScope.Failed => HistoryKinds.Failed,
        _ => HistoryKinds.Quest,
    };

    /// <summary>
    /// The units a press sent, each once, by a kind the door knows and an id; for one quest's, one ask's or one quest's failed
    /// sessions, only that unit, since the press was offered on its page.
    /// </summary>
    private static List<HistoryUnitName> Listed(HistoryScope scope, string named, IReadOnlyList<HistoryUnitName> units)
    {
        var listed = units
            .Select(unit => new HistoryUnitName((unit.Kind ?? "").Trim().ToLowerInvariant(), (unit.Id ?? "").Trim().TrimStart('#').Trim()))
            .Where(unit => HistoryKinds.Is(unit.Kind) && unit.Id.Length > 0)
            .DistinctBy(unit => (unit.Kind, unit.Id.ToLowerInvariant()))
            .ToList();
        return scope == HistoryScope.Workspace
            ? listed
            : [.. listed.Where(unit => Same(unit, new HistoryUnitName(KindOf(scope), named)))];
    }

    private static bool Same(HistoryUnitName a, HistoryUnitName b) =>
        string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) && string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Each listed unit as the service judges it now: a workspace's from one listing, any it no longer lists asked alone; one
    /// the service does not answer for is unknown.
    /// </summary>
    private static async Task<IReadOnlyList<HistoryUnitView>> AgainAsync(
        HistoryWorld world, HistoryScope scope, string named, IReadOnlyList<HistoryUnitName> listed, CancellationToken ct)
    {
        if (listed.Count == 0) return [];
        var listing = scope == HistoryScope.Workspace
            ? await world.Service.HistoryAsync(Query(scope, named), ct).ConfigureAwait(false)
            : [];
        var again = new List<HistoryUnitView>();
        foreach (var name in listed)
        {
            var unit = listing.FirstOrDefault(each => Same(each.Name, name))
                       ?? (await world.Service.HistoryAsync(Query(ScopeOf(name.Kind), name.Id), ct).ConfigureAwait(false))
                           .FirstOrDefault(each => Same(each.Name, name));
            again.Add(unit ?? new HistoryUnitView(name.Kind, name.Id, null)
            {
                Refusal = new HistoryRefusalView(HistoryWords.Unknown, $"No {(name.Kind == HistoryKinds.Ask ? "ask" : "quest")} `#{name.Id}` on this machine.")
                {
                    Quest = name.Kind == HistoryKinds.Ask ? null : name.Id,
                    Ask = name.Kind == HistoryKinds.Ask ? name.Id : null,
                },
            });
        }

        return again;
    }

    /// <summary>One unit judged by both halves: the service's refusal first, said with its facts, then this machine's.</summary>
    private static HistoryUnitPlan Judge(HistoryUnitView unit, Facts facts, Machine machine) =>
        Plan(unit, unit.Refusal is { } refusal ? Said(refusal, unit.Kind, facts) : MachineHalf(unit, facts, machine), facts, machine.Files);

    private static HistoryUnitPlan Plan(HistoryUnitView unit, HistoryKeep? keep, Facts facts, SessionHomeFiles files) =>
        new(unit.Kind, unit.Id, unit.Workspace)
        {
            Quests = unit.Quests,
            Forgotten = unit.Forgotten,
            Asks = unit.Asks,
            Sessions = unit.Sessions,
            Teammates = unit.Teammates,
            Keep = keep,
            Kept = [.. unit.Kept.Select(piece => Said(piece, unit.Kind, facts))],
            Bytes = unit.Sessions.Aggregate(HistoryBytes.None, (sum, session) => sum + files.Size(session))
                    + KeptFiles(files, unit.Quests, unit.Asks),
        };

    /// <summary>
    /// The service's word with its facts, and which of its sentences is meant: what the service named beside the word (what
    /// waits beside <c>needs-you</c>, HIST1l; how the work stands beside <c>open</c> and the open work beside <c>awaited</c>,
    /// HIST1m), else read from the records the word names (§1.2's table); never from the service's sentence, which only a
    /// terminal prints.
    /// </summary>
    private static HistoryKeep Said(HistoryRefusalView refusal, string kind, Facts facts) => new(refusal.Word, refusal.Message)
    {
        Context = ContextOf(refusal, kind, facts),
        Quest = refusal.Quest,
        Ask = refusal.Ask,
        Session = refusal.Session,
        Machine = refusal.Origin,
        Workspace = refusal.Workspace,
    };

    private static string? ContextOf(HistoryRefusalView refusal, string kind, Facts facts) => refusal.Word switch
    {
        HistoryWords.Unknown => kind == HistoryKinds.Ask ? null : HistoryContexts.Quest,
        HistoryWords.Open when kind == HistoryKinds.Failed => HistoryContexts.Failed,
        // How the service read the quest (HIST1m), as what waits below: a second read here may find it taken or closed since.
        HistoryWords.Open when refusal.Stands is HistoryStands.Taken => HistoryContexts.Taken,
        HistoryWords.Open when refusal.Stands is HistoryStands.Open => null,
        // A host before it, or a word this build does not know: the quest read here says which.
        HistoryWords.Open => Is(facts.Quest(refusal.Quest)?.Status, "Taken") ? HistoryContexts.Taken : null,
        HistoryWords.Live => refusal.Origin is not null ? HistoryContexts.Teammate : null,
        // What the service named waiting (HIST1l), judged from the records it read: a second read here may have moved since.
        HistoryWords.NeedsYou when refusal.Waits is HistoryWaits.Parked => null,
        HistoryWords.NeedsYou when refusal.Waits is HistoryWaits.Held => HistoryContexts.Held,
        HistoryWords.NeedsYou when refusal.Waits is HistoryWaits.Conflict => HistoryContexts.Conflict,
        HistoryWords.NeedsYou when refusal.Waits is HistoryWaits.Ask => HistoryContexts.Ask,
        HistoryWords.NeedsYou when refusal.Waits is HistoryWaits.Proposal =>
            refusal.Session is null ? HistoryContexts.ProposalAsk : HistoryContexts.Proposal,
        // A host before it names nothing waiting, and a word this build does not know says nothing it can use, so the records
        // read here say which. The service names a parked session alone, and a rule proposal by its session; a held done and a
        // conflict by the quest.
        HistoryWords.NeedsYou when refusal.Session is { } session =>
            Is(facts.Record(session)?.State, "awaiting-person") ? null : HistoryContexts.Proposal,
        HistoryWords.NeedsYou when refusal.Quest is { } quest =>
            facts.Quest(quest)?.Held == true ? HistoryContexts.Held : HistoryContexts.Conflict,
        HistoryWords.NeedsYou =>
            facts.Ask(refusal.Ask)?.State is { } state && (Is(state, "Open") || Is(state, "Proposed")) ? HistoryContexts.Ask : HistoryContexts.ProposalAsk,
        // The open work the service named (HIST1m): the quest read here may have had its answer, or closed, since.
        HistoryWords.Awaited when refusal.By is HistoryAwaitedBy.Question => HistoryContexts.Published,
        HistoryWords.Awaited when refusal.By is HistoryAwaitedBy.Asker => null,
        HistoryWords.Awaited when refusal.By is HistoryAwaitedBy.Step => HistoryContexts.Chain,
        // A host before it, or a word this build does not know. A question its session published names that session; a taken
        // quest awaiting the answer waits; else a chain's step.
        HistoryWords.Awaited when refusal.Session is not null => HistoryContexts.Published,
        HistoryWords.Awaited => facts.Quest(refusal.Quest)?.Awaits is not null ? null : HistoryContexts.Chain,
        _ => null,
    };

    private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// This machine's half of a unit the service would clear (§1.2), in the order a person meets them: a process still running
    /// here, an automatic landing still trying, a tree still here, a landing's branch still standing. Null when none holds it.
    /// </summary>
    private static HistoryKeep? MachineHalf(HistoryUnitView unit, Facts facts, Machine machine)
    {
        foreach (var session in unit.Sessions.Where(machine.Alive))
        {
            return new HistoryKeep(HistoryWords.Live, $"Session `{session}` is still running; stop it first.") { Session = session };
        }

        foreach (var session in unit.Sessions.Where(machine.Landing))
        {
            return new HistoryKeep(HistoryWords.Live, $"Session `{session}`'s work is still being landed.")
            {
                Session = session, Context = HistoryContexts.Landing,
            };
        }

        foreach (var session in unit.Sessions.Where(session => machine.TreeHere(facts.Record(session))))
        {
            return new HistoryKeep(HistoryWords.TreeHere, $"Session `{session}`'s tree is still here; clean it up, or discard it, first.")
            {
                Session = session,
            };
        }

        foreach (var session in unit.Sessions)
        {
            if (machine.Standing(session) is not { } landing) continue;
            return new HistoryKeep(
                HistoryWords.LandingStands,
                $"The branch `{landing.Branch}` a landing made still stands in `{landing.Repository}`; clean it up once it has merged.")
            {
                Session = session, Branch = landing.Branch, Repository = landing.Repository,
            };
        }

        return null;
    }

    /// <summary>The records a judgement reads beside the service's word: every session record, quest and ask, closed ones included.</summary>
    private sealed record Facts(IReadOnlyList<SessionRecord> Records, IReadOnlyList<QuestView> Quests, IReadOnlyList<AskView> Asks)
    {
        public static async Task<Facts> ReadAsync(ServiceClient service, CancellationToken ct) => new(
            await service.SessionRecordsAsync(ct).ConfigureAwait(false),
            await service.EveryQuestAsync(ct).ConfigureAwait(false),
            await service.EveryAskAsync(ct).ConfigureAwait(false));

        /// <summary>The records as they stand after a clear, or null where the service did not answer: then nothing is tidied from them.</summary>
        public static async Task<Facts?> TryReadAsync(ServiceClient service, CancellationToken ct)
        {
            try
            {
                return await ReadAsync(service, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
            {
                return null;
            }
        }

        public SessionRecord? Record(string? id) =>
            id is null ? null : Records.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.Ordinal));

        public QuestView? Quest(string? id) =>
            id is null ? null : Quests.FirstOrDefault(quest => string.Equals(quest.Id, id, StringComparison.OrdinalIgnoreCase));

        public AskView? Ask(string? id) =>
            id is null ? null : Asks.FirstOrDefault(ask => string.Equals(ask.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>This machine's facts a judgement reads: its processes, its automatic landings, its trees and its landings.</summary>
    private sealed class Machine(HistoryWorld world)
    {
        private readonly SessionTrees _trees = new(world.Home);
        private readonly IReadOnlyList<LandedBranch> _standing = new LandedBranches(world.Home).All();
        private readonly IReadOnlyList<AutoLanding> _landing = new AutoLandings(world.Home).Open();

        public SessionHomeFiles Files { get; } = new(world.Home, world.Remover);

        public SessionTrees Trees => _trees;

        /// <summary>A process for it alive on this machine: this shell's, or one another driver sharing the home marked.</summary>
        public bool Alive(string session) => SessionEvents.IsId(session) && world.Processes.AliveOnThisMachine(session);

        /// <summary>An automatic landing of it still trying (LAND2b).</summary>
        public bool Landing(string session) =>
            _landing.Any(entry => string.Equals(entry.Session, session, StringComparison.OrdinalIgnoreCase));

        /// <summary>Its own tree still here with anything in it, as D126's delete judges it: a checkout is no tree of its own.</summary>
        public bool TreeHere(SessionRecord? record) =>
            record?.Tree is { } tree && Held(tree) && !SessionTrees.TreeGone(tree);

        /// <summary>A landing's branch naming it, as the landing's own or among its advances, that still stands.</summary>
        public LandedBranch? Standing(string session) => _standing.LastOrDefault(entry => entry.Names(session));

        public bool Held(string tree)
        {
            try
            {
                return _trees.Holds(tree);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // A path no folder could have is no tree of this home's.
                return false;
            }
        }
    }
}
