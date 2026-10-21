using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Who needs the person, as every window holds it (PUSH1d4, D182 points 2-4 and 6): <i>What needs you</i>'s rows and the Sessions
/// badge's count, judged once per wake in .NET (<see cref="NeedsAPerson"/>) from what the other slices and the driver's look already
/// hold in memory, and pushed by row, so no window judges them and no tick reads the band's inputs again.
/// </summary>
/// <remarks>
/// <para><b>Three keyed partitions.</b> <see cref="Rows"/>, each row under its kind and id as the band keys it, its wait and the values
/// the page words it from; <see cref="Sessions"/>, each thing that waits on the person in Sessions, which the badge marks; and
/// <see cref="Working"/>, each session the driver runs, which the badge and the status bar count (SESSBADGE1). A row's move pushes that
/// row alone. A row holds no place in the list: the page orders by group, then by wait, then by kind (D182's PUSH1d4 note, its PUSH1c3a
/// rule).</para>
///
/// <para><b>Its inputs, from memory.</b> The sessions, the quests, the asks, the groups and the work as their slices last judged them
/// (<see cref="StateSlice.Snapshot"/>); the registry as the machine's facts hold it; and what the driver's look kept: the trust holds,
/// the considerations and the account waits as the tick hands them, the second opinions judged, the rule proposals, the helpers' asks,
/// the held tool calls and the answers the answers-alone bound holds (COWORK1e, off the home's marks); and for the accounts' rows, the roster's last readings as <c>HARNESSES</c> answers them (asking no account,
/// ROSTER1) and how each agent's accounts are used as the accounts slice holds it. Not judged until every slice it reads has judged and
/// the registry has landed (PUSHUX1): a band drawn from half its inputs would say nothing waits where something does. The look's own
/// lists are empty before its first look, as the page's were.</para>
///
/// <para><b>Its wakes</b>, each only asking. Each input slice's push (<see cref="StateSlice.Pushed"/>), the accounts' among them, the
/// sessions' only where what the judge reads of them moved (<see cref="SessionsRead"/>), so a working session's every word judges nothing
/// here; each look of the driver (the loop wakes it beside the driver's state); a held tool call's start and end
/// (<see cref="HeldPermissions.Changed"/>); the registry landing in the machine's facts; a press declared to the rules, the sessions, the
/// driver or the roster (a proposal answered, a helper's yes or no, a reading); a harness action's end; the service coming up; and the
/// 15 s period, which reads a helper's ask a terminal's driver wrote and an account a terminal signed in.</para>
///
/// <para><b>Desktop-only.</b> A trust hold names a folder and the agent's own file, machine paths this machine's windows alone read
/// (D47, D182 point 2).</para>
/// </remarks>
public static class AttentionStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "attention";

    /// <summary>The rows' partition: each row of <i>What needs you</i>, keyed by its kind and id (the page's <c>attentionKey</c>).</summary>
    public const string Rows = "rows";

    /// <summary>
    /// What waits on the person in Sessions, which the badge marks in the waiting hue (SESSBADGE1; its count before, UX5 U20): each session
    /// parked to ask and each quest parked on its failed sessions, by a key of its own.
    /// </summary>
    public const string Sessions = "sessions";

    /// <summary>
    /// The sessions the driver runs (SESSBADGE1, D76's note), each by its key: what the Sessions badge counts as a quantity and the status
    /// bar beside it, the cap's own count (<see cref="NeedsAPerson.Working"/>).
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
            "kind", "id", "since", "workspace", "where", "note", "detail", "publishTo", "choices", "ask", "number", "goAhead", "session",
            "departed", "setUp", "local", "environment", "shows", "opinion", "placed", "sitting", "trust", "quest", "proposal", "title",
            "permission", "blocked", "held", "unreadable", "wholeWorkspace", "holding", "read", "runs", "account", "answer",
        ],
        ["waiting"] = ["kind", "workspace"],
        ["working"] = ["workspace"],
        ["note"] = ["note", "parts"],
        ["goAhead"] = ["kind", "on", "act"],
        ["departed"] = ["count", "number", "reason"],
        ["setUp"] = ["machine", "sequence"],
        ["opinion"] = ["session", "quest", "repository", "workspace", "since", "auto", "opinion"],
        ["gate"] =
        [
            "state", "holds", "required", "opinion", "reviewer", "product", "maker", "label", "reviewing", "findings", "disputes", "since",
            "code", "until", "later", "person", "answers", "says",
        ],
        ["placed"] = ["session", "group", "shown", "archived", "teammate", "strikes", "awaits", "awaitsOf", "work", "holdsQuest", "pausedBy", "holds"],
        ["placedWork"] = ["commits", "uncommitted", "branch"],
        ["pausedBy"] = ["scope", "id"],
        ["wordsHeld"] = ["why", "reason", "repository", "until"],
        ["sitting"] = ["quest", "repository", "verdict", "reason", "strikes", "since"],
        ["trust"] = ["folder", "trustFile"],
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
        ["unreadable"] = WorkStateSlice.Fields["unreadable"],
        ["holding"] = ["quests", "asks"],
        ["account"] = ["agent", "product", "harness", "signsIn", "named", "outside", "readFirst", "noAccount"],
        ["named"] = ["id", "name", "state", "read", "until"],
        ["outside"] = ["id", "list", "workspace"],
        ["readFirst"] = ["id"],
    };

    /// <summary>Which shape a field holds, as <c>shape.field</c>, an object of it or a list of them; every other field is a value.</summary>
    public static readonly IReadOnlyDictionary<string, string> Holds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["row.note"] = "note",
        ["row.goAhead"] = "goAhead",
        ["row.departed"] = "departed",
        ["row.setUp"] = "setUp",
        ["row.opinion"] = "opinion",
        ["opinion.opinion"] = "gate",
        ["row.placed"] = "placed",
        ["placed.work"] = "placedWork",
        ["placed.pausedBy"] = "pausedBy",
        ["placed.holds"] = "wordsHeld",
        ["row.sitting"] = "sitting",
        ["row.trust"] = "trust",
        ["row.proposal"] = "proposal",
        ["row.permission"] = "permission",
        ["permission.choices"] = "choice",
        ["row.answer"] = "answer",
        ["row.blocked"] = "blocked",
        ["blocked.entries"] = "entry",
        ["row.held"] = "attempt",
        ["row.unreadable"] = "unreadable",
        ["row.holding"] = "holding",
        ["row.account"] = "account",
        ["account.named"] = "named",
        ["account.outside"] = "outside",
        ["account.readFirst"] = "readFirst",
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
    ];

    /// <summary>The slice the shell's loop owns, judged from what <paramref name="loop"/> holds in memory.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        Of(ct => FactsAsync(loop, ct), bus,
            [wake => HearSlices(loop.Slices, wake), wake => HearHolds(HeldPermissions.Of(loop.Events), wake), wake => HearRegistry(loop.Facts, wake)],
            loop.Log);

    /// <summary>The slice over <paramref name="facts"/>, read at each judgement, woken where <paramref name="hears"/> hear a move.</summary>
    public static StateSlice Of(Func<AttentionFacts?> facts, IEventBus bus, IReadOnlyList<Func<Action, IDisposable>> hears, MachineLog? log = null) =>
        Of(_ => Task.FromResult(facts()), bus, hears, log);

    /// <summary>The slice over what <paramref name="facts"/> reads at each judgement.</summary>
    public static StateSlice Of(
        Func<CancellationToken, Task<AttentionFacts?>> facts, IEventBus bus, IReadOnlyList<Func<Action, IDisposable>> hears, MachineLog? log = null) =>
        new(Name, bus, async ct => Judge(await facts(ct).ConfigureAwait(false)))
        {
            Keyed = true,
            WakesOn = WakesOn,
            Hears = hears,
            // A file unreadable: the windows keep what was judged, and the next wake judges again.
            Failed = error => log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>
    /// One judgement: the rows and what the badge counts, each keyed and picked to the allow-list; null where an input was never judged,
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
    /// account (ROSTER1).
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
            Permissions = Written(PermissionWaits(HeldPermissions.Of(loop.Events))),
            Answers = Written(AnswerWaits(loop.Home)),
            Doors = Written(AgentsRoutes.Doors(loop.Harnesses, roster)),
            Use = Use(loop.Slices),
            Waits = Written(loop.Look.Waits.Select(DriverLoop.TickWait)),
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
    /// work's units and held attempts; and the registry. Null while any of them was never judged.
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
        foreach (var part in work.Partitions)
        {
            var data = Node(part.Data) as JsonObject;
            if (part.Partition == WorkStateSlice.Held)
            {
                held.AddRange(Items(data?["held"]));
                unreadable.AddRange(Items(data?["unreadable"]));
            }
            else units.AddRange(Items(data?["units"]));
        }

        return new AttentionFacts
        {
            Sessions = sessions, Quests = quests, Asks = asks, Groups = groups, Works = units, Held = held, Unreadable = unreadable,
            Registry = [.. registry.Select(Registration)],
        };
    }

    /// <summary>A registry row as the page's <c>Registration</c> reads it: the host's own judgement of who can be asked (D70).</summary>
    private static JsonObject Registration(RepoView view) => new()
    {
        ["repository"] = view.Repository,
        ["adopted"] = view.Adopted,
        ["addressable"] = view.Adopted || !string.IsNullOrWhiteSpace(view.Root),
        ["workspace"] = view.Workspace,
    };

    /// <summary>
    /// What the agents' asks for a helper wait on the person's word (MIXAGENT2c, D163 point 3), as <c>HELPER_WAITS</c> answers them: each
    /// the driver's look holds <c>waiting</c> with no code, oldest first, with the session that asked. A read off the home's files.
    /// </summary>
    public static IReadOnlyList<object> HelperWaits(string home)
    {
        var files = new HelperFiles(home);
        return
        [
            .. files.Asks()
                .Where(ask => ask.AskedBy == HelperAsk.ByAgent && files.ReadState(ask.Id) is { State: HelperStates.Waiting, Code: null })
                .OrderBy(ask => ask.At)
                .Select(ask => (object)new { ask.Id, Session = ask.Asker, ask.Question, ask.Agent, At = Moments.Write(ask.At) }),
        ];
    }

    /// <summary>
    /// What this machine's driven sessions hold for the person under asking (AUTONOMY1b3a, D184 point 3), as <c>PERMISSION_WAITS</c>
    /// answers them (the route answers this list), oldest first, from the driver's memory: each with when its bound releases it
    /// (<c>until</c>, PERMUNTIL1), the page's one source for the bound.
    /// </summary>
    public static IReadOnlyList<object> PermissionWaits(HeldPermissions holds) =>
    [
        .. holds.Now().Select(held => (object)new
        {
            held.Session, held.Request, held.Call, held.Title, held.Kind,
            Choices = held.Choices.Select(choice => new { choice.Id, choice.Name, choice.Kind }),
            Since = Moments.Write(held.Since),
            Until = Moments.Write(held.Until),
        }),
    ];

    /// <summary>
    /// The answers the answers-alone bound holds for the person (COWORK1e, D188 point 4; the message-urgency design §8.3), as
    /// <c>ANSWER_WAITS</c> answers them, oldest first: each session's mark <see cref="GoOnMarks.AnswersAlone"/> with the agent that
    /// answered, the answers' ids and since when. A read off the home's marks; one the person kept with *Not now*, and a mark of any
    /// other reason, waits on nothing of theirs.
    /// </summary>
    public static IReadOnlyList<object> AnswerWaits(string home) =>
    [
        .. new GoOnMarks(home).All()
            .Where(each => each.Mark.Why == GoOnMarks.AnswersAlone)
            .OrderBy(each => each.Mark.At).ThenBy(each => each.Session, StringComparer.Ordinal)
            .Select(each => (object)new { each.Session, each.Mark.Agent, Words = each.Mark.Said, Since = Moments.Write(each.Mark.At) }),
    ];

    /// <summary>
    /// What the judge reads of the session records, as one string (PUSH1d4): each record's id, state, answer, repository and workspace,
    /// its kind and the marks the cap's count reads (SESSBADGE1), and, where it is not busy working, when it moved and its note. A working
    /// session's record is written at each word and moves its time alone, which no row reads; its park moves its state.
    /// </summary>
    public static string SessionsRead(IEnumerable<JsonObject> records) =>
        string.Join("\n", records.Select(record =>
        {
            var state = Json.Text(record, "state");
            var busy = state is "queued" or "starting" or "working";
            return string.Join("\t",
                Json.Text(record, "id"), state, Json.Text(record, "answer"), Json.Text(record, "repository"), Json.Text(record, "workspace"),
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

    /// <summary>Wake the slice where a held tool call started or ended (<see cref="HeldPermissions.Changed"/>), which no bus event tells.</summary>
    public static IDisposable HearHolds(HeldPermissions holds, Action wake)
    {
        void Heard(string _) => wake();
        holds.Changed += Heard;
        return new Hearing(() => holds.Changed -= Heard);
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
