using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One native child of a part, bound to that part from what its harness wrote (FABRIC-C1a, D173): never a session of its own. Its
/// state and moments are its latest run's (COWORKVIEW1a); its earlier runs are never a second row.
/// </summary>
/// <param name="Harness"><c>claude-code</c> or <c>codex</c>; null where no line said.</param>
/// <param name="Native">Its id as its harness names it: Claude Code's agent id, Codex's thread id, the protocol door's announcement.</param>
public sealed record ChildBinding(string? Harness, string Native)
{
    /// <summary>The kind of agent, where its harness named one (Claude Code's <c>agent_type</c>).</summary>
    public string? Type { get; init; }

    /// <summary>
    /// The native id of what spawned it: the part's own agent (<see cref="ChildBindings.Root"/>) or another child. Null where no
    /// harness said, which is unknown, never the root by default.
    /// </summary>
    public string? Parent { get; init; }

    /// <summary>How far below the part's own agent it is, 1 for its child; null where unknown.</summary>
    public int? Depth { get; init; }

    /// <summary>Which signals named it, in this order where seen: <c>hook</c>, <c>call</c>, <c>stream</c>.</summary>
    public IReadOnlyList<string> Provenance { get; init; } = [];

    /// <summary><see cref="ChildBindings.Connected"/> where a call of its reached the connector, else <see cref="ChildBindings.Observed"/>.</summary>
    public string Connectivity { get; init; } = ChildBindings.Observed;

    /// <summary>Its run's state, one of <see cref="ChildBindings"/>' (the design §3.6, <i>what a child reads</i>).</summary>
    public string State { get; init; } = ChildBindings.Uncertain;

    /// <summary>How many of its calls reached the connector.</summary>
    public int Calls { get; init; }

    /// <summary>Its label where a line gave one: the protocol door's name or task, the stream's task description.</summary>
    public string? Label { get; init; }

    /// <summary>Its run's start, or its first line where no start was said (<see cref="StartSaid"/> false); null where it had no moment.</summary>
    public DateTimeOffset? Since { get; init; }

    public bool StartSaid { get; init; }

    /// <summary>The moment of the end its harness or its process's <c>end</c> said; null for a close that claims none.</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>
    /// Its run's last line, for a run read not known, unrecorded or ended with its agent by this window's token, and a run begun in a
    /// generation already closed: before the line that closed it, never moved after.
    /// </summary>
    public DateTimeOffset? LastSeen { get; init; }
}

/// <summary>
/// A part's native children, folded from its lineage lines (FABRIC-C1a, D173; <c>docs/2026-10-16-fabric-contract-audit.md</c>
/// §2-§3): the connector's line per call (<c>by: call</c>), Claude Code's hook (<c>by: hook</c>), Claude Code's stream as the
/// driver read it (<c>by: stream</c>) and the part's end (<c>by: end</c>). The lines are the one store; this is their reading,
/// and the part's record stays canonical (D173's first guard).
/// </summary>
/// <remarks>
/// <para><b>How a child is told apart.</b> On Claude Code, each subagent is its hook's <c>agent_id</c>; a call is joined to the
/// agent that made it through the hook's <c>tool_use_id</c>, which is the call's <c>_meta</c> toolUseId; its parent is the
/// stream's <c>parent_task_id</c>, and a first-level subagent's parent is the part's own agent, the hook's session. On Codex,
/// each thread is a call's <c>thread</c>, its parent the call's <c>parent</c>, and the root the thread with none.</para>
///
/// <para><b>What is not said stays unknown</b> (the proposal's canaries 2 and 9). A child whose parent no harness wrote has
/// none and no depth; a call with no lineage, and a Claude call no hook named, are counted apart and bound to nobody; a child
/// only seen, never calling, is <see cref="Observed"/>.</para>
///
/// <para><b>Generations and runs</b> (COWORKVIEW1a, the design §3.6, rules 5 to 8). A <c>start</c> begins its process's
/// generation, and a line naming its process is placed there wherever it sits; before a file's first <c>start</c> a line with
/// none is history, placed by position between the ends an older build wrote. From that <c>start</c> on, a line tied to no
/// process is unplaced: its ids counted, never a row or a state. A generation closes only where its process's exit is proven,
/// by its own <c>end</c> or by this window's token gone, each saying only what it proves; else its runs read
/// <see cref="Working"/> only where this window watches its process, and <see cref="Uncertain"/> everywhere else.</para>
///
/// <para>🔴 <b>Ids only</b>: a value is read only where it is shaped as an id, the connector's <c>CallLineage.Id</c> rule, copied
/// here; nothing the lines carry is a path, save a child's bounded label.</para>
/// </remarks>
public sealed record ChildBindings
{
    /// <summary>Its run open in a process this window watches alive, its harness saying ends.</summary>
    public const string Working = "working";

    /// <summary>Its run open in a process this window watches alive, its harness saying no end (Codex's threads): never working.</summary>
    public const string Unsaid = "unsaid";

    /// <summary>Its run open where nothing proves its process gone and this window does not watch it alive, or bounded by an unclean end
    /// an older build wrote: never working, never lost.</summary>
    public const string Uncertain = "uncertain";

    public const string Completed = "completed";

    public const string Failed = "failed";

    public const string Cancelled = "cancelled";

    public const string Disconnected = "disconnected";

    public const string Stopped = "stopped";

    /// <summary>Its harness said it stopped with no word, or a word none of the five.</summary>
    public const string Finished = "finished";

    /// <summary>Its process's end said clean, and its harness said nothing of its own end: ended with its agent.</summary>
    public const string Ended = "ended";

    /// <summary>Its process's end said killed, stopped or failed, and its harness said nothing of its own: lost when its agent ended.</summary>
    public const string Lost = "lost";

    /// <summary>In a driven record's history, whose ends between its processes were never written: its end not recorded.</summary>
    public const string Unrecorded = "unrecorded";

    /// <summary>Its process's exit seen by this window's token and how it ended not yet said by its end: no manner claimed.</summary>
    public const string AgentEnded = "agentEnded";

    /// <summary>A record's kind whose every process wrote its own end before row a, so a clean one in its history is its process's.</summary>
    public const string Chat = "chat";

    /// <summary>A call of its reached the connector (the proposal's L2).</summary>
    public const string Connected = "connected";

    /// <summary>Only seen, by a hook or the stream (L1).</summary>
    public const string Observed = "observed";

    private const string Claude = "claude-code";
    private const string Codex = "codex";

    /// <summary>🔴 The connector's <c>CallLineage</c> id shape, copied (twins).</summary>
    private static readonly Regex IdShape = new("^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$", RegexOptions.CultureInvariant);

    /// <summary>An agent id the hook was sent and could not read; no id's shape, so no real id equals it.</summary>
    private const string UnreadableAgent = "\0unreadable";

    private static readonly string[] Signals = ["hook", "call", "stream", "acp"];

    private static readonly string[] Words = [Completed, Failed, Cancelled, Disconnected, Stopped];

    /// <summary>A re-run's suffix on the protocol door's ids (CONSOLE2): the same child run again.</summary>
    private static readonly Regex ReRun = new(":generation:[0-9]+$", RegexOptions.CultureInvariant);

    /// <summary>Whether the connector named a field its harness sent that is not an id (FABRIC-C2a): the call is unknown, never the root's.</summary>
    private static bool Malformed(JsonObject line) => line["malformed"] is not null;

    /// <summary>The harness the lines are from, or null where none said.</summary>
    public string? Harness { get; init; }

    /// <summary>The part's own agent's native id: Claude Code's session, Codex's root thread; null where unsaid.</summary>
    public string? Root { get; init; }

    /// <summary>The children, in the order they were first named.</summary>
    public IReadOnlyList<ChildBinding> Children { get; init; } = [];

    /// <summary>The calls the part's own agent made.</summary>
    public int RootCalls { get; init; }

    /// <summary>Calls whose harness wrote no lineage the connector reads.</summary>
    public int UnknownCalls { get; init; }

    /// <summary>Claude Code calls no hook named, whose agent is unknown.</summary>
    public int Unjoined { get; init; }

    /// <summary>On a part with announced children, the agent ids its other lines named that no announcement matched: never children.</summary>
    public int Unmatched { get; init; }

    /// <summary>The agent ids named only by lines tied to no process: never children, rows or states.</summary>
    public int Unplaced { get; init; }

    /// <summary>The tokens this window read its generations by, each with the reading the fold took (COWORKVIEW1a, rule 12).</summary>
    public IReadOnlyDictionary<string, TokenReading> Readings { get; private init; } = new Dictionary<string, TokenReading>();

    private IReadOnlySet<string> UnmatchedIds { get; init; } = new HashSet<string>();
    private IReadOnlySet<string> UnplacedIds { get; init; } = new HashSet<string>();
    private IReadOnlySet<string> RootIds { get; init; } = new HashSet<string>();
    private IReadOnlySet<string> Processes { get; init; } = new HashSet<string>();
    private IReadOnlyDictionary<string, string> Announced { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Whether a line appended after this reading moves what it holds (COWORKVIEW1a, rule 13): any line but a call does; a call only
    /// where it names an id held neither as a child, among the unmatched or as the part's root, nor, tied to no process, among the
    /// unplaced; or a child read not known or whose agent ended, whose last seen it moves; or, on Codex, a held child named with no
    /// parent, which becomes a root. A Claude Code connector call names its agent by the hook's line beside it, so it moves nothing.
    /// </summary>
    public bool Wakes(string line)
    {
        if (Parse(line) is not { } read) return false;
        string id;
        switch (Text(read, "by"))
        {
            case "hook" when Text(read, "event") == "call":
                if (read["agentUnreadable"] is not null || Id(read, "agent") is not { } agent) return false;
                id = agent;
                break;
            case "call" when Text(read, "harness") == Codex:
                if (Malformed(read) || Id(read, "thread") is not { } thread) return false;
                if (Id(read, "parent") is null) return Children.Any(child => child.Native == thread);
                id = thread;
                break;
            case "call":
                return false;
            default:
                return true;
        }

        var placed = Id(read, "proc") is { } proc ? Processes.Contains(proc) : Processes.Count == 0;
        if (!placed) return !UnplacedIds.Contains(id);
        if (RootIds.Contains(id)) return false;
        var key = Announced.Count == 0 ? id : Announced.GetValueOrDefault(ReRun.Replace(id, ""));
        if (key is null || Children.FirstOrDefault(child => child.Native == key) is not { } held) return !UnmatchedIds.Contains(id);
        return held.State is Uncertain or AgentEnded;
    }

    /// <summary>One process's lines: a <c>start</c>'s, or history's between the ends an older build wrote.</summary>
    private sealed class Generation(int order, string? proc)
    {
        public int Order { get; } = order;

        /// <summary>Its process's token; null for history.</summary>
        public string? Proc { get; } = proc;

        /// <summary>This window's reading of its token; null where another Daoris process issued it.</summary>
        public TokenReading? Reading { get; init; }

        /// <summary>Where its own <c>end</c> sits, what it said and when; null while the file holds none.</summary>
        public int? EndIndex { get; set; }

        public bool Clean { get; set; }

        public DateTimeOffset? EndAt { get; set; }
    }

    private sealed class Run(Generation generation, int order)
    {
        public Generation Generation { get; } = generation;
        public int Order { get; } = order;
        public DateTimeOffset? Since { get; init; }
        public bool StartSaid { get; init; }

        /// <summary>Begun by a line placed after its generation's close, which has ended it already.</summary>
        public bool Late { get; init; }

        public DateTimeOffset? Last { get; set; }
        public bool Stopped { get; set; }
        public string? Word { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
    }

    private sealed class Child(string native)
    {
        public string? Harness { get; set; }
        public string Native { get; } = native;
        public string? Type { get; set; }
        public string? StreamParent { get; set; }
        public int? StreamDepth { get; set; }
        public string? AcpParent { get; set; }
        public bool AcpByRoot { get; set; }
        public string? CallParent { get; set; }
        public string? Label { get; set; }
        public HashSet<string> Seen { get; } = new(StringComparer.Ordinal);
        public int Calls { get; set; }

        /// <summary>Its harness says no child's end (Codex's threads): open, it is never working.</summary>
        public bool Unsaid { get; set; }

        public List<Run> Runs { get; } = [];
    }

    private enum Act
    {
        Start,
        Stop,
        Touch,
    }

    /// <summary>The children the lines name, bound to the part; lines that do not read are skipped.</summary>
    /// <param name="kind">The record's kind: <see cref="Chat"/>, or driven, which null reads as.</param>
    /// <param name="window">
    /// This window's reading of a recorded generation's token, by its <c>proc</c> and its <c>start</c>'s <c>issuer</c>; null where
    /// another Daoris process issued it. None reads every generation as another's.
    /// </param>
    public static ChildBindings Fold(IEnumerable<string> lines, string? kind = null, Func<string, string?, TokenReading?>? window = null)
    {
        var read = lines.Select(Parse).OfType<JsonObject>().ToList();

        // First every line, unplaced ones too, in no order, for who an id is: Claude Code's root is the hook's session, Codex's the
        // thread a call names with no parent, else the root its metadata names; a call's agent by its toolUse; the protocol door's
        // children by their id less a re-run's suffix. And each process the file recorded: its `start`, and its own `end` wherever
        // it sits.
        string? harness = null;
        string? claudeRoot = null;
        string? codexNamed = null;
        var agentOf = new Dictionary<string, string?>(StringComparer.Ordinal);
        var roots = new HashSet<string>(StringComparer.Ordinal);
        var announced = new Dictionary<string, string>(StringComparer.Ordinal);
        var starts = new Dictionary<string, Generation>(StringComparer.Ordinal);
        var firstStart = -1;
        for (var i = 0; i < read.Count; i++)
        {
            var line = read[i];
            switch (Text(line, "by"))
            {
                case "hook":
                    harness ??= Claude;
                    claudeRoot ??= Id(line, "session");
                    if (Text(line, "event") == "call" && Id(line, "toolUse") is { } use)
                    {
                        agentOf[use] = line["agentUnreadable"] is JsonValue bad && bad.TryGetValue<bool>(out var unread) && unread
                            ? UnreadableAgent
                            : Id(line, "agent");
                    }

                    break;
                case "stream":
                    harness ??= Claude;
                    break;
                case "call" when Text(line, "harness") == Claude:
                    harness ??= Claude;
                    break;
                case "call" when Text(line, "harness") == Codex:
                    harness ??= Codex;
                    if (Id(line, "thread") is { } thread && Id(line, "parent") is null && !Malformed(line)) roots.Add(thread);
                    codexNamed ??= Id(line, "root");
                    break;
                case "acp" when Id(line, "agent") is { } agent:
                    announced.TryAdd(ReRun.Replace(agent, ""), agent);
                    break;
                case "start" when Id(line, "proc") is { } proc && !starts.ContainsKey(proc):
                    if (firstStart < 0) firstStart = i;
                    starts[proc] = new Generation(i, proc) { Reading = window?.Invoke(proc, Text(line, "issuer")) };
                    break;
            }
        }

        for (var i = 0; i < read.Count; i++)
        {
            if (Text(read[i], "by") == "end" && Id(read[i], "proc") is { } proc && starts.TryGetValue(proc, out var ended) && ended.EndIndex is null)
            {
                (ended.EndIndex, ended.Clean, ended.EndAt) = (i, Clean(read[i]), AtOf(read[i]));
            }
        }

        var codexRoot = roots.FirstOrDefault() ?? codexNamed;
        var root = harness == Codex ? codexRoot : claudeRoot;

        // Then each line in the file's order, placed in its process's generation, or as history, or nowhere.
        var children = new List<Child>();
        var byId = new Dictionary<string, Child>(StringComparer.Ordinal);
        var unmatched = new HashSet<string>(StringComparer.Ordinal);
        var unplaced = new HashSet<string>(StringComparer.Ordinal);
        Generation? history = null;
        int rootCalls = 0, unknown = 0, unjoined = 0, runs = 0;
        for (var i = 0; i < read.Count; i++)
        {
            var line = read[i];
            var by = Text(line, "by");
            if (by == "end" && Id(line, "proc") is null && (firstStart < 0 || i < firstStart) && history is { EndIndex: null } open)
            {
                // An older build's end, closing the history generation open where it sits.
                (open.EndIndex, open.Clean, open.EndAt) = (i, Clean(line), AtOf(line));
                continue;
            }

            string? who = null, agent = null, word = null;
            var act = Act.Touch;
            switch (by)
            {
                case "hook" when Id(line, "agent") is { } named:
                    (who, agent) = (Claude, named);
                    act = Text(line, "event") switch { "started" => Act.Start, "stopped" => Act.Stop, _ => Act.Touch };
                    break;
                case "stream" or "acp" when Id(line, "agent") is { } named:
                    (who, agent, word) = (by == "stream" ? Claude : null, named, Id(line, by == "stream" ? "status" : "state"));
                    act = Text(line, "event") switch { "started" => Act.Start, "ended" => Act.Stop, _ => Act.Touch };
                    break;
                case "call" when Text(line, "harness") == Claude && Id(line, "toolUse") is { } use:
                    if (!agentOf.TryGetValue(use, out var caller)) unjoined++;
                    else if (caller == UnreadableAgent) unknown++;
                    else if (caller is null) rootCalls++;
                    else (who, agent) = (Claude, caller);
                    break;
                case "call" when Text(line, "harness") == Codex && Id(line, "thread") is { } thread && !Malformed(line):
                    if (roots.Contains(thread) || thread == codexRoot) rootCalls++;
                    else (who, agent) = (Codex, thread);
                    break;
                case "call":
                    unknown++;
                    break;
            }

            if (agent is null) continue;
            // On a part with announced children, the rows are theirs: another line's id joins one by its id or a re-run's suffix.
            var key = announced.Count == 0 ? agent : announced.GetValueOrDefault(ReRun.Replace(agent, ""));
            var endsAClosedRun = act == Act.Stop && key is not null && byId.TryGetValue(key, out var known)
                                 && known.Runs.Any(run => run.Generation == history);
            var generation = Placed(i, line, endsAClosedRun);
            if (generation is null)
            {
                unplaced.Add(agent);
                continue;
            }

            if (key is null)
            {
                unmatched.Add(agent);
                continue;
            }

            if (!byId.TryGetValue(key, out var child))
            {
                child = new Child(key);
                byId[key] = child;
                children.Add(child);
            }

            child.Harness ??= who;
            child.Seen.Add(by!);
            Said(child, by!, line, key: id => announced.GetValueOrDefault(ReRun.Replace(id, "")) ?? id);
            if (by == "call") child.Calls++;
            if (who == Codex && announced.Count == 0) child.Unsaid = true;
            Moved(child, generation, i, act, word, AtOf(line));
        }

        HashSet<string> rootIds = [.. roots];
        if (claudeRoot is not null) rootIds.Add(claudeRoot);
        if (codexRoot is not null) rootIds.Add(codexRoot);
        return new ChildBindings
        {
            Harness = harness,
            Root = root,
            RootCalls = rootCalls,
            UnknownCalls = unknown,
            Unjoined = unjoined,
            Unmatched = unmatched.Count,
            Unplaced = unplaced.Count,
            UnmatchedIds = unmatched,
            UnplacedIds = unplaced,
            RootIds = rootIds,
            Announced = announced,
            Processes = starts.Keys.ToHashSet(StringComparer.Ordinal),
            Readings = starts.Values.Where(each => each.Reading is not null).ToDictionary(each => each.Proc!, each => each.Reading!.Value, StringComparer.Ordinal),
            Children = [.. children.Select(child => Bound(child, byId, (child.Harness ?? harness) == Codex ? codexRoot : claudeRoot, harness, kind))],
        };

        // A line naming its process is that process's wherever it sits, and one no `start` names is no process's. Before a file's first
        // `start` a line naming none is history, in the generation open where it sits, and after an end it begins the next, save a stop
        // of a run the end closed, which stays in it. From that `start` on, a line naming no process is no process's.
        Generation? Placed(int i, JsonObject line, bool endsAClosedRun)
        {
            if (Id(line, "proc") is { } proc) return starts.GetValueOrDefault(proc);
            if (firstStart >= 0 && i > firstStart) return null;
            if (history is null || (history.EndIndex is not null && !endsAClosedRun)) history = new Generation(i, null);
            return history;
        }

        // A run begins at a start its harness said, or at its id's first line in the generation; ends at a stop its harness said or
        // at its generation's close. A start after either is a new run; a call or stop after them begins nothing, a stop's word
        // still the run's. A line after its generation's close changes that generation alone and moves no last seen.
        void Moved(Child child, Generation generation, int i, Act act, string? word, DateTimeOffset? at)
        {
            var run = child.Runs.LastOrDefault(each => each.Generation == generation);
            var closed = generation.EndIndex is int end && i > end;
            var over = run is not null && (run.Stopped || closed);
            if (run is null || (act == Act.Start && over))
            {
                run = new Run(generation, runs++) { Since = at, StartSaid = act == Act.Start, Late = closed, Last = at };
                child.Runs.Add(run);
                if (act != Act.Stop) return;
            }

            if (act == Act.Stop)
            {
                run.Word ??= word;
                if (run.Stopped) return;
                (run.Stopped, run.EndedAt) = (true, at);
            }

            if (!over) run.Last = at;
        }
    }

    /// <summary>What a placed line says of who its child is: its kind, its place, its label.</summary>
    private static void Said(Child child, string by, JsonObject line, Func<string, string> key)
    {
        switch (by)
        {
            case "hook":
                child.Type ??= Id(line, "type");
                break;
            case "stream":
                child.Type ??= Id(line, "type");
                if (Text(line, "event") != "started") break;
                child.StreamParent = Id(line, "parent");
                child.StreamDepth = Number(line, "depth");
                child.Label ??= Text(line, "description");
                break;
            case "acp" when Text(line, "event") == "started":
                child.AcpParent ??= Id(line, "parent") is { } parent ? key(parent) : null;
                child.AcpByRoot |= Number(line, "depth") == 1;
                child.Label ??= Text(line, "name") ?? Text(line, "task");
                break;
            case "call":
                child.CallParent ??= Id(line, "parent");
                break;
        }
    }

    private static ChildBinding Bound(Child child, IReadOnlyDictionary<string, Child> byId, string? root, string? harness, string? kind)
    {
        var parent = child.StreamParent ?? child.AcpParent ?? child.CallParent ?? (child.StreamDepth == 1 || child.AcpByRoot ? root : null);
        var run = child.Runs.MaxBy(each => (each.Generation.Order, each.Order))!;
        var (state, endedAt, lastSeen) = Read(run, kind, child.Unsaid);
        return new ChildBinding(child.Harness ?? harness, child.Native)
        {
            Type = child.Type,
            Parent = parent,
            Depth = DepthOf(child, byId, root, 0),
            Provenance = [.. Signals.Where(child.Seen.Contains)],
            Connectivity = child.Calls > 0 ? Connected : Observed,
            State = state,
            Calls = child.Calls,
            Label = child.Label,
            Since = run.Since,
            StartSaid = run.StartSaid,
            EndedAt = endedAt,
            LastSeen = lastSeen,
        };
    }

    /// <summary>
    /// What a run reads (the design §3.6, <i>what a child reads</i>): as its harness said it ended; else closed only where its process's
    /// exit is proven, by its own end or this window's token gone, saying what that proves; else working only where this window
    /// watches its process alive; else not known.
    /// </summary>
    private static (string State, DateTimeOffset? EndedAt, DateTimeOffset? LastSeen) Read(Run run, string? kind, bool unsaid)
    {
        if (run.Stopped) return (Words.Contains(run.Word) ? run.Word! : Finished, run.EndedAt, null);
        var generation = run.Generation;
        var late = run.Late ? run.Last : null;
        if (generation.EndIndex is not null)
        {
            // A recorded end is its issuer's word that the process exited; a chat's from before row a only where it says clean; a
            // driven record's from before row a may be a later process's, so it claims nothing.
            if (generation.Proc is not null || (kind == Chat && generation.Clean))
            {
                return (generation.Clean ? Ended : Lost, run.Late ? null : generation.EndAt, late);
            }

            return (kind == Chat ? Uncertain : Unrecorded, null, run.Last);
        }

        return generation.Proc is null ? (Uncertain, null, run.Last) : generation.Reading switch
        {
            TokenReading.Gone => (AgentEnded, null, run.Last),
            TokenReading.Watched => (unsaid ? Unsaid : Working, null, null),
            _ => (Uncertain, null, run.Last),
        };
    }

    /// <summary>The stream's depth where it said one; else one below the parent, which is the root or a child; else unknown.</summary>
    private static int? DepthOf(Child child, IReadOnlyDictionary<string, Child> byId, string? root, int walked)
    {
        if (child.StreamDepth is { } said) return said;
        if (child.AcpByRoot && child.AcpParent is null) return 1;
        var parent = child.StreamParent ?? child.AcpParent ?? child.CallParent;
        if (parent is null || walked > byId.Count) return null;
        if (parent == root) return 1;
        return byId.TryGetValue(parent, out var above) && DepthOf(above, byId, root, walked + 1) is { } depth ? depth + 1 : null;
    }

    /// <summary>
    /// The lineage line a line of Claude Code's <c>stream-json</c> says, or null: a subagent's start (<c>task_started</c>,
    /// <c>local_agent</c>) with its parent, depth and task's description, and the end (<c>task_notification</c>) of one whose start
    /// was seen, which <paramref name="agents"/> keeps. Every other line, a background command's included, says none.
    /// </summary>
    /// <param name="proc">The token of the process whose stream it is, written where it is shaped as an id.</param>
    public static string? StreamLine(string line, DateTimeOffset at, ISet<string> agents, string? proc = null)
    {
        if (!line.Contains("\"task_", StringComparison.Ordinal) || Parse(line) is not { } root || Text(root, "type") != "system") return null;
        switch (Text(root, "subtype"))
        {
            case "task_started" when Text(root, "task_type") == "local_agent" && Id(root, "task_id") is { } agent:
            {
                agents.Add(agent);
                var said = Stamped(at, "stream", "started", agent);
                if (Id(root, "parent_task_id") is { } parent) said["parent"] = parent;
                if (Number(root, "spawn_depth") is { } depth) said["depth"] = depth;
                if (Id(root, "subagent_type") is { } type) said["type"] = type;
                if (LineageLabels.Task(Text(root, "description")) is { } description) said["description"] = description;
                return Named(said, proc);
            }

            case "task_notification" when Id(root, "task_id") is { } agent && agents.Contains(agent):
            {
                var said = Stamped(at, "stream", "ended", agent);
                if (Id(root, "status") is { } status) said["status"] = status;
                return Named(said, proc);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// A child the protocol door announced (COWORKVIEW1a), or null where its id is not one: its place, <c>parent</c> or
    /// <c>depth: 1</c>, and its label as bounded.
    /// </summary>
    public static string? AcpStarted(LineageAnnouncement said, DateTimeOffset at, string? proc = null)
    {
        if (!IdShape.IsMatch(said.Agent)) return null;
        var line = Stamped(at, "acp", "started", said.Agent);
        if (said.Parent is { } parent && IdShape.IsMatch(parent)) line["parent"] = parent;
        else if (said.ByRoot) line["depth"] = 1;
        if (said.Name is { } name) line["name"] = name;
        if (said.Task is { } task) line["task"] = task;
        return Named(line, proc);
    }

    /// <summary>A child's end the protocol door's wire said, with its word where that is one; null where its id is not one.</summary>
    public static string? AcpEnded(string agent, string? state, DateTimeOffset at, string? proc = null)
    {
        if (!IdShape.IsMatch(agent)) return null;
        var line = Stamped(at, "acp", "ended", agent);
        if (state is not null && IdShape.IsMatch(state)) line["state"] = state;
        return Named(line, proc);
    }

    /// <summary>
    /// A process's start (COWORKVIEW1a), written before its spawn: its token, and the Daoris process that issued it as
    /// <c>{pid} {ticks}</c>; null where the token is not one.
    /// </summary>
    public static string? StartLine(string proc, string issuer, DateTimeOffset at) => IdShape.IsMatch(proc)
        ? new JsonObject { ["at"] = Moment(at), ["by"] = "start", ["proc"] = proc, ["issuer"] = issuer }.ToJsonString()
        : null;

    /// <summary>A process's end once its exit was read: clean, or killed, stopped or failed; naming its process where it has a token.</summary>
    public static string EndLine(bool clean, DateTimeOffset at, string? proc = null)
    {
        var line = new JsonObject { ["at"] = Moment(at), ["by"] = "end" };
        if (proc is not null && IdShape.IsMatch(proc)) line["proc"] = proc;
        line["clean"] = clean;
        return line.ToJsonString();
    }

    private static JsonObject Stamped(DateTimeOffset at, string by, string @event, string agent) =>
        new() { ["at"] = Moment(at), ["by"] = by, ["event"] = @event, ["agent"] = agent };

    private static string Named(JsonObject line, string? proc)
    {
        if (proc is not null && IdShape.IsMatch(proc)) line["proc"] = proc;
        return line.ToJsonString();
    }

    private static string Moment(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static JsonObject? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonObject line, string name) =>
        line[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Id(JsonObject line, string name) => Text(line, name) is { } text && IdShape.IsMatch(text) ? text : null;

    private static int? Number(JsonObject line, string name) =>
        line[name] is JsonValue value && value.TryGetValue<int>(out var number) && number > 0 ? number : null;

    /// <summary>A line's moment; null where it carries none that reads.</summary>
    private static DateTimeOffset? AtOf(JsonObject line) =>
        Text(line, "at") is { } at && DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment
            : null;

    private static bool Clean(JsonObject line) => line["clean"] is JsonValue said && said.TryGetValue<bool>(out var clean) && clean;
}
