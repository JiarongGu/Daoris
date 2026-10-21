using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What <i>What needs you</i> is judged from (PUSH1d4, D182 point 6): each input in the page's shapes, as the other slices and the routes
/// already write them, so the judge and its tests read one shape.
/// </summary>
/// <remarks>Every list defaults to empty: an input that holds nothing judges no row, as the page's defaults did.</remarks>
public sealed record AttentionFacts
{
    /// <summary>Every session record, closed ones and every workspace's included, as the sessions slice holds them.</summary>
    public IReadOnlyList<JsonObject> Sessions { get; init; } = [];

    /// <summary>Every quest, as the quests slice holds them.</summary>
    public IReadOnlyList<JsonObject> Quests { get; init; } = [];

    /// <summary>The registry, each row's <c>repository</c>, <c>adopted</c>, <c>addressable</c> and <c>workspace</c>.</summary>
    public IReadOnlyList<JsonObject> Registry { get; init; } = [];

    /// <summary>Every ask, as the asks slice holds them, each with its go-aheads.</summary>
    public IReadOnlyList<JsonObject> Asks { get; init; } = [];

    /// <summary>The folders the last look held for the agent's trust (D73), as the tick hands them.</summary>
    public IReadOnlyList<JsonObject> Untrusted { get; init; } = [];

    /// <summary>What agents proposed about the rules (PERM2), as <c>RULES</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Proposals { get; init; } = [];

    /// <summary>What the last look considered of each open quest, as the tick hands it.</summary>
    public IReadOnlyList<JsonObject> Considered { get; init; } = [];

    /// <summary>Each session's place in Sessions' list, as the groups slice holds its rows.</summary>
    public IReadOnlyList<JsonObject> Groups { get; init; } = [];

    /// <summary>The second opinions that wait on the person, as <c>OPINION_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Opinions { get; init; } = [];

    /// <summary>The agents' asks for a helper that wait on the person's yes, as <c>HELPER_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Helpers { get; init; } = [];

    /// <summary>The agents' asks to hand their sessions over that wait on the person's yes or no (NOTIFY2e), as the look holds them.</summary>
    public IReadOnlyList<JsonObject> HandOvers { get; init; } = [];

    /// <summary>Each unit of work's milestone, as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Works { get; init; } = [];

    /// <summary>Every plugin step's attempt held for the person, as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Held { get; init; } = [];

    /// <summary>Every node record that does not read (FABRIC-PERFORM1j), as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Unreadable { get; init; } = [];

    /// <summary>Every closure graph's end (FABRIC-CLOSE1j), as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Ends { get; init; } = [];

    /// <summary>The tool calls the sessions hold for the person's answer, as <c>PERMISSION_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Permissions { get; init; } = [];

    /// <summary>The answers the answers-alone bound holds for the person (COWORK1e), as <c>ANSWER_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Answers { get; init; } = [];

    /// <summary>The roster's doors, each as <c>HARNESSES</c> answers it: each account's last reading with its time (ROSTER1).</summary>
    public IReadOnlyList<JsonObject> Doors { get; init; } = [];

    /// <summary>How each agent's accounts are used, as <c>ACCOUNTS</c> answers it (<c>{agents}</c>); null where it has not answered.</summary>
    public JsonObject? Use { get; init; }

    /// <summary>The starts the last look held on their accounts, an intake's among them, as the tick hands them (UX6d).</summary>
    public IReadOnlyList<JsonObject> Waits { get; init; } = [];

    /// <summary>The presses a terminal sent that wait for the window's Confirm (CONFIRMSEEN1), as <c>PERSON_CONFIRMATIONS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Confirmations { get; init; } = [];
}

/// <summary>
/// Who needs the person (PUSH1d4, D182 point 6): <i>What needs you</i>'s rows and what the Sessions badge counts, judged once for every
/// window. Each row carries its kind, its id, since when it has waited and the values it is worded from; what it is called, its
/// sentences and the order the band draws are the page's (D182's PUSH1d4 note).
/// </summary>
/// <remarks>
/// <para><b>No row starts anything to find out</b> (design §6.3): every input is a record a slice already holds or the look already
/// kept. A row whose wait no record says has a <c>since</c> of null, never the clock's now, which would move at every judgement.</para>
///
/// <para><b>The scope a window reads</b> (NEEDSSCOPE1): a row that is a session's, a quest's or an ask's carries that record's
/// <c>workspace</c>, however its wait reached the judge, the service's records or the driver's own lists (a review, an opinion, a
/// helper's ask, a hand-over ask, a held call, a held answer, a unit's clean-up), by which a window scoped to one circle filters, as its lists were. Only
/// what is the machine's carries none, and every window shows it: a terminal's press, a rule for the whole machine, a folder or an
/// account whose holds span workspaces, a unit no record owns; and a row whose record is not in hand, which no window may lose.</para>
/// </remarks>
public static class NeedsAPerson
{
    /// <summary>The intake states in which an ask is the harness's to answer, not yet the person's (D65 §1b).</summary>
    private static readonly HashSet<string> IntakeBusy = new(StringComparer.Ordinal) { "queued", "starting", "working" };

    /// <summary>The states in which a session's turn runs: an agent's ask to hand it over holds nothing up until it ends (NOTIFY2e).</summary>
    private static readonly HashSet<string> TurnRuns = new(StringComparer.Ordinal) { "queued", "starting", "working" };

    /// <summary>How a quest's and an ask's units are named (<see cref="QuestUnits.IdOf"/>, <see cref="AskUnits.IdOf"/>).</summary>
    private static readonly string QuestUnit = QuestUnits.IdOf(""), AskUnit = AskUnits.IdOf("");

    /// <summary>How the work inventory names a session's unit before a branch record follows it.</summary>
    private const string SessionUnit = "session:";

    /// <summary>Every row that waits on the person, each one in no order: the band orders by group and wait.</summary>
    public static IReadOnlyList<JsonObject> Rows(AttentionFacts facts)
    {
        var sessions = First(facts.Sessions);
        var quests = First(facts.Quests);
        var asks = First(facts.Asks);
        var rows = new List<JsonObject>();
        var units = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var unit in facts.Works)
        {
            if (Text(unit, "unit") is { } id) units.TryAdd(id, unit);
        }

        // The workspace of the session a row is, where its record is in hand (NEEDSSCOPE1); else none, and every window shows it.
        string? SessionScope(string? id) => id is not null && sessions.GetValueOrDefault(id) is { } record ? Scope(record) : null;
        // A unit's: a quest's or an ask's unit by that record; else the session of the unit as the work slice holds it, or the one its id names.
        string? UnitScope(string? unit) => unit switch
        {
            null => null,
            _ when unit.StartsWith(QuestUnit, StringComparison.Ordinal) => quests.GetValueOrDefault(unit[QuestUnit.Length..]) is { } quest ? Scope(quest) : null,
            _ when unit.StartsWith(AskUnit, StringComparison.Ordinal) => asks.GetValueOrDefault(unit[AskUnit.Length..]) is { } ask ? Scope(ask) : null,
            _ when units.GetValueOrDefault(unit) is { } held => SessionScope(Text(held, "session")),
            _ when unit.StartsWith(SessionUnit, StringComparison.Ordinal) => SessionScope(unit[SessionUnit.Length..]),
            _ => null,
        };

        var live = facts.Asks.Where(ask => Text(ask, "state") is "Open" or "Proposed").ToList();
        JsonObject? IntakeOf(JsonObject ask) => Truthy(ask, "intake") is { } intake ? sessions.GetValueOrDefault(intake) : null;

        // Whom an ask in this workspace could be published to: what the host says can be asked, as the ask's page offers (D70).
        List<string> ReceiversIn(string? workspace) =>
        [
            .. facts.Registry
                .Where(row => CanBeAsked(row) && (Text(row, "workspace") ?? "default") == workspace)
                .Select(row => Text(row, "repository"))
                .OfType<string>()
                .Order(StringComparer.Ordinal),
        ];

        // An ask waits on the person unless its intake is busy with it: an intake parked asking makes it the intake's row, else a proposal.
        foreach (var ask in live)
        {
            var intake = IntakeOf(ask);
            if (IntakeBusy.Contains(Text(intake, "state") ?? "completed")) continue;
            var workspace = Text(ask, "workspace");
            if (intake is not null && Text(intake, "state") == "awaiting-person")
            {
                var row = Row("intake", Text(ask, "id"), Text(intake, "updated"), Scope(ask));
                row["where"] = workspace;
                row["note"] = NoteOf(intake);
                rows.Add(row);
                continue;
            }

            var proposal = Row("proposal", Text(ask, "id"), Text(ask, "asked"), Scope(ask));
            proposal["where"] = workspace;
            // A refused receiver's sentence is the service's, verbatim; otherwise the page says what was proposed.
            if (Text(ask, "note") is { } refused) proposal["detail"] = refused;
            proposal["publishTo"] = Strings(Objects(ask["proposal"]).Select(match => Text(match, "repository")));
            proposal["choices"] = Strings(ReceiversIn(workspace));
            rows.Add(proposal);
        }

        // Every go-ahead still asked on an ask that is not closed: one row each, since each is its own act (KNOWUSE1a).
        foreach (var ask in facts.Asks.Where(ask => Text(ask, "state") != "Closed"))
        {
            foreach (var goAhead in Objects(ask["goAheads"]).Where(goAhead => Text(goAhead, "state") == "asked"))
            {
                var first = Objects(goAhead["asked"]).FirstOrDefault();
                var row = Row("go-ahead", $"{Text(ask, "id")}#{Part(goAhead["number"])}", Text(first, "at") ?? Text(ask, "updated"), Scope(ask));
                row["ask"] = Text(ask, "id");
                row["number"] = goAhead["number"]?.DeepClone();
                row["goAhead"] = new JsonObject { ["kind"] = Text(goAhead, "kind"), ["on"] = Text(goAhead, "on"), ["act"] = Text(goAhead, "act") };
                // Why the first session needed it, in its words; and the session that asked it first, whose run its door opens.
                row["detail"] = Text(first, "why");
                row["session"] = Text(first, "session");
                if (Stale(ask, goAhead, facts) is { } stale) row["stale"] = stale;
                rows.Add(row);
            }
        }

        // A done its departure holds for the person's yes (DRIFT1d2), until they accept it. A set-up step its review holds waits for
        // their review instead (REVIEWENV1g), which no yes lifts, so it is the set-up's row and never this one.
        foreach (var quest in facts.Quests.Where(quest => quest["held"] is JsonValue held && held.TryGetValue<bool>(out var yes) && yes
                     && Truthy(quest, "accepted") is null && Text(quest, "hold") != "unreviewed"))
        {
            var row = Row("departure", Text(quest, "id"), Text(quest, "updated"), Scope(quest));
            row["where"] = Text(quest, "to");
            row["departed"] = Departed(quest);
            rows.Add(row);
        }

        // A set-up step whose newest set-up waits for the person's look (REVIEWENV1g, design §3.3): what it showed, and the set-up the
        // row drew, which its verdict names.
        foreach (var quest in facts.Quests)
        {
            if (Looked(quest) is not { } newest) continue;
            var row = Row("set-up", Text(quest, "id"), Text(newest, "at") ?? Text(quest, "updated"), Scope(quest));
            row["where"] = Text(quest, "to");
            row["setUp"] = new JsonObject { ["machine"] = Text(newest, "machine"), ["sequence"] = newest["sequence"]?.DeepClone() };
            row["local"] = newest["local"] is not JsonValue local || !local.TryGetValue<bool>(out var flag) || flag;
            row["session"] = Text(newest, "session");
            row["environment"] = Text(quest, "setUpIn");
            row["shows"] = Text(newest, "shows");
            rows.Add(row);
        }

        // A second opinion that waits on the person (XAGENT1g, design §9), from the driver's own list, its wait whole.
        foreach (var wait in facts.Opinions)
        {
            var row = Row("opinion", Text(wait, "session"), Text(wait, "since"), Text(wait, "workspace") ?? SessionScope(Text(wait, "session")));
            row["where"] = Text(wait, "repository");
            row["opinion"] = wait.DeepClone();
            rows.Add(row);
        }

        // What Sessions' list places To review (D126), from the groups the list reads: never a reader of its own. Ask Daoris's
        // conversations are reached from its own panel, never from Sessions (ASKRAIL1).
        foreach (var placed in facts.Groups.Where(placed => Text(placed, "group") == "review"))
        {
            var record = sessions.GetValueOrDefault(Text(placed, "session") ?? "");
            if (AskDaoris(record) || Text(placed, "repository") == HelpRoom.Repository) continue;
            var row = Row("review", Text(placed, "session"), Text(record, "updated"), record is null ? null : Scope(record));
            row["where"] = Text(record, "repository") ?? "";
            row["placed"] = placed.DeepClone();
            rows.Add(row);
        }

        // A park the person answered goes on at the driver's next look (ANSWER1c): it no longer needs them. A parked intake whose ask is
        // live is the ask's row.
        var standsFor = live.Select(ask => Truthy(ask, "intake")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var session in facts.Sessions.Where(session => Parks(session) && !standsFor.Contains(Text(session, "id") ?? "")))
        {
            var row = Row("parked", Text(session, "id"), Text(session, "updated"), Scope(session));
            row["where"] = Text(session, "repository");
            row["note"] = NoteOf(session);
            rows.Add(row);
        }

        // A quest parked on its failed sessions here (SESSUX1i): the planner's verdict, since its last session ended, else its last move.
        foreach (var consideration in facts.Considered.Where(consideration => Text(consideration, "verdict") == "Exhausted"))
        {
            var held = quests.GetValueOrDefault(Text(consideration, "quest") ?? "");
            var row = Row("parked-quest", Text(consideration, "quest"), Text(consideration, "since") ?? Text(held, "updated"), held is null ? null : Scope(held));
            row["where"] = Text(consideration, "repository");
            row["sitting"] = consideration.DeepClone();
            rows.Add(row);
        }

        // A quest this deployment has no registration for its receiver: no agent will pull it, whatever the receiver's case.
        var registered = facts.Registry.Select(row => Text(row, "repository")?.ToLowerInvariant()).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var quest in facts.Quests.Where(quest => Text(quest, "status") == "Open" && !registered.Contains((Text(quest, "to") ?? "").ToLowerInvariant())))
        {
            var row = Row("unanswerable", Text(quest, "id"), Text(quest, "filed"), Scope(quest));
            row["where"] = Text(quest, "to");
            rows.Add(row);
        }

        rows.AddRange(Folders(facts, quests, asks));

        // An agent's proposal to widen the rules (PERM2, D74) waits for the person's word: the proposal whole, why in its session's words.
        // Its workspace is the one its change reaches: a workspace's rule that workspace's, a repository's the proposing session's. A
        // rule for the whole machine carries none, deliberately: it reaches every workspace, so every window shows it (NEEDSSCOPE1).
        foreach (var proposal in facts.Proposals.Where(proposal => Text(proposal, "state") == "waiting"))
        {
            var reaches = Text(proposal, "scope") switch
            {
                "machine" => null,
                "workspace" when Text(proposal, "name") is { } workspace => workspace,
                _ => SessionScope(Text(proposal, "session")),
            };
            var row = Row("rule", Text(proposal, "id"), Text(proposal, "proposed"), reaches);
            row["detail"] = Text(proposal, "why");
            row["proposal"] = proposal.DeepClone();
            rows.Add(row);
        }

        // An agent's ask for a helper that waits on the person's yes (MIXAGENT2c): its question's first line as written, the session that
        // asked, and its repository where the session's record is in hand.
        foreach (var wait in facts.Helpers)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var question = Text(wait, "question") ?? "";
            var line = question.Split('\n')[0].Trim();
            var row = Row("helper", Text(wait, "id"), Truthy(wait, "at"), SessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["title"] = line.Length > 0 ? line : question;
            rows.Add(row);
        }

        // An agent's ask to hand its session over that waits on the person's yes or no (NOTIFY2e, MIXAGENT1c): by the ask's own id, its
        // note's first line as written, the session that asked and the agent it goes to. It stands still on it once that session's turn
        // is no longer running, since the session goes on only by their word; while its turn runs it holds nothing up yet.
        foreach (var wait in facts.HandOvers)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var note = Text(wait, "note") ?? "";
            var line = note.Split('\n')[0].Trim();
            var row = Row("hand-over", Text(wait, "id"), Truthy(wait, "at"), SessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["title"] = line.Length > 0 ? line : note;
            row["agent"] = Text(wait, "agent");
            row["stands"] = record is not null && !TurnRuns.Contains(Text(record, "state") ?? "");
            rows.Add(row);
        }

        // A tool call a session holds for the person's answer under asking (AUTONOMY1b3c), keyed by the session and the request, since a
        // request's id is only its agent's wire's.
        foreach (var wait in facts.Permissions)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var row = Row("permission", $"{Text(wait, "session")}#{Text(wait, "request")}", Truthy(wait, "since"), SessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["permission"] = wait.DeepClone();
            rows.Add(row);
        }

        // A third answer in a row with nobody between, which its session's work waits on the person for (COWORK1e, D188 point 4; the
        // message-urgency design §8.3): keyed by the session, its wait whole, the agent that answered among it.
        foreach (var wait in facts.Answers)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var row = Row("answer", Text(wait, "session"), Truthy(wait, "since"), SessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["answer"] = wait.DeepClone();
            rows.Add(row);
        }

        // A unit only the person's press settles (FABRIC-MILESTONE1b, OVERVIEWNOISE1b): one whose owner nothing records, or a tree its session
        // recorded that no branch record follows, each waiting on the clean-up's press; its blocked entries with the code the page words
        // each by. A guard the tidy keeps a branch by is said on its session's page and its branch's line; a wait that ends by itself is no row.
        foreach (var unit in facts.Works)
        {
            var waits = WaitsOnPerson(unit);
            if (waits.Count == 0) continue;
            var end = unit["end"] as JsonObject;
            var unknown = Text(end, "state") == "unknown";
            // A unit no record owns carries none, deliberately: nothing says whose it is, so every window shows it.
            var row = Row("work", Text(unit, "session") ?? Text(unit, "unit"), Text(end, "at"), UnitScope(Text(unit, "unit")));
            row["where"] = Text(unit, "repository");
            row["session"] = Text(unit, "session");
            // Its branch, else its tree's folder by name: never the unit's id, which a tree's path makes.
            row["title"] = Text(unit, "branch") ?? TreeName(waits.Concat(Objects(unit["entries"]))) ?? Text(unit, "unit");
            row["blocked"] = new JsonObject
            {
                ["unknown"] = unknown,
                ["entries"] = new JsonArray([.. waits.Where(entry => !unknown || Text(entry, "disposition") == "blocked").Select(Blocked)]),
            };
            rows.Add(row);
        }

        // A plugin step's attempt held for the person (FABRIC-PERFORM1g), a held close among them (FABRIC-CLOSE1q): the attempt whole, since
        // its word was recorded where one was, else since it was held.
        foreach (var attempt in facts.Held)
        {
            var row = Row(
                "work", $"{Part(attempt["unit"])}#{Part(attempt["node"])}#{Part(attempt["attempt"])}", Text(attempt, "resolvedAt") ?? Text(attempt, "at"),
                UnitScope(Text(attempt, "unit")));
            row["where"] = Text(attempt, "unit");
            row["held"] = attempt.DeepClone();
            rows.Add(row);
        }

        // A node record that does not read (FABRIC-PERFORM1j, §D3): nothing on its unit is judged or started until the person mends it, and
        // the row's press opens the folder it is kept in. By its file, since its unit may be unknown; since it was last written.
        foreach (var record in facts.Unreadable)
        {
            var row = Row("work", $"record#{Part(record["file"])}", Text(record, "at"), UnitScope(Text(record, "unit")));
            row["where"] = Text(record, "unit");
            row["unreadable"] = record.DeepClone();
            rows.Add(row);
        }

        // A closure graph closed only in part (FABRIC-CLOSE1j, §E5): while its close node's stop is offered, or the person's stop waits for
        // the next look, since it was found. An override is the person's own, and said on the work's foot alone.
        foreach (var end in facts.Ends.Where(end => Text(end, "kind") == "partially-closed"
                     && (Text(end, "resolution") is not null || (end["words"] as JsonArray)?.Any(word => word?.GetValue<string>() == "stop") == true)))
        {
            var row = Row("work", $"{Part(end["unit"])}#{Part(end["node"])}#{Part(end["quest"])}", Text(end, "at"), UnitScope(Text(end, "unit")));
            row["where"] = Text(end, "unit");
            row["ended"] = end.DeepClone();
            rows.Add(row);
        }

        // A press a terminal sent that waits for the person's Confirm in the window (CONFIRMSEEN1): its terminal holds still until they
        // answer on its card, or its time ends, which refuses it. By its route and the terminal as the system names it, since it was
        // asked; never a value it carries, which the card alone shows beside its Confirm. Every window shows it: the pipe is this machine's.
        foreach (var waiting in facts.Confirmations)
        {
            // No workspace, deliberately (NEEDSSCOPE1): a terminal's press is the machine's, whichever workspace its route touches.
            var row = Row("confirmation", Text(waiting, "id"), Text(waiting, "asked"));
            var asker = waiting["asker"] as JsonObject;
            row["confirmation"] = new JsonObject
            {
                ["module"] = Text(waiting, "module"),
                ["route"] = Text(waiting, "route"),
                ["asker"] = new JsonObject { ["process"] = Text(asker, "process"), ["pid"] = asker?["pid"]?.DeepClone() },
                ["expires"] = Text(waiting, "expires"),
                ["shownAt"] = Text(waiting, "shownAt"),
            };
            rows.Add(row);
        }

        // The starts waiting on their accounts, and the signed-out accounts no waiting start names (UX6d).
        rows.AddRange(AccountAttention.Rows(facts));
        return rows;
    }

    /// <summary>
    /// What the Sessions icon's badge counts (UX5 U20, D126 §2.5), each by a key of its own: this machine's sessions parked to ask that
    /// nobody answered, a parked intake among them, and each quest parked on its failed sessions, each with its workspace for the window's
    /// scope (NEEDSSCOPE1); a parked quest whose record is not in hand says none, and every window counts it. A teammate's park waits on them.
    /// </summary>
    public static IReadOnlyList<(string Key, JsonObject Value)> WaitingInSessions(AttentionFacts facts)
    {
        var counted = new List<(string, JsonObject)>();
        foreach (var session in facts.Sessions.Where(session => Parks(session) && !Elsewhere(Text(session, "id") ?? "")))
        {
            counted.Add(($"session:{Text(session, "id")}", new JsonObject { ["kind"] = "parked", ["workspace"] = Scope(session) }));
        }

        var quests = First(facts.Quests);
        foreach (var quest in facts.Considered.Where(consideration => Text(consideration, "verdict") == "Exhausted").Select(each => Text(each, "quest")).Distinct())
        {
            var parked = new JsonObject { ["kind"] = "parked-quest" };
            if (quests.GetValueOrDefault(quest ?? "") is { } record) parked["workspace"] = Scope(record);
            counted.Add(($"quest:{quest}", parked));
        }

        return counted;
    }

    /// <summary>
    /// The sessions the driver runs on this machine (SESSBADGE1, D76's note), each by a key of its own with its workspace for the window's
    /// scope: what the Sessions badge counts as a quantity and the status bar beside it. One count, the cap's: an active record of this
    /// machine's that takes a place under the cap (CAPCOUNT1, <see cref="ActiveSessions.HoldsSlot(string?, string?, string?, string?, string?)"/>),
    /// a parked one among them, never a conversation the person holds, Ask Daoris's or a chat. A teammate's runs on its own machine.
    /// </summary>
    public static IReadOnlyList<(string Key, JsonObject Value)> Working(AttentionFacts facts) =>
    [
        .. facts.Sessions
            .Where(session => Text(session, "id") is { Length: > 0 } id && !Elsewhere(id) && Active.Contains(Text(session, "state") ?? "")
                && ActiveSessions.HoldsSlot(
                    Text(session, "kind"), Text(session, "repository"), Text(session, "ask"), Text(session, "serves"), Text(session, "opinion")))
            .Select(session => ($"session:{Text(session, "id")}", new JsonObject { ["workspace"] = Text(session, "workspace") ?? "default" })),
    ];

    /// <summary>
    /// The states in which a record still holds its repository, the service's <c>Session.Active</c>: what the planner's snapshot lists as
    /// active, so the count is made over the records it counts.
    /// </summary>
    private static readonly HashSet<string> Active = new(StringComparer.Ordinal) { "queued", "starting", "working", "awaiting-person" };

    /// <summary>
    /// One row per folder in one file: two quests held on one untrusted tree are one grant (D73), waiting since the oldest thing it holds.
    /// Its identity is the file AND the folder (REV3): the folder alone gave one folder held in two accounts' files two rows with one key.
    /// </summary>
    private static IEnumerable<JsonObject> Folders(AttentionFacts facts, Dictionary<string, JsonObject> quests, Dictionary<string, JsonObject> asks)
    {
        var folders = new List<JsonObject>();
        var seen = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        // Each folder's workspace while every hold's record names the same one; null once one is not in hand or two differ (NEEDSSCOPE1).
        var scopes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var hold in facts.Untrusted)
        {
            var held = Truthy(hold, "quest") is { } quest ? quests.GetValueOrDefault(quest) : null;
            var asked = Truthy(hold, "ask") is { } ask ? asks.GetValueOrDefault(ask) : null;
            var since = Text(held, "filed") ?? Text(asked, "asked");
            var key = $"{Text(hold, "trustFile")}\n{Text(hold, "folder")}";
            var circle = held is not null ? Scope(held) : asked is not null ? Scope(asked) : null;
            if (seen.TryGetValue(key, out var row))
            {
                if (since is not null && (Text(row, "since") is not { } was || string.CompareOrdinal(since, was) < 0)) row["since"] = since;
                if (!string.Equals(scopes[key], circle, StringComparison.Ordinal)) scopes[key] = null;
                continue;
            }

            scopes[key] = circle;

            row = Row("trust", key, since);
            // Its repository where it holds a quest in hand; else the page names the ask it holds, or the quest by its id.
            row["where"] = Text(held, "to");
            row["trust"] = new JsonObject { ["folder"] = Text(hold, "folder"), ["trustFile"] = Text(hold, "trustFile") };
            // What its door opens: the quest it holds, or the ask whose intake it holds.
            if (Truthy(hold, "quest") is { } holds) row["quest"] = holds;
            if (Truthy(hold, "ask") is { } intake) row["ask"] = intake;
            seen[key] = row;
            folders.Add(row);
        }

        // The grant is the machine's, but what it holds is a quest's or an ask's: the row is their workspace's where they share one, and
        // carries none where they span two or one is not in hand, so every window shows it.
        foreach (var (key, workspace) in scopes)
        {
            if (workspace is not null) seen[key]["workspace"] = workspace;
        }

        return folders;
    }

    /// <summary>
    /// A go-ahead nothing on its ask could still use (GOAHEADSTALE1, D196's note): no session that asked it, no intake of the ask and no
    /// session of a quest the ask asked is queued, starting, working or parked, and no quest the ask asked is open, taken or held for the
    /// person's yes, whose next session would be handed its answer. It is then stale since the last thing on its ask moved: the newest of
    /// those records' moves and its own askings, so its age says how long nothing has needed it. Null while anything could still use it.
    /// </summary>
    /// <remarks>
    /// A record not in hand is not running: the sessions slice holds every record the service answers, so one it lacks was forgotten. What
    /// is on another ask never counts. Judged from the records alone, as every row is, so it moves when they do and never with the clock.
    /// </remarks>
    private static JsonObject? Stale(JsonObject ask, JsonObject goAhead, AttentionFacts facts)
    {
        if (Text(ask, "id") is not { } id) return null;
        var quests = facts.Quests.Where(quest => AskWords.AskOf(Text(quest, "from")) == id).ToList();
        if (quests.Any(StillOpen)) return null;

        var requests = Objects(goAhead["asked"]).ToList();
        var askers = requests.Select(request => Text(request, "session")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var asked = quests.Select(quest => Text(quest, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var on = facts.Sessions
            .Where(session => (Text(session, "id") is { } asker && askers.Contains(asker))
                || Text(session, "ask") == id
                || (Text(session, "quest") is { } quest && asked.Contains(quest)))
            .ToList();
        if (on.Any(session => Active.Contains(Text(session, "state") ?? ""))) return null;

        var moved = on.Select(session => Text(session, "updated"))
            .Concat(quests.Select(quest => Text(quest, "updated")))
            .Concat(requests.Select(request => Text(request, "at")));
        return new JsonObject { ["since"] = Latest(moved) };
    }

    /// <summary>
    /// A quest whose next session would still be handed its ask's go-aheads: open or taken, or a done held for the person's yes that no
    /// acceptance has let on yet (DRIFT1d2), as the page counts a held quest among those not closed.
    /// </summary>
    private static bool StillOpen(JsonObject quest) =>
        Text(quest, "status") is "Open" or "Taken"
        || (quest["held"] is JsonValue held && held.TryGetValue<bool>(out var yes) && yes && Truthy(quest, "accepted") is null);

    /// <summary>The newest of <paramref name="moments"/> as it was written, by the moment it names; null where none reads.</summary>
    private static string? Latest(IEnumerable<string?> moments)
    {
        (string Text, DateTimeOffset At)? newest = null;
        foreach (var moment in moments)
        {
            if (!DateTimeOffset.TryParse(moment, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at)) continue;
            if (newest is null || at > newest.Value.At) newest = (moment!, at);
        }

        return newest?.Text;
    }

    /// <summary>Which requirement a held done departed from first and why, and how many it departed from; null where none.</summary>
    private static JsonObject? Departed(JsonObject quest)
    {
        var departed = Objects(quest["answers"])
            .Where(answer => string.IsNullOrWhiteSpace(Text(answer, "met")) && !string.IsNullOrWhiteSpace(Text(answer, "departed")))
            .ToList();
        if (departed.Count == 0) return null;
        return new JsonObject { ["count"] = departed.Count, ["number"] = departed[0]["requirement"]?.DeepClone(), ["reason"] = Text(departed[0], "departed") };
    }

    /// <summary>
    /// A set-up step's newest set-up where it waits for the person's look (REVIEWENV1g, the page's <c>waitsForLook</c>): the step names
    /// an environment, it stands shown, and its newest set-up is named whole; null otherwise.
    /// </summary>
    private static JsonObject? Looked(JsonObject quest)
    {
        if (Truthy(quest, "setUpIn") is null) return null;
        var newest = quest["setUps"] is JsonArray setUps && setUps.Count > 0 ? setUps[^1] as JsonObject : null;
        return StepState(quest, newest) == "shown" && Reference(newest) is not null ? newest : null;
    }

    /// <summary>Where a set-up step stands, from its own record (design §3.2), as the page's <c>stepState</c> reads it.</summary>
    private static string StepState(JsonObject step, JsonObject? newest)
    {
        if (Text(step, "status") == "Declined") return "none";
        if (Objects(step["verdicts"]).Any(verdict => Text(verdict, "said") == "skipped")) return "skipped";
        if (Text(step, "status") is "Open" or "Taken" || newest is null) return "being-set-up";
        if (VerdictOn(step, newest, "reviewed") is not null) return "reviewed";
        if (VerdictOn(step, newest, "not-yet") is not null) return "not-yet";
        return "shown";
    }

    /// <summary>The person's newest verdict of one kind on a set-up: by the machine and sequence that name it, else by its commit.</summary>
    private static JsonObject? VerdictOn(JsonObject step, JsonObject setUp, string said)
    {
        var reference = Reference(setUp);
        return Objects(step["verdicts"]).Reverse().FirstOrDefault(verdict => Text(verdict, "said") == said && (reference is { } named
            ? verdict["setUp"] is JsonObject on && Text(on, "machine") == named.Machine && Number(on["sequence"]) == named.Sequence
            : string.Equals(Text(verdict, "commit") ?? "", Text(setUp, "commit") ?? "", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>A set-up's reference, whole, or null where the record carries half of one or none (REVIEWENV1b3).</summary>
    private static (string Machine, double Sequence)? Reference(JsonObject? setUp) =>
        Truthy(setUp, "machine") is { } machine && setUp!["sequence"] is JsonValue sequence && sequence.TryGetValue<double>(out var number)
            ? (machine, number)
            : null;

    /// <summary>
    /// What of a unit waits on the person (design §D0; OVERVIEWNOISE1b, only where they have an act): where no record names its owner, the
    /// entries its clean-up is blocked on, else the unit itself by its first entry; where its owner is known, the trees its session recorded
    /// and no branch record follows, which only the clean-up's press takes. A guard the tidy keeps a branch by has no act of the person's
    /// here, and a pending wait ends by itself.
    /// </summary>
    private static List<JsonObject> WaitsOnPerson(JsonObject unit)
    {
        var blocked = Objects((unit["cleanup"] as JsonObject)?["waits"]).Where(entry => Text(entry, "disposition") == "blocked").ToList();
        if (Text(unit["end"] as JsonObject, "state") != "unknown") return [.. blocked.Where(entry => Text(entry, "code") == InventoryCode.Unfollowed)];
        return blocked.Count > 0 ? blocked : [.. Objects(unit["entries"]).Take(1)];
    }

    /// <summary>A blocked entry as the row carries it: what it is, the driver's sentence, and the code the page words it by, with its facts.</summary>
    private static JsonNode Blocked(JsonObject entry)
    {
        var said = new JsonObject { ["kind"] = Text(entry, "kind"), ["resource"] = Text(entry, "resource"), ["detail"] = Text(entry, "detail") };
        if (Truthy(entry, "code") is { } code) said["code"] = code;
        if (entry["facts"] is JsonObject facts) said["facts"] = facts.DeepClone();
        return said;
    }

    /// <summary>The first tree entry's folder by its name (OVERVIEWNOISE1b): what a unit with no branch is called, never its path.</summary>
    private static string? TreeName(IEnumerable<JsonObject> entries) =>
        entries.Where(entry => Text(entry, "kind") == InventoryKind.Tree).Select(entry => Text(entry, "resource")).OfType<string>()
            .Select(path => path.Replace('\\', '/').TrimEnd('/').Split('/')[^1]).FirstOrDefault(name => name.Length > 0);

    /// <summary>
    /// A session parked to ask that nobody answered yet (ANSWER1c): the person's answer goes on at the driver's next look. Never Ask
    /// Daoris's conversation, which is answered in its own panel (ASKRAIL1).
    /// </summary>
    private static bool Parks(JsonObject session) =>
        Text(session, "state") == "awaiting-person" && Truthy(session, "answer") is null && !AskDaoris(session);

    /// <summary>Ask Daoris's conversation (HELP1a, D89), by the repository it is recorded in: no session row of Sessions' (ASKRAIL1).</summary>
    private static bool AskDaoris(JsonObject? session) => Text(session, "repository") == HelpRoom.Repository;

    /// <summary>A teammate's session, keyed by its machine's origin (D47): a local id never carries a separator.</summary>
    private static bool Elsewhere(string id) => id.IndexOf('/') > 0;

    /// <summary>Whether a repository can be asked: the host's answer (D70), or adoption where an older host gives none.</summary>
    private static bool CanBeAsked(JsonObject row) =>
        (row["addressable"] ?? row["adopted"]) is JsonValue value && value.TryGetValue<bool>(out var yes) && yes;

    /// <summary>A record's note whole, its parts beside the English, which the row words in the reader's language (LANG1b).</summary>
    private static JsonObject NoteOf(JsonObject record)
    {
        var note = new JsonObject();
        if (record.TryGetPropertyValue("note", out var english)) note["note"] = english?.DeepClone();
        if (record.TryGetPropertyValue("noteParts", out var parts)) note["parts"] = parts?.DeepClone();
        return note;
    }

    /// <summary>A row of <paramref name="kind"/>, with <c>workspace</c> where it is judged from a record a window's scope filters.</summary>
    private static JsonObject Row(string kind, string? id, string? since, string? workspace = null)
    {
        var row = new JsonObject { ["kind"] = kind, ["id"] = id, ["since"] = since };
        if (workspace is not null) row["workspace"] = workspace;
        return row;
    }

    /// <summary>The workspace a record of the service's belongs to, as a window's scope reads it: <c>default</c> where it names none.</summary>
    private static string Scope(JsonObject record) => Text(record, "workspace") ?? "default";

    /// <summary>Each record by its id, the first of an id winning, as the page's <c>find</c> did.</summary>
    private static Dictionary<string, JsonObject> First(IEnumerable<JsonObject> records)
    {
        var byId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (Text(record, "id") is { } id) byId.TryAdd(id, record);
        }

        return byId;
    }

    private static IEnumerable<JsonObject> Objects(JsonNode? node) => node is JsonArray items ? items.OfType<JsonObject>() : [];

    private static JsonArray Strings(IEnumerable<string?> values) => new([.. values.Select(value => (JsonNode?)value)]);

    private static string? Text(JsonObject? record, string field) =>
        record?[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A field's text where it is a non-empty string: what the page's truthiness read.</summary>
    private static string? Truthy(JsonObject? record, string field) => Text(record, field) is { Length: > 0 } text ? text : null;

    private static double? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    /// <summary>A value as the page's template wrote it into an id: a string as itself, anything else as its JSON.</summary>
    private static string Part(JsonNode? node) => node switch
    {
        null => "undefined",
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString(),
    };
}
