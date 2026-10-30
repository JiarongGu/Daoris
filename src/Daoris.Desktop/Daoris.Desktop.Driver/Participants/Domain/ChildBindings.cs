using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One native child of a part, bound to that part from what its harness wrote (FABRIC-C1a, D173): never a session of its own.
/// </summary>
/// <param name="Harness"><c>claude-code</c> or <c>codex</c>.</param>
/// <param name="Native">Its id as its harness names it: Claude Code's agent id, Codex's thread id.</param>
public sealed record ChildBinding(string Harness, string Native)
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

    /// <summary><see cref="ChildBindings.Active"/>, <see cref="ChildBindings.Finished"/>, <see cref="ChildBindings.Ended"/> or <see cref="ChildBindings.Lost"/>.</summary>
    public string State { get; init; } = ChildBindings.Active;

    /// <summary>How many of its calls reached the connector.</summary>
    public int Calls { get; init; }
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
/// only seen, never calling, is <see cref="Observed"/>. Its end is its harness's own stop where one was said
/// (<see cref="Finished"/>), else the part's: <see cref="Ended"/> with a part that ended clean, <see cref="Lost"/> with one
/// killed, stopped or failed, since a killed root says nothing of its children on either harness.</para>
///
/// <para>🔴 <b>Ids only</b>: a value is read only where it is shaped as an id, the connector's <c>CallLineage.Id</c> rule, copied
/// here; nothing the lines carry is a path.</para>
/// </remarks>
public sealed record ChildBindings
{
    /// <summary>Running with its part, its own end not yet said.</summary>
    public const string Active = "active";

    /// <summary>Its harness said it stopped.</summary>
    public const string Finished = "finished";

    /// <summary>Its part ended clean, and its harness said nothing of its own end.</summary>
    public const string Ended = "ended";

    /// <summary>Its part was killed, stopped or failed, and its harness said nothing of its own end.</summary>
    public const string Lost = "lost";

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

    private static readonly string[] Signals = ["hook", "call", "stream"];

    /// <summary>Whether the connector named a field its harness sent that is not an id (FABRIC-C2a): the call is unknown, never the root's.</summary>
    private static bool Malformed(JsonObject line) => line["malformed"] is not null;

    /// <summary>The harness the lines are from, or null where none said.</summary>
    public string? Harness { get; init; }

    /// <summary>The part's own agent's native id: Claude Code's session, Codex's root thread; null where unsaid.</summary>
    public string? Root { get; init; }

    /// <summary>Whether the last thing written is the part's end.</summary>
    public bool PartEnded { get; init; }

    /// <summary>Whether the part's last end was clean; null before any end.</summary>
    public bool? Clean { get; init; }

    /// <summary>The children, in the order they were first named.</summary>
    public IReadOnlyList<ChildBinding> Children { get; init; } = [];

    /// <summary>The calls the part's own agent made.</summary>
    public int RootCalls { get; init; }

    /// <summary>Calls whose harness wrote no lineage the connector reads.</summary>
    public int UnknownCalls { get; init; }

    /// <summary>Claude Code calls no hook named, whose agent is unknown.</summary>
    public int Unjoined { get; init; }

    private sealed class Child(string harness, string native)
    {
        public string Harness { get; } = harness;
        public string Native { get; } = native;
        public string? Type { get; set; }
        public string? StreamParent { get; set; }
        public int? StreamDepth { get; set; }
        public string? CallParent { get; set; }
        public HashSet<string> Seen { get; } = new(StringComparer.Ordinal);
        public int Calls { get; set; }
        public bool Stopped { get; set; }
        public string? Closed { get; set; }
    }

    /// <summary>The children the lines name, bound to the part; lines that do not read are skipped.</summary>
    public static ChildBindings Fold(IEnumerable<string> lines)
    {
        var read = lines.Select(Parse).OfType<JsonObject>().ToList();

        // First the joins and the roots, which do not depend on the order lines were written in: Claude Code's is the hook's
        // session, Codex's the thread a call names with no parent, else the root its metadata names.
        string? harness = null;
        string? claudeRoot = null;
        string? codexNamed = null;
        var agentOf = new Dictionary<string, string?>(StringComparer.Ordinal);
        var roots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in read)
        {
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
            }
        }

        var codexRoot = roots.FirstOrDefault() ?? codexNamed;
        var root = harness == Codex ? codexRoot : claudeRoot;

        var children = new List<Child>();
        var byId = new Dictionary<string, Child>(StringComparer.Ordinal);
        Child Of(string kind, string id)
        {
            if (!byId.TryGetValue(id, out var child))
            {
                child = new Child(kind, id);
                byId[id] = child;
                children.Add(child);
            }

            // A child named again after an end runs again, in the part's next run.
            child.Closed = null;
            return child;
        }

        int rootCalls = 0, unknown = 0, unjoined = 0;
        var ended = false;
        bool? clean = null;
        foreach (var line in read)
        {
            var by = Text(line, "by");
            if (by != "end") ended = false;
            switch (by)
            {
                case "hook" when Id(line, "agent") is { } agent:
                {
                    var child = Of(Claude, agent);
                    child.Seen.Add("hook");
                    child.Type ??= Id(line, "type");
                    if (Text(line, "event") == "stopped") child.Stopped = true;
                    break;
                }

                case "stream" when Id(line, "agent") is { } agent:
                {
                    var child = Of(Claude, agent);
                    child.Seen.Add("stream");
                    child.Type ??= Id(line, "type");
                    if (Text(line, "event") == "started")
                    {
                        child.StreamParent = Id(line, "parent");
                        child.StreamDepth = Number(line, "depth");
                    }
                    else if (Text(line, "event") == "ended")
                    {
                        child.Stopped = true;
                    }

                    break;
                }

                case "call" when Text(line, "harness") == Claude && Id(line, "toolUse") is { } use:
                    if (!agentOf.TryGetValue(use, out var by_)) unjoined++;
                    else if (by_ == UnreadableAgent) unknown++;
                    else if (by_ is null) rootCalls++;
                    else Called(Of(Claude, by_));
                    break;

                case "call" when Text(line, "harness") == Codex && Id(line, "thread") is { } thread && !Malformed(line):
                    if (roots.Contains(thread) || thread == codexRoot)
                    {
                        rootCalls++;
                    }
                    else
                    {
                        var child = Of(Codex, thread);
                        child.CallParent ??= Id(line, "parent");
                        Called(child);
                    }

                    break;

                case "call":
                    unknown++;
                    break;

                case "end":
                    clean = line["clean"] is JsonValue said && said.TryGetValue<bool>(out var value) && value;
                    foreach (var child in children.Where(child => child.Closed is null)) child.Closed = clean == true ? Ended : Lost;
                    ended = true;
                    break;
            }
        }

        return new ChildBindings
        {
            Harness = harness,
            Root = root,
            PartEnded = ended,
            Clean = clean,
            RootCalls = rootCalls,
            UnknownCalls = unknown,
            Unjoined = unjoined,
            Children = [.. children.Select(child => Bound(child, byId, child.Harness == Codex ? codexRoot : claudeRoot))],
        };

        static void Called(Child child)
        {
            child.Calls++;
            child.Seen.Add("call");
        }
    }

    private static ChildBinding Bound(Child child, IReadOnlyDictionary<string, Child> byId, string? root)
    {
        var parent = child.StreamParent ?? child.CallParent ?? (child.StreamDepth == 1 ? root : null);
        return new ChildBinding(child.Harness, child.Native)
        {
            Type = child.Type,
            Parent = parent,
            Depth = DepthOf(child, byId, root, 0),
            Provenance = [.. Signals.Where(child.Seen.Contains)],
            Connectivity = child.Calls > 0 ? Connected : Observed,
            State = child.Stopped ? Finished : child.Closed ?? Active,
            Calls = child.Calls,
        };
    }

    /// <summary>The stream's depth where it said one; else one below the parent, which is the root or a child; else unknown.</summary>
    private static int? DepthOf(Child child, IReadOnlyDictionary<string, Child> byId, string? root, int walked)
    {
        if (child.StreamDepth is { } said) return said;
        var parent = child.StreamParent ?? child.CallParent;
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
}
