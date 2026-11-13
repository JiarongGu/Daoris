using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Who needs the person, as every window holds it: <i>What needs you</i>'s rows and the Sessions
/// badge's count, judged once per wake in .NET (<see cref="NeedsAPerson"/>) from what the other slices and the driver's look already
/// hold in memory, and pushed by row, so no window judges them and no tick reads the band's inputs again.
/// </summary>
/// <remarks>
/// <para><b>Three keyed partitions.</b> <see cref="Rows"/>, each row under its kind and id as the band keys it, its wait and the values
/// the page words it from, which the bell and each place of the activity bar count; <see cref="Sessions"/>, each session parked
/// on the person and each quest parked on its failures; and <see cref="Working"/>, each session the driver runs, which the status bar
/// counts and, less the parks, lights Sessions' running mark. A row's move pushes that
/// row alone. A row holds no place in the list: the page orders by group, then by wait, then by kind.</para>
///
/// <para><b>Its inputs, from memory.</b> The sessions, the quests, the asks, the groups and the work as their slices last judged them
/// (<see cref="StateSlice.Snapshot"/>); the registry as the machine's facts hold it; and what the driver's look kept: the trust holds,
/// the considerations and the account waits as the tick hands them, the second opinions judged, the rule proposals, the helpers' asks,
/// the moves back to the person the service refused, the agents' asks to hand over, the held tool calls and the answers the answers-alone bound holds (off the home's marks); and for the accounts' rows, the roster's last readings as <c>HARNESSES</c> answers them (asking no account)
/// and how each agent's accounts are used as the accounts slice holds it. Not judged until every slice it reads has judged and
/// the registry has landed: a band drawn from half its inputs would say nothing waits where something does. The look's own
/// lists are empty before its first look.</para>
///
/// <para><b>Its wakes</b>, each only asking. Each input slice's push (<see cref="StateSlice.Pushed"/>), the accounts' among them, the
/// sessions' only where what the judge reads of them moved (<see cref="SessionsRead"/>), so a working session's every word judges nothing
/// here; each look of the driver (the loop wakes it beside the driver's state); a held tool call's start and end
/// (<see cref="HeldPermissions.Changed"/>); a stretch below the free-space floor said or ended (<see cref="SpaceFloors.Changed"/>); the registry landing in the machine's facts; a press declared to the rules, the sessions, the
/// driver or the roster (a proposal answered, a helper's yes or no, a reading); a harness action's end; the service coming up; a
/// terminal's press for the window asked, shown or ended (<see cref="PipeConfirmations.Event"/>); and the
/// 15 s period, which reads a helper's or a hand-over's ask a terminal's driver wrote and an account a terminal signed in.</para>
///
/// <para><b>Desktop-only.</b> A trust hold names a folder and the agent's own file, machine paths this machine's windows alone read
/// </para>
/// </remarks>
public static class AttentionStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "attention";

    /// <summary>The rows' partition: each row of <i>What needs you</i>, keyed by its kind and id (the page's <c>attentionKey</c>).</summary>
    public const string Rows = "rows";

    /// <summary>
    /// Each session parked to ask and each quest parked on its failed sessions, by a key of its own: the page keeps the
    /// parks out of Sessions' running mark by their keys; what a place counts is the rows.
    /// </summary>
    public const string Sessions = "sessions";

    /// <summary>
    /// The sessions the driver runs, each by its key, the cap's own count (<see cref="NeedsAPerson.Working"/>):
    /// what the status bar says, and, less the parks of <see cref="Sessions"/>, Sessions' running mark.
    /// </summary>
    public const string Working = "working";

    /// <summary>The slices it is judged from, each read from memory: none judged, nothing judged here.</summary>
    public static readonly IReadOnlyList<string> Inputs =
        [SessionsStateSlice.Name, QuestsStateSlice.Name, AsksStateSlice.Name, GroupsStateSlice.Name, WorkStateSlice.Name];

    /// <summary>
    /// The allow-list: each shape a partition carries, and the fields of it pushed. <c>row</c> is a row, <c>waiting</c> what the badge
    /// marks, <c>working</c> what it counts; the rest are what they hold (<see cref="Holds"/>), each picked as its source answers it.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["row"] =
        [
            "kind", "id", "since", "workspace", "where", "note", "detail", "publishTo", "choices", "ask", "question", "number", "goAhead", "session",
            "departed", "setUp", "local", "environment", "shows", "opinion", "placed", "sitting", "trust", "quest", "proposal", "title",
            "permission", "blocked", "held", "unreadable", "wholeWorkspace", "holding", "read", "account", "answer", "ended",
            "confirmation", "agent", "stands", "stale", "space", "git", "quiet", "declined", "memory", "integration",
            "convert", "gitAsk", "obligation", "task", "secondCheck",
        ],
        ["waiting"] = ["kind", "workspace"],
        ["working"] = ["workspace"],
        ["note"] = ["note", "parts"],
        ["goAhead"] = ["kind", "on", "act"],
        // A go-ahead nothing on its ask could still use, since the last thing on its ask moved.
        ["stale"] = ["since"],
        ["departed"] = ["count", "number", "reason"],
        ["setUp"] = ["machine", "sequence"],
        ["opinion"] = ["session", "quest", "repository", "workspace", "since", "auto", "opinion"],
        ["gate"] =
        [
            "state", "holds", "required", "opinion", "reviewer", "product", "maker", "label", "reviewing", "findings", "disputes", "since",
            "code", "until", "later", "person", "answers", "says",
        ],
        ["placed"] = ["session", "group", "state", "reason", "shown", "archived", "teammate", "strikes", "awaits", "awaitsOf", "work", "holdsQuest", "pausedBy", "holds", "waits"],
        ["placedWaits"] = ["run", "node", "why", "since"],
        ["placedWork"] = ["commits", "uncommitted", "branch"],
        ["pausedBy"] = ["scope", "id"],
        ["wordsHeld"] = ["why", "reason", "repository", "until", "account", "accountName"],
        ["sitting"] = ["quest", "repository", "verdict", "reason", "strikes", "since"],
        // `own`, a hold on the own sign-in's file, which the page draws with no grant.
        ["trust"] = ["folder", "trustFile", "own"],
        ["proposal"] =
        [
            "id", "state", "action", "scope", "name", "list", "rule", "default", "on", "why", "session", "ask", "proposed", "settled",
            "settledBy", "note",
        ],
        ["permission"] = ["session", "request", "call", "title", "kind", "choices", "since", "until"],
        ["choice"] = ["id", "name", "kind"],
        ["answer"] = ["session", "agent", "words", "since"],
        ["blocked"] = ["unknown", "entries"],
        ["entry"] = ["kind", "resource", "detail", "code", "facts"],
        ["attempt"] = WorkStateSlice.Fields["attempt"],
        ["verification"] = WorkStateSlice.Fields["verification"],
        ["unreadable"] = WorkStateSlice.Fields["unreadable"],
        ["graphEnd"] = WorkStateSlice.Fields["graphEnd"],
        ["holding"] = ["quests", "asks"],
        ["account"] = ["agent", "product", "door", "signsIn", "named", "noAccount"],
        ["named"] = ["id", "name", "state", "read", "until"],
        ["confirmation"] = ["module", "route", "asker", "expires", "shownAt"],
        ["asker"] = ["process", "pid"],
        // A volume still below its free-space floor once the floor's acts ran.
        ["space"] = ["root", "free", "total", "floor", "freed", "could"],
        // The old records every part's kept census holds, and their count by part.
        ["convert"] = ["records", "parts"],
        // A held group of git remotes that cannot authenticate. Its way and what serves it; the move's row
        // (`no-identity`) names every host and lists apart the repositories whose own configuration names authentication.
        ["git"] = ["host", "class", "holds", "remote", "repositories", "words", "way", "served", "identity", "identityId", "keyPage", "hosts", "ownInput"],
        // A git go-ahead a door holds while its act waits.
        ["gitAsk"] = ["repository", "remote", "host", "class", "session", "gone"],
        // A quiet session brought to the person, or a busy one nothing is heard from.
        ["quiet"] = ["minutes", "busy", "intake", "words", "heavy"],
        // A run held at a declined request, its node and the quest declined.
        ["declined"] = ["node", "quest", "to", "work", "request"],
        ["secondCheck"] = ["session", "range", "index", "says", "certainty", "call", "at", "reader", "independence"],
        // A run waiting on a person's task, its node and the presses it waits for.
        ["task"] = ["run", "node", "acts", "work"],
        // What memory waits on the person for one session: ends and replacements to approve, a stopped session's staged rows.
        ["memory"] = ["ends", "replacements", "staged"],
        ["memoryEnd"] = ["op", "repository", "target", "revision", "reason"],
        // An integration's wait on the owner: which (decide, superseded, card, declaration), its line and what it names; a
        // declaration's `tip` is its blob and its `state` unaccepted or changed.
        ["integration"] = ["wait", "repository", "line", "branch", "branches", "candidate", "request", "commit", "subject", "unit", "tip", "why", "state"],
    };

    /// <summary>Which shape a field holds, as <c>shape.field</c>, an object of it or a list of them; every other field is a value.</summary>
    public static readonly IReadOnlyDictionary<string, string> Holds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["row.note"] = "note",
        ["row.goAhead"] = "goAhead",
        ["row.stale"] = "stale",
        ["row.departed"] = "departed",
        ["row.setUp"] = "setUp",
        ["row.opinion"] = "opinion",
        ["opinion.opinion"] = "gate",
        ["row.placed"] = "placed",
        ["placed.work"] = "placedWork",
        ["placed.pausedBy"] = "pausedBy",
        ["placed.holds"] = "wordsHeld",
        ["placed.waits"] = "placedWaits",
        ["row.sitting"] = "sitting",
        ["row.trust"] = "trust",
        ["row.proposal"] = "proposal",
        ["row.permission"] = "permission",
        ["permission.choices"] = "choice",
        ["row.answer"] = "answer",
        ["row.blocked"] = "blocked",
        ["blocked.entries"] = "entry",
        ["row.held"] = "attempt",
        ["attempt.verification"] = "verification",
        ["row.unreadable"] = "unreadable",
        ["row.ended"] = "graphEnd",
        ["row.holding"] = "holding",
        ["row.account"] = "account",
        ["row.quiet"] = "quiet",
        ["account.named"] = "named",
        ["row.confirmation"] = "confirmation",
        ["confirmation.asker"] = "asker",
        ["row.space"] = "space",
        ["row.git"] = "git",
        ["row.gitAsk"] = "gitAsk",
        ["row.convert"] = "convert",
        ["row.declined"] = "declined",
        ["row.task"] = "task",
        ["row.secondCheck"] = "secondCheck",
        ["row.memory"] = "memory",
        ["memory.end"] = "memoryEnd",
        ["row.integration"] = "integration",
    };

    /// <summary>
    /// What wakes it on the kit's bus: a press declared to the rules (a proposal answered), the sessions or the driver (a helper's yes or
    /// no), and the service coming up. Its inputs' pushes, each look and a held call's move wake it too (<see cref="Of(DriverLoop, IEventBus)"/>).
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed
            && changed.Scopes.Any(scope => scope is "rules" or "sessions" or "driver" or "harnesses")),
        // A harness action's end: a sign-in makes an account and moves its reading.
        new("HARNESS_ENDED"),
        new("DRIVER_READY"),
        // A terminal's press asked, shown or ended, as every window is told it.
        new(PipeConfirmations.Event),
        // What memory waits on the person, as the service is last read.
        new(MemoryWaits.Event),
    ];

    /// <summary>
    /// The slice the shell's loop owns, judged from what <paramref name="loop"/> holds in memory; a held tool call's row first seen is
    /// announced while the loop's notify switch is on.
    /// </summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        Of(ct => FactsAsync(loop, ct), bus,
            [
                wake => HearSlices(loop.Slices, wake), wake => HearHolds(HeldPermissions.Of(loop.Events), wake), wake => HearRegistry(loop.Facts, wake),
                wake => HearFloors(SpaceFloors.Shared, wake), wake => HearGitHolds(loop.Home, wake), wake => HearGitAsks(loop.Home, wake),
                wake => HearQuiet(SessionWatch.Of(loop.Events), wake), wake => AttentionSecondCheckRows.Hear(loop.Home, wake),
            ],
            loop.Log, new HeldCallAnnouncements(bus, () => NotifyRule.Tells(NewsKind.HeldCall, loop.Notifying())));

    /// <summary>The slice over <paramref name="facts"/>, read at each judgement, woken where <paramref name="hears"/> hear a move.</summary>
    public static StateSlice Of(
        Func<AttentionFacts?> facts, IEventBus bus, IReadOnlyList<Func<Action, IDisposable>> hears, MachineLog? log = null,
        HeldCallAnnouncements? held = null) =>
        Of(_ => Task.FromResult(facts()), bus, hears, log, held);

    /// <summary>
    /// The slice over what <paramref name="facts"/> reads at each judgement; <paramref name="held"/>, where named, told each judgement, so
    /// a held tool call is announced where its row is first seen.
    /// </summary>
    public static StateSlice Of(
        Func<CancellationToken, Task<AttentionFacts?>> facts, IEventBus bus, IReadOnlyList<Func<Action, IDisposable>> hears, MachineLog? log = null,
        HeldCallAnnouncements? held = null) =>
        new(Name, bus, async ct =>
        {
            var judged = Judge(await facts(ct).ConfigureAwait(false));
            if (judged is not null) held?.Judged(judged);
            return judged;
        })
        {
            Keyed = true,
            WakesOn = WakesOn,
            Hears = hears,
            // A file unreadable: the windows keep what is judged, and the next wake judges again.
            Failed = error => log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>
    /// One judgement: the rows, the parks and the sessions the driver runs, each keyed and picked to the allow-list; null where an input is never judged,
    /// which judges nothing. Judged empty, each partition is pushed empty, never gone.
    /// </summary>
    public static IReadOnlyList<SliceJudged>? Judge(AttentionFacts? facts)
    {
        if (facts is null) return null;
        var rows = new JsonObject();
        foreach (var row in NeedsAPerson.Rows(facts))
        {
            var key = $"{Json.Text(row, "kind")}:{Json.Text(row, "id")}";
            // Two rows of one key would be one row on the page, as the band keys them: the first stands.
            if (!rows.ContainsKey(key)) rows[key] = Picked(row);
        }

        var counted = new JsonObject();
        foreach (var (key, value) in NeedsAPerson.WaitingInSessions(facts)) counted[key] = Pick(value, "waiting");
        var working = new JsonObject();
        foreach (var (key, value) in NeedsAPerson.Working(facts)) working[key] = Pick(value, "working");
        return [new SliceJudged(Rows, rows), new SliceJudged(Sessions, counted), new SliceJudged(Working, working)];
    }

    /// <summary>A row as pushed: its fields of the allow-list, each shape it holds picked to its own.</summary>
    public static JsonObject Picked(JsonObject row) => (JsonObject)Pick(row, "row")!;

    private static JsonNode? Pick(JsonNode? node, string shape) => node switch
    {
        JsonObject one => PickObject(one, shape),
        JsonArray many => new JsonArray([.. many.Select(item => Pick(item, shape))]),
        _ => node?.DeepClone(),
    };

    private static JsonObject PickObject(JsonObject from, string shape)
    {
        var picked = new JsonObject();
        foreach (var field in Fields[shape])
        {
            if (!from.TryGetPropertyValue(field, out var value)) continue;
            picked[field] = Holds.TryGetValue($"{shape}.{field}", out var held) ? Pick(value, held) : value?.DeepClone();
        }

        return picked;
    }

    /// <summary>
    /// Everything the judge reads, from <paramref name="loop"/>'s memory and the home's files the routes read; null until every input
    /// slice has judged and the registry has landed. The roster is its last readings, as <c>HARNESSES</c> answers them, asking no
    /// account.
    /// </summary>
    public static async Task<AttentionFacts?> FactsAsync(DriverLoop loop, CancellationToken ct)
    {
        if (Gather(loop.Slices, loop.Facts.Current.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole)?.Snapshot.Repositories) is not { } facts)
        {
            return null;
        }

        var roster = await loop.Harnesses.RosterAsync(DriverConfig.Load(loop.ConfigPath), refresh: false, ct).ConfigureAwait(false);
        return facts with
        {
            Untrusted = Written(loop.Trust.Latest),
            Considered = Written(loop.Considered ?? []),
            Proposals = Written(RulesRoutes.Proposals(loop.Home)),
            Opinions = Written(TreesRoutes.OpinionWaits(loop.Judged.Latest)),
            Helpers = Written(HelperWaits(loop.Home)),
            HandOvers = Written(HandOverWaits(loop.Home)),
            // Each move back to the person the service refused, from the home's helpers folder.
            Backs = Written(BackWaits(loop.Home)),
            Permissions = Written(PermissionWaits(HeldPermissions.Of(loop.Events))),
            Answers = Written(AnswerWaits(loop.Home)),
            // Each session whose memory changes wait on the person, as the service is last read.
            Memory = [.. loop.MemoryWaits.Now().Select(each => JsonNode.Parse(each.GetRawText())!.AsObject())],
            Doors = Written(AgentsRoutes.Doors(loop.Harnesses, roster)),
            Use = Use(loop.Slices),
            Waits = Written(loop.Look.Waits.Select(DriverLoop.TickWait)),
            // What a terminal sent that waits for the window, from the pipe's memory; none where this shell serves no pipe.
            Confirmations = Written(ConfirmationWaits(loop.Pipe?.Confirmations)),
            // Each volume the look's floor pass said is still below its floor, from the process's memory.
            Space = Written(SpaceWaits(SpaceFloors.Shared, loop.Home)),
            // Each remote held since it could not authenticate, from the home's run/git/held.json.
            Git = Written(GitWaits(loop.Home)),
            // Each git go-ahead a door holds, from the home's run/git/asks/.
            GitAsks = Written(GitAskWaits(loop.Home)),
            // Each run held at a declined request, from the node runtime's last look and its node records.
            Requests = Written(loop.RequestHolds.Select(hold => new
            {
                hold.Run, hold.Node, hold.Repository, hold.Workspace, hold.Quest, hold.To, hold.Work, hold.Request, Since = Moments.Write(hold.Since),
            })),
            // Each run waiting on a person's task, from the node runtime's last look and its node records.
            Tasks = Written(loop.HumanTasks),
            // The integration's waits on the owner, from the home's integration record and the node runtime's last look.
            Integration = Written(IntegrationWaits(loop.Home, loop.CardWaits, loop.DeclarationsOwed)),
            // Each session the watcher brings to the person, from its memory.
            Quiet = Written(QuietWaits(SessionWatch.Of(loop.Events), loop.Processes)),
            // The parks a supervisor's held or released decision answers for, from the decisions' memory.
            Supervised = loop.Supervisor.Answering(),
            StoppedQuests = StoppedRuns.Quests(loop.Home),
            SecondCheck = Written(AttentionSecondCheckRows.Unseen(loop.Home)),
        };
    }

    /// <summary>
    /// How each agent's accounts are used, as <c>ACCOUNTS</c> answers it (<c>{agents}</c>), from the accounts slice's memory, a partition
    /// per agent; null where it has not judged, and the look's waits alone say the account rows.
    /// </summary>
    public static JsonObject? Use(StateSlices slices) =>
        Ready(slices.Find(AccountsStateSlice.Name)) is { } snapshot
            ? new JsonObject { ["agents"] = new JsonArray([.. snapshot.Partitions.Select(each => Node(each.Data)?.DeepClone())]) }
            : null;

    /// <summary>
    /// The inputs the other slices hold, each as last judged: the records of the sessions, the quests and the asks, the groups' rows, the
    /// work's units and held attempts; and the registry. Null while any of them is never judged.
    /// </summary>
    public static AttentionFacts? Gather(StateSlices slices, IReadOnlyList<RepoView>? registry)
    {
        if (registry is null) return null;
        var sessions = Records(slices.Find(SessionsStateSlice.Name));
        var quests = Records(slices.Find(QuestsStateSlice.Name));
        var asks = Records(slices.Find(AsksStateSlice.Name));
        var groups = Ready(slices.Find(GroupsStateSlice.Name)) is { } placed
            ? placed.Partitions.Where(each => each.Partition == GroupsStateSlice.Rows).SelectMany(each => Values(Node(each.Data))).ToList()
            : null;
        var work = Ready(slices.Find(WorkStateSlice.Name));
        if (sessions is null || quests is null || asks is null || groups is null || work is null) return null;

        var units = new List<JsonObject>();
        var held = new List<JsonObject>();
        var unreadable = new List<JsonObject>();
        var ends = new List<JsonObject>();
        foreach (var part in work.Partitions)
        {
            var data = Node(part.Data) as JsonObject;
            if (part.Partition == WorkStateSlice.Held)
            {
                held.AddRange(Items(data?["held"]));
                unreadable.AddRange(Items(data?["unreadable"]));
                ends.AddRange(Items(data?["ends"]));
            }
            else units.AddRange(Items(data?["units"]));
        }

        return new AttentionFacts
        {
            Sessions = sessions, Quests = quests, Asks = asks, Groups = groups, Works = units, Held = held, Unreadable = unreadable, Ends = ends,
            Registry = [.. registry.Select(Registration)],
        };
    }

    /// <summary>A registry row as the page's <c>Registration</c> reads it: the host's own judgement of who can be asked.</summary>
    private static JsonObject Registration(RepoView view) => new()
    {
        ["repository"] = view.Repository,
        ["adopted"] = view.Adopted,
        ["addressable"] = view.Adopted || !string.IsNullOrWhiteSpace(view.Root),
        ["workspace"] = view.Workspace,
    };

    /// <summary>The agents' asks for a helper that wait on the person (<see cref="AttentionWaits.HelperWaits"/>).</summary>
    public static IReadOnlyList<object> HelperWaits(string home) => AttentionWaits.HelperWaits(home);

    /// <summary>The sessions whose move back to the person the service refused (<see cref="AttentionWaits.BackWaits"/>).</summary>
    public static IReadOnlyList<object> BackWaits(string home) => AttentionWaits.BackWaits(home);

    /// <summary>The agents' asks to hand their sessions over (<see cref="AttentionWaits.HandOverWaits"/>).</summary>
    public static IReadOnlyList<object> HandOverWaits(string home) => AttentionWaits.HandOverWaits(home);

    /// <summary>What the driven sessions hold for the person under asking (<see cref="AttentionWaits.PermissionWaits"/>).</summary>
    public static IReadOnlyList<object> PermissionWaits(HeldPermissions holds) => AttentionWaits.PermissionWaits(holds);

    /// <summary>What a terminal sent that waits for the window's Confirm (<see cref="AttentionWaits.ConfirmationWaits"/>).</summary>
    public static IReadOnlyList<object> ConfirmationWaits(PipeConfirmations? confirmations) => AttentionWaits.ConfirmationWaits(confirmations);

    /// <summary>Each volume still below its free-space floor (<see cref="AttentionWaits.SpaceWaits"/>).</summary>
    public static IReadOnlyList<object> SpaceWaits(SpaceFloors floors, string home) => AttentionWaits.SpaceWaits(floors, home);

    /// <summary>The answers the answers-alone bound holds for the person (<see cref="AttentionWaits.AnswerWaits"/>).</summary>
    public static IReadOnlyList<object> AnswerWaits(string home) => AttentionWaits.AnswerWaits(home);

    /// <summary>
    /// What the judge reads of the session records, as one string: each record's id, state, the person's words waiting (its
    /// keyed <c>said</c>), repository and workspace,
    /// its kind and the marks the cap's count reads, and, where it is not busy working, when it moved and its note. A working
    /// session's record is written at each word and moves its time alone, which no row reads; its park moves its state.
    /// </summary>
    public static string SessionsRead(IEnumerable<JsonObject> records) =>
        string.Join("\n", records.Select(record =>
        {
            var state = Json.Text(record, "state");
            var busy = state is "queued" or "starting" or "working";
            return string.Join("\t",
                Json.Text(record, "id"), state, SessionRecords.PersonsSaid(record), Json.Text(record, "repository"), Json.Text(record, "workspace"),
                Json.Text(record, "kind"), Json.Text(record, "ask"), Json.Text(record, "serves"), Json.Text(record, "opinion"),
                busy ? "" : Json.Text(record, "updated"), busy ? "" : record["note"]?.ToJsonString(), busy ? "" : record["noteParts"]?.ToJsonString());
        }));

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where an input slice pushed a move: any of the quests', the asks', the groups' or the
    /// work's, and the sessions' only where what the judge reads of them moved. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearSlices(StateSlices slices, Action wake)
    {
        var stops = new List<Action>();
        // And the accounts', which the account rows read where it has judged.
        foreach (var name in Inputs.Append(AccountsStateSlice.Name))
        {
            if (slices.Find(name) is not { } slice) continue;
            var heard = name == SessionsStateSlice.Name ? OnMove(() => SessionsRead(Records(slice) ?? []), wake) : wake;
            slice.Pushed += heard;
            stops.Add(() => slice.Pushed -= heard);
        }

        return new Hearing(() => stops.ForEach(stop => stop()));
    }

    /// <summary>The sessions the watcher brings to the person, oldest heard first (<see cref="AttentionWaits.QuietWaits"/>).</summary>
    public static IReadOnlyList<object> QuietWaits(SessionWatch watch, SessionProcesses processes) => AttentionWaits.QuietWaits(watch, processes);

    /// <summary>Wake the slice where the watcher's mark or row moved (<see cref="SessionWatch.Changed"/>), which no bus event tells.</summary>
    public static IDisposable HearQuiet(SessionWatch watch, Action wake)
    {
        void Heard(string _) => wake();
        watch.Changed += Heard;
        return new Hearing(() => watch.Changed -= Heard);
    }

    /// <summary>Wake the slice where a held tool call started or ended (<see cref="HeldPermissions.Changed"/>), which no bus event tells.</summary>
    public static IDisposable HearHolds(HeldPermissions holds, Action wake)
    {
        void Heard(string _) => wake();
        holds.Changed += Heard;
        return new Hearing(() => holds.Changed -= Heard);
    }

    /// <summary>Each remote the home holds since it could not authenticate, oldest first (<see cref="AttentionWaits.GitWaits"/>).</summary>
    public static IReadOnlyList<object> GitWaits(string home) => AttentionWaits.GitWaits(home);

    /// <summary>The integration's waits on the owner, oldest first by kind (<see cref="AttentionWaits.IntegrationWaits"/>).</summary>
    public static IReadOnlyList<object> IntegrationWaits(string home, IReadOnlyList<IntegrationCardWait> cards, IReadOnlyList<DeclarationOwed>? declarations = null) =>
        AttentionWaits.IntegrationWaits(home, cards, declarations);

    /// <summary>Wake the slice where a hold on a git remote of <paramref name="home"/>'s is kept or lifted (<see cref="GitHolds.Changed"/>).</summary>
    public static IDisposable HearGitHolds(string home, Action wake)
    {
        void Heard(string changed)
        {
            if (string.Equals(Path.GetFullPath(changed), Path.GetFullPath(home), StringComparison.OrdinalIgnoreCase)) wake();
        }

        GitHolds.Changed += Heard;
        return new Hearing(() => GitHolds.Changed -= Heard);
    }

    /// <summary>Each git go-ahead a door holds, oldest first (<see cref="AttentionWaits.GitAskWaits"/>).</summary>
    public static IReadOnlyList<object> GitAskWaits(string home) => AttentionWaits.GitAskWaits(home);

    /// <summary>
    /// Wake the slice where a door raises, or a person answers, a git go-ahead: the doors are other processes, so the home's
    /// <c>run/git/asks/</c> is watched, made where it is not there yet.
    /// </summary>
    public static IDisposable HearGitAsks(string home, Action wake)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.GitAsks);
        Directory.CreateDirectory(folder);
        var watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
        void Heard(object? _, FileSystemEventArgs __) => wake();
        watcher.Created += Heard;
        watcher.Deleted += Heard;
        watcher.Renamed += (_, _) => wake();
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    /// <summary>Wake the slice where a stretch below the free-space floor is said or ends (<see cref="SpaceFloors.Changed"/>).</summary>
    public static IDisposable HearFloors(SpaceFloors floors, Action wake)
    {
        void Heard(string _) => wake();
        floors.Changed += Heard;
        return new Hearing(() => floors.Changed -= Heard);
    }

    /// <summary>Wake the slice where the registry the machine's facts hold landed with a new value, by its reference.</summary>
    public static IDisposable HearRegistry(MachineFacts facts, Action wake)
    {
        var gate = new Lock();
        var version = facts.Current.Version;
        var machine = facts.Current.Of(HelpFacts.Machine, HelpFacts.Whole)?.Value;
        void Landed(MachineFactsSnapshot snapshot)
        {
            bool moved;
            lock (gate)
            {
                if (snapshot.Version <= version) return;
                var now = snapshot.Of(HelpFacts.Machine, HelpFacts.Whole)?.Value;
                moved = !ReferenceEquals(now, machine);
                (version, machine) = (snapshot.Version, now);
            }

            if (moved) wake();
        }

        facts.Changed += Landed;
        return new Hearing(() => facts.Changed -= Landed);
    }

    /// <summary>A hearing that wakes only where what <paramref name="read"/> answers moved since it last heard.</summary>
    private static Action OnMove(Func<string> read, Action wake)
    {
        string? last = null;
        var gate = new Lock();
        return () =>
        {
            var now = read();
            bool moved;
            lock (gate)
            {
                moved = now != last;
                last = now;
            }

            if (moved) wake();
        };
    }

    private static SliceSnapshot? Ready(StateSlice? slice) => slice?.Snapshot() is { Status: StateSlice.Ready } snapshot ? snapshot : null;

    /// <summary>Every record a keyed slice holds, its partitions in name order and each in its order; null where it never judged.</summary>
    private static List<JsonObject>? Records(StateSlice? slice) =>
        Ready(slice) is { } snapshot ? [.. snapshot.Partitions.SelectMany(each => Values(Node(each.Data)))] : null;

    private static IEnumerable<JsonObject> Values(JsonNode? keyed) =>
        keyed is JsonObject records ? records.Select(each => each.Value).OfType<JsonObject>() : [];

    private static IEnumerable<JsonObject> Items(JsonNode? node) => node is JsonArray items ? items.OfType<JsonObject>() : [];

    private static JsonNode? Node(object? data) => data as JsonNode ?? JsonSerializer.SerializeToNode(data, IpcJson.Options);

    /// <summary>Each answer as the bridge writes it to the page: the page's shape, a null left out.</summary>
    private static List<JsonObject> Written<T>(IEnumerable<T> answers) =>
        [.. answers.Select(answer => JsonSerializer.SerializeToNode(answer, IpcJson.Options)).OfType<JsonObject>()];

    private sealed class Hearing(Action stop) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0) stop();
        }
    }
}
