using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// Who needs the person: <i>What needs you</i>'s rows, the parks and the sessions the driver runs, judged once for every
/// window. Each row carries its kind, its id, since when it has waited and the values it is worded from; what it is called, its
/// sentences and the order the band draws are the page's.
/// </summary>
/// <remarks>
/// <para><b>No row starts anything to find out</b>: every input is a record a slice already holds or the look already
/// kept. A row whose wait no record says has a <c>since</c> of null, never the clock's now, which would move at every judgement.</para>
///
/// <para><b>The scope a window reads</b>: a row that is a session's, a quest's or an ask's carries that record's
/// <c>workspace</c>, however its wait reached the judge, the service's records or the driver's own lists (a review, an opinion, a
/// helper's ask, a hand-over ask, a held call, a held answer, a unit's clean-up), by which a window scoped to one circle filters, as its lists were. Only
/// what is the machine's carries none, and every window shows it: a terminal's press, a rule for the whole machine, a folder or an
/// account whose holds span workspaces, a unit no record owns; and a row whose record is not in hand, which no window may lose.</para>
/// </remarks>
public static class NeedsAPerson
{
    /// <summary>The intake states in which an ask is the harness's to answer, not yet the person's.</summary>
    private static readonly HashSet<string> IntakeBusy = new(StringComparer.Ordinal) { "queued", "starting", "working" };

    /// <summary>How a quest's and an ask's units are named (<see cref="QuestUnits.IdOf"/>, <see cref="AskUnits.IdOf"/>).</summary>
    private static readonly string QuestUnit = QuestUnits.IdOf(""), AskUnit = AskUnits.IdOf("");

    /// <summary>How the work inventory names a session's unit before a branch record follows it.</summary>
    private const string SessionUnit = "session:";

    /// <summary>What an integration's wait names on its row: which wait, its line, and the candidate, request, commit or unit it is of.</summary>
    private static readonly string[] IntegrationFields =
        ["wait", "repository", "line", "branch", "branches", "candidate", "request", "commit", "subject", "unit", "tip", "why", "state"];

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

        // The workspace of the session a row is, where its record is in hand; else none, and every window shows it.
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

        // Whom an ask in this workspace could be published to: what the host says can be asked, as the ask's page offers.
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
            // A refused receiver's sentence is the service's, verbatim; otherwise the page says what is proposed.
            if (Text(ask, "note") is { } refused) proposal["detail"] = refused;
            proposal["publishTo"] = Strings(Objects(ask["proposal"]).Select(match => Text(match, "repository")));
            proposal["choices"] = Strings(ReceiversIn(workspace));
            rows.Add(proposal);
        }

        // Every go-ahead still asked on an ask that is not closed: one row each, since each is its own act.
        foreach (var ask in facts.Asks.Where(ask => Text(ask, "state") != "Closed"))
        {
            foreach (var goAhead in Objects(ask["goAheads"]).Where(goAhead => Text(goAhead, "state") == "asked"))
            {
                var first = Objects(goAhead["asked"]).FirstOrDefault();
                var row = Row("go-ahead", $"{Text(ask, "id")}#{Part(goAhead["number"])}", Text(first, "at") ?? Text(ask, "updated"), Scope(ask));
                // Keyed by its exchange's id, the one obligation its askers' parks wait on too.
                row["obligation"] = $"go-ahead:{Text(ask, "id")}/{Part(goAhead["number"])}";
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

        // A done its departure holds for the person's yes, until they accept it. A set-up step its review holds waits for
        // their review instead, which no yes lifts, so it is the set-up's row and never this one.
        foreach (var quest in facts.Quests.Where(quest => quest["held"] is JsonValue held && held.TryGetValue<bool>(out var yes) && yes
                     && Truthy(quest, "accepted") is null && Text(quest, "hold") != "unreviewed"))
        {
            var row = Row("departure", Text(quest, "id"), Text(quest, "updated"), Scope(quest));
            row["where"] = Text(quest, "to");
            row["departed"] = Departed(quest);
            rows.Add(row);
        }

        // A set-up step whose newest set-up waits for the person's look: what it showed, and the set-up the
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

        // A second opinion that waits on the person, from the driver's own list, its wait whole.
        foreach (var wait in facts.Opinions)
        {
            var row = Row("opinion", Text(wait, "session"), Text(wait, "since"), Text(wait, "workspace") ?? SessionScope(Text(wait, "session")));
            row["where"] = Text(wait, "repository");
            row["opinion"] = wait.DeepClone();
            rows.Add(row);
        }

        // A run whose every wait is the person's Accept, one row by the session Sessions' list places for it in Needs you: never a
        // reader of its own. Ask Daoris's conversations are reached from its own panel, never from Sessions.
        foreach (var placed in facts.Groups.Where(placed => Text(placed, "group") == SessionGroup.NeedsYou && Text(placed, "reason") == SessionReason.Review))
        {
            var record = sessions.GetValueOrDefault(Text(placed, "session") ?? "");
            if (AskDaoris(record) || Text(placed, "repository") == HelpRoom.Repository) continue;
            if (Text(record, "quest") is { } quest && facts.StoppedQuests.Contains(quest)) continue;
            var row = Row("review", Text(placed, "session"), Text(record, "updated"), record is null ? null : Scope(record));
            row["where"] = Text(record, "repository") ?? "";
            row["placed"] = placed.DeepClone();
            rows.Add(row);
        }

        // A park the person answered goes on at the driver's next look: it no longer needs them. A parked intake whose ask is
        // live is the ask's row.
        var standsFor = live.Select(ask => Truthy(ask, "intake")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        // Nor a park a supervisor's held or released decision answers for, nor a park on a go-ahead its session asked,
        // or on its agent's ask to hand it over, still asked: the person's one decision is that row's, which holds the
        // work, so the bell counts the obligation once.
        var decided = OwnAsks(facts);
        foreach (var session in facts.Sessions.Where(session =>
                     Parks(session) && !standsFor.Contains(Text(session, "id") ?? "") && !facts.Supervised.Contains(Text(session, "id") ?? "")
                     && !decided.Contains(Text(session, "id") ?? "")))
        {
            var row = Row("parked", Text(session, "id"), Text(session, "updated"), Scope(session));
            row["where"] = Text(session, "repository");
            row["note"] = NoteOf(session);
            rows.Add(row);
        }

        // A quest parked on its failed sessions here: the planner's verdict, since its last session ended, else its last move.
        foreach (var consideration in facts.Considered.Where(consideration => Text(consideration, "verdict") == "Exhausted"))
        {
            var held = quests.GetValueOrDefault(Text(consideration, "quest") ?? "");
            var row = Row("parked-quest", Text(consideration, "quest"), Text(consideration, "since") ?? Text(held, "updated"), held is null ? null : Scope(held));
            row["where"] = Text(consideration, "repository");
            row["sitting"] = consideration.DeepClone();
            rows.Add(row);
        }

        // A quest its role's policy holds, for an ask or for the bound of moves, waits on the person's
        // yes (Try again, one move). The planner's typed verdict, so a plan with no look says it as the look does.
        foreach (var consideration in facts.Considered.Where(consideration => Text(consideration, "verdict") == "PolicyHeld"))
        {
            var held = quests.GetValueOrDefault(Text(consideration, "quest") ?? "");
            var row = Row("policy-hold", Text(consideration, "quest"), Text(consideration, "since") ?? Text(held, "updated"), held is null ? null : Scope(held));
            row["where"] = Text(consideration, "repository");
            row["sitting"] = consideration.DeepClone();
            rows.Add(row);
        }

        rows.AddRange(UnclosedQuestRows.Rows(facts, quests));

        // A quest the steward's stop holds waits on a Try again, the steward's or the owner's: *Stopped by the
        // steward*, the one act of the steward's that can leave something waiting on the owner. Read from the planner's verdict by its code.
        foreach (var consideration in facts.Considered.Where(consideration => Text(consideration, "verdict") == "Stopped"
                     && consideration["said"] is JsonObject said && Text(said, "code") == SittingFact.Steward))
        {
            var held = quests.GetValueOrDefault(Text(consideration, "quest") ?? "");
            var row = Row("steward-stopped", Text(consideration, "quest"), Text(consideration, "since") ?? Text(held, "updated"), held is null ? null : Scope(held));
            row["where"] = Text(consideration, "repository");
            row["session"] = Text((JsonObject)consideration["said"]!, "session");
            row["sitting"] = consideration.DeepClone();
            rows.Add(row);
        }

        // A run whose work.requests holds it at a declined request waits on the person's *Pass over…*, one row per
        // run, keyed by it, since the decline. Its workspace is the run's own. Read from the runtime's last look, never stored on the node.
        foreach (var hold in facts.Requests)
        {
            var row = Row("request-declined", Text(hold, "run"), Text(hold, "since"), Text(hold, "workspace") ?? "default");
            row["where"] = Text(hold, "repository");
            var declined = new JsonObject
            {
                ["node"] = Text(hold, "node"), ["quest"] = Text(hold, "quest"), ["to"] = Text(hold, "to"), ["work"] = Text(hold, "work"),
            };
            // The run's own request, whose acts are *Ask again…* and *Go on without it…*; written only where true, so the work's stays as it is.
            if (hold["request"] is JsonValue asked && asked.TryGetValue<bool>(out var own) && own) declined["request"] = true;
            row["declined"] = declined;
            rows.Add(row);
        }

        rows.AddRange(HumanTaskRows.Of(facts.Tasks));

        // A candidate the policy held for the owner's decide, a superseded commit awaiting
        // its answer, and a landing whose integration waits for the owner's card, one row each in its repository's workspace, its door the
        // repository's Integration tab, where each is pressed. A held candidate holds its line's next, and a card's landing its unit's flow.
        // A declaration owed acceptance is the fourth: its door opens the repository's set-up page, where the person reads it and accepts;
        // nothing it holds runs, so it stands for nothing.
        foreach (var wait in facts.Integration)
        {
            var row = Row("integration", Text(wait, "id"), Text(wait, "since"), Text(wait, "workspace") ?? "default");
            row["where"] = Text(wait, "repository");
            if (Text(wait, "wait") is "decide" or "card") row["stands"] = true;
            var named = new JsonObject();
            foreach (var field in IntegrationFields)
            {
                if (wait[field] is { } value) named[field] = value.DeepClone();
            }

            row["integration"] = named;
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

        // An agent's proposal to widen the rules waits for the person's word: the proposal whole, why in its session's words.
        // Its workspace is the one its change reaches: a workspace's rule that workspace's, a repository's the proposing session's. A
        // rule for the whole machine carries none, deliberately: it reaches every workspace, so every window shows it.
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

﻿        rows.AddRange(AttentionSessionRows.Rows(facts, sessions, SessionScope));
        rows.AddRange(AttentionMachineRows.Rows(facts, UnitScope));

        // The starts waiting on their accounts, and the signed-out accounts no waiting start names.
        rows.AddRange(AccountAttention.Rows(facts));
        return rows;
    }

    /// <summary>
    /// What parked on the person, which the page keeps out of Sessions' running mark (a place counts
    /// the rows), each by a key of its own: this machine's sessions parked to ask that
    /// nobody answered, a parked intake among them, and each quest parked on its failed sessions, each with its workspace for the window's
    /// scope; a parked quest whose record is not in hand says none, and every window counts it. A teammate's park waits on them.
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

        // And a quest its role's policy holds for the person's yes, by its own key beside the parked quests'.
        foreach (var quest in facts.Considered.Where(consideration => Text(consideration, "verdict") == "PolicyHeld").Select(each => Text(each, "quest")).Distinct())
        {
            var held = new JsonObject { ["kind"] = "policy-hold" };
            if (quests.GetValueOrDefault(quest ?? "") is { } record) held["workspace"] = Scope(record);
            counted.Add(($"policy:{quest}", held));
        }

        counted.AddRange(UnclosedQuestRows.Counted(facts, quests));
        return counted;
    }

    /// <summary>
    /// The sessions the driver runs on this machine, each by a key of its own with its workspace for the window's
    /// scope: what the status bar says, and, less the parks, Sessions' running mark. One count, the cap's: an active record of this
    /// machine's that takes a place under the cap (<see cref="ActiveSessions.HoldsSlot(string?, string?, string?, string?, string?)"/>),
    /// a parked one among them, never a conversation the person holds, Ask Daoris's or a chat. A teammate's runs on its own machine.
    /// </summary>
    public static IReadOnlyList<(string Key, JsonObject Value)> Working(AttentionFacts facts) =>
    [
        .. facts.Sessions
            .Where(session => Text(session, "id") is { Length: > 0 } id && !Elsewhere(id) && Active.Contains(Text(session, "state") ?? "")
                && ActiveSessions.HoldsSlot(
                    Text(session, "kind"), Text(session, "repository"), Text(session, "ask"), Text(session, "serves"), Text(session, "opinion"),
                    Text(session, "decides")))
            .Select(session => ($"session:{Text(session, "id")}", new JsonObject { ["workspace"] = Text(session, "workspace") ?? "default" })),
    ];

    /// <summary>
    /// The states in which a record still holds its repository, the service's <c>Session.Active</c>: what the planner's snapshot lists as
    /// active, so the count is made over the records it counts.
    /// </summary>
    private static readonly HashSet<string> Active = new(StringComparer.Ordinal) { "queued", "starting", "working", "awaiting-person" };

    /// <summary>
    /// One row per folder in one file: two quests held on one untrusted tree are one grant, waiting since the oldest thing it holds.
    /// Its identity is the file AND the folder: the folder alone gave one folder held in two accounts' files two rows with one key.
    /// </summary>
    private static IEnumerable<JsonObject> Folders(AttentionFacts facts, Dictionary<string, JsonObject> quests, Dictionary<string, JsonObject> asks)
    {
        var folders = new List<JsonObject>();
        var seen = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        // Each folder's workspace while every hold's record names the same one; null once one is not in hand or two differ.
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
            // Its file the own sign-in's: the page draws no press, and TRUST_FOLDER refuses one.
            if (hold["own"]?.GetValueKind() == JsonValueKind.True) row["trust"]!["own"] = true;
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
    /// A go-ahead nothing on its ask could still use: no session that asked it, no intake of the ask and no
    /// session of a quest the ask asked is queued, starting, working or parked, and no quest the ask asked is open, taken or held for the
    /// person's yes, whose next session would be handed its answer. It is then stale since the last thing on its ask moved: the newest of
    /// those records' moves and its own askings, so its age says how long nothing has needed it. Null while anything could still use it.
    /// </summary>
    /// <remarks>
    /// A record not in hand is not running: the sessions slice holds every record the service answers, so one it lacks is forgotten. What
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
    /// acceptance has let on yet, as the page counts a held quest among those not closed.
    /// </summary>
    private static bool StillOpen(JsonObject quest) =>
        Text(quest, "status") is "Open" or "Taken"
        || (quest["held"] is JsonValue held && held.TryGetValue<bool>(out var yes) && yes && Truthy(quest, "accepted") is null);

    /// <summary>The newest of <paramref name="moments"/> as it is written, by the moment it names; null where none reads.</summary>
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
    /// A set-up step's newest set-up where it waits for the person's look (the page's <c>waitsForLook</c>): the step names
    /// an environment, it stands shown, and its newest set-up is named whole; null otherwise.
    /// </summary>
    private static JsonObject? Looked(JsonObject quest)
    {
        if (Truthy(quest, "setUpIn") is null) return null;
        var newest = quest["setUps"] is JsonArray setUps && setUps.Count > 0 ? setUps[^1] as JsonObject : null;
        return StepState(quest, newest) == "shown" && Reference(newest) is not null ? newest : null;
    }

    /// <summary>Where a set-up step stands, from its own record, as the page's <c>stepState</c> reads it.</summary>
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

    /// <summary>A set-up's reference, whole, or null where the record carries half of one or none.</summary>
    private static (string Machine, double Sequence)? Reference(JsonObject? setUp) =>
        Truthy(setUp, "machine") is { } machine && setUp!["sequence"] is JsonValue sequence && sequence.TryGetValue<double>(out var number)
            ? (machine, number)
            : null;

    /// <summary>
    /// A session parked to ask that nobody answered yet: the person's answer, read from its keyed words, goes on at
    /// the driver's next look. Never Ask Daoris's conversation, which is answered in its own panel.
    /// </summary>
    private static bool Parks(JsonObject session) =>
        Text(session, "state") == "awaiting-person" && SessionRecords.PersonsSaid(session) is null && !AskDaoris(session);

    /// <summary>Ask Daoris's conversation, by the repository it is recorded in: no session row of Sessions'.</summary>
    private static bool AskDaoris(JsonObject? session) => Text(session, "repository") == HelpRoom.Repository;

    /// <summary>A teammate's session, keyed by its machine's origin: a local id never carries a separator.</summary>
    private static bool Elsewhere(string id) => id.IndexOf('/') > 0;

    /// <summary>Whether a repository can be asked: the host's answer, or adoption where an older host gives none.</summary>
    private static bool CanBeAsked(JsonObject row) =>
        (row["addressable"] ?? row["adopted"]) is JsonValue value && value.TryGetValue<bool>(out var yes) && yes;

    /// <summary>A record's note whole, its parts beside the English, which the row words in the reader's language.</summary>
    private static JsonObject NoteOf(JsonObject record)
    {
        var note = new JsonObject();
        if (record.TryGetPropertyValue("note", out var english)) note["note"] = english?.DeepClone();
        if (record.TryGetPropertyValue("noteParts", out var parts)) note["parts"] = parts?.DeepClone();
        return note;
    }

    /// <summary>
    /// The sessions whose park is a row of its own request: one that asked a go-ahead still asked on an ask not closed, or whose
    /// agent's ask to hand it over waits on the person. Their parks wait on that one decision.
    /// </summary>
    private static HashSet<string> OwnAsks(AttentionFacts facts)
    {
        var sessions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var goAhead in facts.Asks.Where(ask => Text(ask, "state") != "Closed").SelectMany(ask => Objects(ask["goAheads"]))
                     .Where(goAhead => Text(goAhead, "state") == "asked"))
        {
            foreach (var asked in Objects(goAhead["asked"])) if (Text(asked, "session") is { } session) sessions.Add(session);
        }

        foreach (var wait in facts.HandOvers) if (Text(wait, "session") is { } session) sessions.Add(session);
        return sessions;
    }

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

    private static double? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;
}
