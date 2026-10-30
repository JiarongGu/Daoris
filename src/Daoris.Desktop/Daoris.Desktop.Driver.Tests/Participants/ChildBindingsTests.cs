using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-C1a (D173, its FABRIC-C0 note; <c>docs/2026-10-16-fabric-contract-audit.md</c> §2): a part's native children, bound
/// from what the harness itself wrote of their lineage. The fold reads the session's lineage lines: the connector's per call
/// (<c>by: call</c>), the hook's on Claude Code (<c>by: hook</c>), the driver's off Claude Code's stream (<c>by: stream</c>)
/// and the part's end (<c>by: end</c>). It binds each child to the part, never to a session of its own, and says what it
/// does not know as unknown.
/// </summary>
/// <remarks>
/// The lines are shaped as the probe kit's receipts (FABRIC-PROBE1): ids as the harnesses wrote them. The call lines are the
/// connector's own, word for word as its <c>CallLineageTests</c> hold them: the twin table (<c>.claude/knowledge/twins.md</c>).
/// </remarks>
public sealed class ChildBindingsTests
{
    private const string Session = "858a71dc-99cf-4136-8787-da86714459d2";
    private const string SiblingA = "aadc0410bd5fa6a1e";
    private const string Grandchild = "a1b84f19758fd8ef5";
    private const string SiblingB = "aeb870c28aae7df9f";
    private const string Tool = "mcp__daoris-knowledge__knowledge_search";

    // COWORKVIEW1a: three processes' tokens, and the states a child reads (§3.6 "what a child reads", rule 5).
    private const string P1 = "a000000000000001";
    private const string P2 = "a000000000000002";
    private const string P3 = "a000000000000003";
    private const string Working = ChildBindings.Working;
    private const string NotKnown = ChildBindings.Uncertain;
    private const string AgentEnded = ChildBindings.AgentEnded;
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 8, 30, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int second) => T0.AddSeconds(second);

    /// <summary>A moment as a child's fields hold one.</summary>
    private static DateTimeOffset? When(int second) => At(second);

    private static string Line(JsonObject line, string? proc = null)
    {
        line["at"] ??= "2026-10-16T08:30:00.000Z";
        if (proc is not null) line["proc"] = proc;
        return line.ToJsonString();
    }

    private static string Call(string toolUse, string? proc = null) =>
        Line(new JsonObject { ["by"] = "call", ["tool"] = "knowledge_search", ["harness"] = "claude-code", ["toolUse"] = toolUse }, proc);

    private static string HookCall(string toolUse, string? agent, string? proc = null) => Line(new JsonObject
    {
        ["by"] = "hook", ["event"] = "call", ["agent"] = agent, ["type"] = agent is null ? null : "general-purpose",
        ["toolUse"] = toolUse, ["tool"] = Tool, ["session"] = Session,
    }, proc);

    private static string Hook(string @event, string agent, string? proc = null) =>
        Line(new JsonObject { ["by"] = "hook", ["event"] = @event, ["agent"] = agent, ["type"] = "general-purpose", ["session"] = Session }, proc);

    private static string Started(string agent, string? parent, int depth, string? proc = null) => Line(new JsonObject
    {
        ["by"] = "stream", ["event"] = "started", ["agent"] = agent, ["parent"] = parent, ["depth"] = depth, ["type"] = "general-purpose",
    }, proc);

    private static string Ended(string agent, string status = "completed", string? proc = null) =>
        Line(new JsonObject { ["by"] = "stream", ["event"] = "ended", ["agent"] = agent, ["status"] = status }, proc);

    private static string End(bool clean, string? proc = null) => ChildBindings.EndLine(clean, DateTimeOffset.UnixEpoch, proc);

    /// <summary>A line about one agent at <paramref name="second"/>: the hook's, the stream's or the protocol door's.</summary>
    private static string Said(string by, string @event, string agent, int second, string? proc = null, string? word = null)
    {
        var line = new JsonObject { ["at"] = Moment(second), ["by"] = by, ["event"] = @event, ["agent"] = agent };
        if (by == "hook") line["session"] = Session;
        if (word is not null) line[by == "acp" ? "state" : "status"] = word;
        if (proc is not null) line["proc"] = proc;
        return line.ToJsonString();
    }

    private static string HookAt(string @event, string agent, int second, string? proc = null) => Said("hook", @event, agent, second, proc);

    private static string StreamAt(string @event, string agent, int second, string? proc = null, string? status = null) =>
        Said("stream", @event, agent, second, proc, status);

    private static string AcpAt(string @event, string agent, int second, string? proc = null, string? state = null) =>
        Said("acp", @event, agent, second, proc, state);

    private static string HookCallAt(string toolUse, string agent, int second, string? proc = null) => Stamped(HookCall(toolUse, agent, proc), second);

    private static string CallAt(string toolUse, int second, string? proc = null) => Stamped(Call(toolUse, proc), second);

    private static string CodexAt(string thread, string? parent, int second, string? proc = null)
    {
        var line = new JsonObject
        {
            ["at"] = Moment(second), ["by"] = "call", ["tool"] = "knowledge_search", ["harness"] = "codex", ["thread"] = thread, ["root"] = "t-root",
        };
        if (parent is not null) line["parent"] = parent;
        if (proc is not null) line["proc"] = proc;
        return line.ToJsonString();
    }

    private static string StartAt(string proc, int second) => ChildBindings.StartLine(proc, "4242 638000000000000000", At(second))!;

    private static string EndAt(string proc, bool clean, int second) => ChildBindings.EndLine(clean, At(second), proc);

    /// <summary>A process's end as a build before row a wrote it: naming no process.</summary>
    private static string HistoryEnd(bool clean, int second) => ChildBindings.EndLine(clean, At(second));

    private static string Moment(int second) => At(second).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static string Stamped(string line, int second)
    {
        var node = JsonNode.Parse(line)!.AsObject();
        node["at"] = Moment(second);
        return node.ToJsonString();
    }

    /// <summary>
    /// A file written line by line with this window's token readings moving beside it, read at each prefix the test names: in this
    /// window, which issued the tokens <see cref="Here"/> names, and from another Daoris process, which holds none.
    /// </summary>
    private sealed class Story(string? kind = null)
    {
        private readonly List<string> _lines = [];
        private readonly Dictionary<string, TokenReading> _here = new(StringComparer.Ordinal);

        public Story Then(params string[] lines)
        {
            _lines.AddRange(lines);
            return this;
        }

        public Story Here(string proc, TokenReading reading)
        {
            _here[proc] = reading;
            return this;
        }

        public ChildBindings Fold(bool elsewhere = false) => ChildBindings.Fold(
            _lines, kind, elsewhere ? null : (proc, _) => _here.TryGetValue(proc, out var reading) ? reading : null);

        public ChildBinding? Child(string id, bool elsewhere = false) => Fold(elsewhere).Children.SingleOrDefault(child => child.Native == id);

        /// <summary>The child's state at this prefix here and, where given, from another Daoris process; null for no row.</summary>
        public Story Reads(string id, string? here, string? elsewhere = null)
        {
            var seen = Child(id)?.State;
            Assert.True(seen == here, $"after {_lines.Count} lines {id} reads {seen ?? "no row"} here, not {here ?? "no row"}");
            if (elsewhere is null) return this;
            var other = Child(id, elsewhere: true)?.State;
            Assert.True(other == elsewhere, $"after {_lines.Count} lines {id} reads {other ?? "no row"} elsewhere, not {elsewhere}");
            return this;
        }
    }

    /// <summary>The probe's family on Claude Code: the root, two siblings, and a grandchild under the first.</summary>
    private static IEnumerable<string> ClaudeFamily(bool stops, string? proc = null)
    {
        yield return HookCall("toolu_root", agent: null, proc);
        yield return Call("toolu_root", proc);
        yield return Hook("started", SiblingA, proc);
        yield return Started(SiblingA, parent: null, depth: 1, proc);
        yield return HookCall("toolu_a", SiblingA, proc);
        yield return Call("toolu_a", proc);
        yield return Hook("started", Grandchild, proc);
        yield return Started(Grandchild, parent: SiblingA, depth: 2, proc);
        yield return HookCall("toolu_g", Grandchild, proc);
        yield return Call("toolu_g", proc);
        if (stops) yield return Hook("stopped", Grandchild, proc);
        if (stops) yield return Ended(Grandchild, proc: proc);
        if (stops) yield return Hook("stopped", SiblingA, proc);
        yield return Hook("started", SiblingB, proc);
        yield return Started(SiblingB, parent: null, depth: 1, proc);
        yield return HookCall("toolu_b", SiblingB, proc);
        yield return Call("toolu_b", proc);
        if (stops) yield return Hook("stopped", SiblingB, proc);
    }

    /// <summary>A recorded process's start then its lines, in a window watching it.</summary>
    private static IEnumerable<string> Recorded(IEnumerable<string> lines, string proc = P1) =>
        lines.Prepend(ChildBindings.StartLine(proc, "4242 638000000000000000", T0)!);

    private static Func<string, string?, TokenReading?> Watching(params string[] procs) =>
        (proc, _) => procs.Contains(proc) ? TokenReading.Watched : null;

    [Fact]
    public void Claude_tells_a_child_a_grandchild_and_two_siblings_apart_bound_to_the_part()
    {
        var bound = ChildBindings.Fold(Recorded(ClaudeFamily(stops: false, P1)), window: Watching(P1));

        Assert.Equal("claude-code", bound.Harness);
        Assert.Equal(Session, bound.Root);
        Assert.Equal(1, bound.RootCalls);
        Assert.Equal(
            [(SiblingA, Session, 1), (Grandchild, SiblingA, 2), (SiblingB, Session, 1)],
            bound.Children.Select(child => (child.Native, child.Parent, child.Depth)));
        Assert.All(bound.Children, child =>
        {
            Assert.Equal("claude-code", child.Harness);
            Assert.Equal("general-purpose", child.Type);
            Assert.Equal(["hook", "call", "stream"], child.Provenance);
            Assert.Equal(ChildBindings.Connected, child.Connectivity);
            Assert.Equal(Working, child.State);
            Assert.Equal(1, child.Calls);
        });
    }

    [Fact]
    public void A_killed_process_s_end_marks_the_children_still_running_lost_and_one_that_stopped_before_it_finished()
    {
        var lines = Recorded(ClaudeFamily(stops: false, P1).Append(Hook("stopped", Grandchild, P1)).Append(End(clean: false, P1)));

        var bound = ChildBindings.Fold(lines, window: Watching(P1));

        Assert.Equal(
            [(SiblingA, ChildBindings.Lost), (Grandchild, ChildBindings.Finished), (SiblingB, ChildBindings.Lost)],
            bound.Children.Select(child => (child.Native, child.State)));
    }

    [Fact]
    public void A_clean_end_ends_the_children_with_their_agent_and_their_own_stop_says_finished()
    {
        var stopped = ChildBindings.Fold(Recorded(ClaudeFamily(stops: true, P1).Append(End(clean: true, P1))));
        var unsaid = ChildBindings.Fold(Recorded(ClaudeFamily(stops: false, P1).Append(End(clean: true, P1))));

        Assert.Equal(
            [(SiblingA, ChildBindings.Finished), (Grandchild, ChildBindings.Completed), (SiblingB, ChildBindings.Finished)],
            stopped.Children.Select(child => (child.Native, child.State)));
        Assert.All(unsaid.Children, child => Assert.Equal(ChildBindings.Ended, child.State));
    }

    [Fact]
    public void Codex_binds_each_thread_by_its_metadata_its_parent_beside_it_and_never_says_a_stop_it_did_not_see()
    {
        static string Codex(string thread, string? parent) => Line(new JsonObject
        {
            ["by"] = "call", ["tool"] = "knowledge_search", ["harness"] = "codex", ["thread"] = thread, ["parent"] = parent,
            ["kind"] = parent is null ? null : "thread_spawn", ["source"] = parent is null ? "user" : "subagent", ["root"] = "t-root",
        });

        var lines = new[]
        {
            Codex("t-root", null), Codex("t-a", "t-root"), Codex("t-g", "t-a"), Codex("t-g", "t-a"), Codex("t-b", "t-root"), End(clean: true),
        };

        var bound = ChildBindings.Fold(lines, ChildBindings.Chat);

        Assert.Equal("codex", bound.Harness);
        Assert.Equal("t-root", bound.Root);
        Assert.Equal(1, bound.RootCalls);
        Assert.Equal(
            [("t-a", "t-root", 1, 1), ("t-g", "t-a", 2, 2), ("t-b", "t-root", 1, 1)],
            bound.Children.Select(child => (child.Native, child.Parent, child.Depth, child.Calls)));
        Assert.All(bound.Children, child =>
        {
            Assert.Equal(["call"], child.Provenance);
            Assert.Null(child.Type);
            Assert.Equal(ChildBindings.Connected, child.Connectivity);
            // Codex says no child's end: each ended with its agent, never "finished" (canary 9), first seen at its first call.
            Assert.Equal((ChildBindings.Ended, false, (DateTimeOffset?)T0), (child.State, child.StartSaid, child.Since));
        });
    }

    [Fact]
    public void What_a_harness_did_not_say_stays_unknown()
    {
        // A call with no lineage, a Claude call no hook named, and a child the hook saw start that called nothing and that no
        // stream placed: none is guessed.
        var lines = new[]
        {
            Line(new JsonObject { ["by"] = "call", ["tool"] = "knowledge_search" }),
            Call("toolu_unnamed"),
            Hook("started", SiblingA),
        };

        var bound = ChildBindings.Fold(lines);

        Assert.Equal(1, bound.UnknownCalls);
        Assert.Equal(1, bound.Unjoined);
        var child = Assert.Single(bound.Children);
        Assert.Equal((SiblingA, (string?)null, (int?)null), (child.Native, child.Parent, child.Depth));
        Assert.Equal(["hook"], child.Provenance);
        Assert.Equal(ChildBindings.Observed, child.Connectivity);
        Assert.Equal(0, child.Calls);
    }

    /// <summary>
    /// FABRIC-C2a (D173's note, point 1; the connector's <c>CallerProof</c> is the twin): an id the harness sent that could not be
    /// read is kept as such, so a call it names is unknown, never the root's.
    /// </summary>
    [Fact]
    public void A_malformed_id_reads_as_unknown_never_as_the_root_s()
    {
        var lines = new[]
        {
            Line(new JsonObject
            {
                ["by"] = "hook", ["event"] = "call", ["agentUnreadable"] = true, ["toolUse"] = "toolu_x", ["tool"] = Tool, ["session"] = Session,
            }),
            Call("toolu_x"),
            """{"at":"2026-10-16T08:30:00.123Z","by":"call","tool":"helper_ask","harness":"codex","thread":"t2","root":"t1","malformed":"parent"}""",
        };

        var bound = ChildBindings.Fold(lines);

        Assert.Equal((0, 2, 0), (bound.RootCalls, bound.UnknownCalls, bound.Unjoined));
        Assert.Empty(bound.Children);
    }

    [Fact]
    public void Nothing_written_is_nothing_known_and_a_line_that_does_not_read_is_skipped()
    {
        var none = ChildBindings.Fold([]);
        var noise = ChildBindings.Fold(["not json", "[]", "{\"by\":42}", "{\"by\":\"call\",\"harness\":\"claude-code\",\"toolUse\":\"a\\\\b\"}", ""]);

        Assert.Null(none.Harness);
        Assert.Empty(none.Children);
        Assert.Empty(noise.Children);
        // The last noise line names an id that is not one: it is a call with no lineage that reads.
        Assert.Equal(1, noise.UnknownCalls);
    }

    [Fact]
    public void A_chat_that_went_on_after_an_end_before_row_a_keeps_what_the_end_said_of_its_children_and_binds_the_next_run_s_anew()
    {
        var lines = new[]
        {
            Hook("started", SiblingA), Started(SiblingA, null, 1), End(clean: true),
            Hook("started", SiblingB), Started(SiblingB, null, 1),
        };

        var bound = ChildBindings.Fold(lines, ChildBindings.Chat);

        Assert.Equal([(SiblingA, ChildBindings.Ended), (SiblingB, NotKnown)], bound.Children.Select(child => (child.Native, child.State)));
    }

    /// <summary>The twin table: each call line exactly as the connector's <c>LineageFile.Line</c> writes it.</summary>
    [Fact]
    public void The_connector_s_call_lines_read_as_its_tests_write_them()
    {
        var lines = new[]
        {
            """{"at":"2026-10-16T08:30:00.123Z","by":"call","tool":"knowledge_search","harness":"claude-code","toolUse":"toolu_1"}""",
            """{"at":"2026-10-16T08:30:00.123Z","by":"call","tool":"knowledge_get","harness":"codex","thread":"t2","parent":"t1","kind":"thread_spawn","source":"subagent","root":"t1"}""",
            """{"at":"2026-10-16T08:30:00.123Z","by":"call","tool":"daoris_me"}""",
            """{"at":"2026-10-16T08:30:00.123Z","by":"call"}""",
        };

        var bound = ChildBindings.Fold(lines);

        Assert.Equal(1, bound.Unjoined);
        Assert.Equal(2, bound.UnknownCalls);
        var child = Assert.Single(bound.Children);
        Assert.Equal(("codex", "t2", "t1", 1), (child.Harness, child.Native, child.Parent, child.Depth));
    }

    [Fact]
    public void Claude_s_stream_says_each_subagent_s_start_with_its_parent_and_its_end_and_nothing_else()
    {
        var agents = new HashSet<string>(StringComparer.Ordinal);
        var at = DateTimeOffset.UnixEpoch;
        string? Take(string line) => ChildBindings.StreamLine(line, at, agents);

        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"stream","event":"started","agent":"aadc0410bd5fa6a1e","depth":1,"type":"general-purpose","description":"Sibling A"}""",
            Take("""{"type":"system","subtype":"task_started","task_id":"aadc0410bd5fa6a1e","run_id":"r","tool_use_id":"toolu_01ANoLUxP1AZ2LrqzFZdMaVH","description":"Sibling A","subagent_type":"general-purpose","is_backgrounded":false,"spawn_depth":1,"task_type":"local_agent","prompt":"call it"}"""));
        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"stream","event":"started","agent":"a1b84f19758fd8ef5","parent":"aadc0410bd5fa6a1e","depth":2,"type":"general-purpose"}""",
            Take("""{"type":"system","subtype":"task_started","task_id":"a1b84f19758fd8ef5","tool_use_id":"toolu_015M","subagent_type":"general-purpose","spawn_depth":2,"parent_task_id":"aadc0410bd5fa6a1e","task_type":"local_agent"}"""));
        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"stream","event":"ended","agent":"a1b84f19758fd8ef5","status":"completed"}""",
            Take("""{"type":"system","subtype":"task_notification","task_id":"a1b84f19758fd8ef5","status":"completed","output_file":"x"}"""));

        // A background command is no subagent; its end is not a child's; and every other line is no lineage.
        Assert.Null(Take("""{"type":"system","subtype":"task_started","task_id":"b1","task_type":"local_bash","description":"npm test"}"""));
        Assert.Null(Take("""{"type":"system","subtype":"task_notification","task_id":"b1","status":"completed"}"""));
        Assert.Null(Take("""{"type":"assistant","message":{"content":[]}}"""));
        Assert.Null(Take("not json"));
    }

    [Fact]
    public void The_stream_s_start_keeps_the_task_s_description_to_its_first_line_bounded()
    {
        var agents = new HashSet<string>(StringComparer.Ordinal);
        var description = new string('d', 205) + "\nthe rest";

        var said = JsonNode.Parse(ChildBindings.StreamLine(
            $$"""{"type":"system","subtype":"task_started","task_id":"a1","description":{{JsonSerializer.Serialize(description)}},"spawn_depth":1,"task_type":"local_agent"}""",
            DateTimeOffset.UnixEpoch, agents)!)!;

        Assert.Equal(new string('d', 200), (string?)said["description"]);
        Assert.Null(JsonNode.Parse(ChildBindings.StreamLine(
            """{"type":"system","subtype":"task_started","task_id":"a2","description":"  ","spawn_depth":1,"task_type":"local_agent"}""",
            DateTimeOffset.UnixEpoch, agents)!)!["description"]);
    }

    [Fact]
    public void The_protocol_door_s_lines_name_the_child_its_label_its_place_and_the_word_its_wire_said()
    {
        var at = DateTimeOffset.UnixEpoch;

        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"acp","event":"started","agent":"a1","depth":1,"name":"Reader","task":"Read the README."}""",
            ChildBindings.AcpStarted(new LineageAnnouncement("a1", "Reader", "Read the README.") { ByRoot = true }, at));
        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"acp","event":"started","agent":"a2:generation:2","parent":"a1"}""",
            ChildBindings.AcpStarted(new LineageAnnouncement("a2:generation:2", null, null) { Parent = "a1" }, at));
        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"acp","event":"started","agent":"a3","name":"D:/fam/x"}""",
            ChildBindings.AcpStarted(new LineageAnnouncement("a3", "D:/fam/x", null) { Parent = "../up" }, at));
        Assert.Null(ChildBindings.AcpStarted(new LineageAnnouncement("../a4", "Bad", null) { ByRoot = true }, at));

        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"acp","event":"ended","agent":"a1","state":"completed"}""",
            ChildBindings.AcpEnded("a1", "completed", at));
        Assert.Equal("""{"at":"1970-01-01T00:00:00.000Z","by":"acp","event":"ended","agent":"a1"}""", ChildBindings.AcpEnded("a1", null, at));
        Assert.Equal("""{"at":"1970-01-01T00:00:00.000Z","by":"acp","event":"ended","agent":"a1"}""", ChildBindings.AcpEnded("a1", "no word/here", at));
        Assert.Null(ChildBindings.AcpEnded("a b", "completed", at));
    }

    [Fact]
    public void A_line_the_driver_writes_for_a_process_names_that_process_where_its_token_is_one()
    {
        var agents = new HashSet<string>(StringComparer.Ordinal);
        var at = DateTimeOffset.UnixEpoch;
        const string started = """{"type":"system","subtype":"task_started","task_id":"a1","spawn_depth":1,"task_type":"local_agent"}""";

        Assert.Equal("0123456789abcdef", (string?)JsonNode.Parse(ChildBindings.StreamLine(started, at, agents, "0123456789abcdef")!)!["proc"]);
        Assert.Equal("0123456789abcdef", (string?)JsonNode.Parse(ChildBindings.AcpStarted(new LineageAnnouncement("a1", null, null), at, "0123456789abcdef")!)!["proc"]);
        Assert.Equal("0123456789abcdef", (string?)JsonNode.Parse(ChildBindings.AcpEnded("a1", null, at, "0123456789abcdef")!)!["proc"]);
        Assert.Null(JsonNode.Parse(ChildBindings.StreamLine(started, at, agents, "not a token")!)!["proc"]);
        Assert.Null(JsonNode.Parse(ChildBindings.AcpEnded("a1", null, at)!)!["proc"]);
    }

    [Fact]
    public void A_process_s_start_names_it_and_the_daoris_process_that_issued_it_and_its_end_names_it()
    {
        var at = DateTimeOffset.UnixEpoch;

        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"start","proc":"0123456789abcdef","issuer":"4242 638000000000000000"}""",
            ChildBindings.StartLine("0123456789abcdef", "4242 638000000000000000", at));
        Assert.Null(ChildBindings.StartLine("not a token", "4242 638000000000000000", at));
        Assert.Equal(
            """{"at":"1970-01-01T00:00:00.000Z","by":"end","proc":"0123456789abcdef","clean":true}""",
            ChildBindings.EndLine(true, at, "0123456789abcdef"));
    }

    [Fact]
    public void The_end_line_says_whether_the_part_ended_clean()
    {
        Assert.Equal("""{"at":"1970-01-01T00:00:00.000Z","by":"end","clean":false}""", ChildBindings.EndLine(false, DateTimeOffset.UnixEpoch));
    }

    // Rule 5: a generation, recorded or history, closes only where its process's exit is proven, each proof drawing what it proves.

    [Fact]
    public void A_chat_file_from_before_row_a_closes_each_process_s_children_at_its_own_clean_end_and_a_late_stop_there_ends_its_run()
    {
        var bound = ChildBindings.Fold(
            [
                StreamAt("started", "a1", 1), HistoryEnd(true, 2), StreamAt("ended", "a1", 3, status: "completed"),
                StreamAt("started", "b1", 4), HistoryEnd(true, 5), StreamAt("started", "c1", 6), HistoryEnd(true, 7),
            ],
            ChildBindings.Chat);

        Assert.Equal(
            [("a1", ChildBindings.Completed, When(3)), ("b1", ChildBindings.Ended, When(5)), ("c1", ChildBindings.Ended, When(7))],
            bound.Children.Select(child => (child.Native, child.State, child.EndedAt)));
        Assert.Equal((When(4), true), (bound.Children[1].Since, bound.Children[1].StartSaid));
    }

    [Fact]
    public void An_old_chat_s_unclean_end_proves_nothing_so_its_child_reads_not_known_and_a_later_call_begins_a_later_run()
    {
        var story = new Story(ChildBindings.Chat)
            .Then(HookAt("started", "x1", 1)).Reads("x1", NotKnown)
            .Then(HookAt("call", "x1", 2)).Reads("x1", NotKnown)
            .Then(HistoryEnd(false, 3)).Reads("x1", NotKnown);
        Assert.Equal((When(2), (DateTimeOffset?)null), (story.Child("x1")!.LastSeen, story.Child("x1")!.EndedAt));
        story.Then(HookAt("call", "x1", 4)).Reads("x1", NotKnown);
        Assert.Equal((When(4), When(4), false), (story.Child("x1")!.LastSeen, story.Child("x1")!.Since, story.Child("x1")!.StartSaid));
        story.Then(StartAt(P2, 5)).Here(P2, TokenReading.Watched).Reads("x1", NotKnown)
            .Then(HookAt("started", "y1", 6, P2)).Reads("x1", NotKnown).Reads("y1", Working);

        var clean = new Story(ChildBindings.Chat).Then(HookAt("started", "x1", 1), HookAt("call", "x1", 2), HistoryEnd(true, 3)).Reads("x1", ChildBindings.Ended);
        Assert.Equal(When(3), clean.Child("x1")!.EndedAt);
    }

    [Fact]
    public void A_driven_record_s_file_from_before_row_a_claims_no_boundary_so_its_unended_child_reads_unrecorded()
    {
        string[] lines = [HookAt("started", "a1", 1), HookAt("call", "a1", 2), HookAt("started", "b1", 3), HookAt("stopped", "b1", 4), HistoryEnd(true, 9)];

        foreach (var kind in new[] { null, "driven" })
        {
            var bound = ChildBindings.Fold(lines, kind);
            Assert.Equal((ChildBindings.Unrecorded, (DateTimeOffset?)null, (DateTimeOffset?)At(2)), (bound.Children[0].State, bound.Children[0].EndedAt, bound.Children[0].LastSeen));
            Assert.Equal((ChildBindings.Finished, (DateTimeOffset?)At(4)), (bound.Children[1].State, bound.Children[1].EndedAt));
        }

        var chat = ChildBindings.Fold(lines, ChildBindings.Chat);
        Assert.Equal((ChildBindings.Ended, (DateTimeOffset?)At(9)), (chat.Children[0].State, chat.Children[0].EndedAt));
    }

    [Fact]
    public void A_history_generation_with_no_end_reads_not_known_and_the_file_s_first_start_closes_nothing()
    {
        new Story(ChildBindings.Chat)
            .Then(HookAt("started", "a1", 1)).Reads("a1", NotKnown)
            .Then(StartAt(P1, 2)).Here(P1, TokenReading.Watched).Reads("a1", NotKnown)
            .Then(HookAt("started", "b1", 3, P1)).Reads("a1", NotKnown).Reads("b1", Working);
    }

    [Fact]
    public void A_child_of_a_process_this_window_watches_works_reads_its_agent_ended_from_the_exit_and_its_end_s_word_once_that_lands()
    {
        Story Exited() => new Story()
            .Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched)
            .Then(StreamAt("started", "a1", 1, P1), HookAt("call", "a1", 2, P1)).Reads("a1", Working, elsewhere: NotKnown)
            .Here(P1, TokenReading.Gone).Reads("a1", AgentEnded, elsewhere: NotKnown);

        var gone = Exited().Child("a1")!;
        Assert.Equal(((DateTimeOffset?)At(2), (DateTimeOffset?)null), (gone.LastSeen, gone.EndedAt));
        var clean = Exited().Then(EndAt(P1, true, 5)).Reads("a1", ChildBindings.Ended, elsewhere: ChildBindings.Ended);
        Assert.Equal(When(5), clean.Child("a1")!.EndedAt);
        Exited().Then(EndAt(P1, false, 5)).Reads("a1", ChildBindings.Lost, elsewhere: ChildBindings.Lost);
    }

    [Fact]
    public void A_child_of_a_process_this_window_cannot_watch_reads_not_known_until_loop_tells_its_exit()
    {
        new Story()
            .Then(StartAt(P1, 0)).Here(P1, TokenReading.Unwatched).Then(StreamAt("started", "a1", 1, P1)).Reads("a1", NotKnown)
            .Here(P1, TokenReading.Gone).Reads("a1", AgentEnded)
            .Then(EndAt(P1, true, 3)).Reads("a1", ChildBindings.Ended);
    }

    [Fact]
    public void Two_starts_of_one_record_each_read_by_their_own_process_at_every_prefix()
    {
        Story Raced() => new Story()
            .Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched).Then(StreamAt("started", "a1", 1, P1)).Reads("a1", Working, NotKnown)
            .Then(StartAt(P2, 2)).Here(P2, TokenReading.Watched).Reads("a1", Working, NotKnown)
            .Then(StreamAt("started", "b1", 3, P2)).Reads("a1", Working, NotKnown).Reads("b1", Working, NotKnown)
            .Here(P2, TokenReading.Gone).Reads("a1", Working, NotKnown).Reads("b1", AgentEnded, NotKnown);

        Raced()
            .Then(EndAt(P2, false, 5)).Reads("a1", Working, NotKnown).Reads("b1", ChildBindings.Lost, ChildBindings.Lost)
            .Then(HookAt("call", "a1", 6, P1)).Reads("a1", Working, NotKnown)
            .Here(P1, TokenReading.Gone).Reads("a1", AgentEnded, NotKnown)
            .Then(EndAt(P1, true, 8)).Reads("a1", ChildBindings.Ended, ChildBindings.Ended).Reads("b1", ChildBindings.Lost, ChildBindings.Lost);
        // The second's end refused by the file: its agent ended for good, the first's unchanged.
        Raced()
            .Then(HookAt("call", "a1", 6, P1)).Reads("a1", Working, NotKnown).Reads("b1", AgentEnded, NotKnown)
            .Here(P1, TokenReading.Gone).Then(EndAt(P1, true, 8)).Reads("a1", ChildBindings.Ended).Reads("b1", AgentEnded, NotKnown);
    }

    [Fact]
    public void A_competitor_held_before_its_advance_reads_by_its_own_process_through_the_other_s_end_its_continuation_and_a_late_go_on()
    {
        new Story()
            .Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched).Then(StreamAt("started", "a1", 1, P1)).Reads("a1", Working)
            .Then(StartAt(P2, 2)).Here(P2, TokenReading.Watched).Then(StreamAt("started", "b1", 3, P2)).Reads("b1", Working, NotKnown)
            .Here(P1, TokenReading.Gone).Then(EndAt(P1, true, 4)).Reads("a1", ChildBindings.Ended).Reads("b1", Working, NotKnown)
            .Then(StartAt(P3, 5)).Here(P3, TokenReading.Watched).Then(StreamAt("started", "c1", 6, P3)).Reads("c1", Working).Reads("b1", Working, NotKnown)
            .Here(P3, TokenReading.Gone).Then(EndAt(P3, true, 7)).Reads("c1", ChildBindings.Ended).Reads("b1", Working, NotKnown)
            .Here(P2, TokenReading.Gone).Reads("b1", AgentEnded, NotKnown)
            .Then(EndAt(P2, false, 9)).Reads("b1", ChildBindings.Lost, ChildBindings.Lost);
    }

    [Fact]
    public void A_live_run_beside_a_competitor_s_end_and_a_go_on_s_start_works_until_its_exit_and_reads_not_known_once_its_window_is_gone()
    {
        var story = new Story()
            .Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched).Then(StreamAt("started", "a1", 1, P1))
            .Then(StartAt(P2, 2)).Here(P2, TokenReading.Watched).Then(StreamAt("started", "b1", 3, P2))
            .Here(P2, TokenReading.Gone).Then(EndAt(P2, false, 4)).Reads("a1", Working, NotKnown)
            .Then(StartAt(P3, 5)).Here(P3, TokenReading.Watched).Then(StreamAt("started", "g1", 6, P3)).Reads("a1", Working, NotKnown).Reads("g1", Working, NotKnown);

        // Its window crashed with its agent left running: a later window holds none of these tokens, and a call of a1 moves its last seen.
        var later = story.Then(HookAt("call", "a1", 7, P1)).Child("a1", elsewhere: true)!;
        Assert.Equal((NotKnown, (DateTimeOffset?)At(7)), (later.State, later.LastSeen));
    }

    [Fact]
    public void A_chat_s_clean_end_landing_after_its_go_on_s_start_closes_its_own_generation_and_the_go_on_s_start_closes_nothing()
    {
        var story = new Story(ChildBindings.Chat)
            .Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched).Then(StreamAt("started", "x1", 1, P1)).Here(P1, TokenReading.Gone)
            .Then(StartAt(P2, 2)).Here(P2, TokenReading.Watched).Then(StreamAt("started", "y1", 3, P2)).Reads("x1", AgentEnded, NotKnown).Reads("y1", Working, NotKnown)
            .Then(HookAt("call", "x1", 4, P1)).Reads("x1", AgentEnded, NotKnown);
        Assert.Equal(When(4), story.Child("x1")!.LastSeen);
        story.Then(EndAt(P1, true, 5)).Reads("x1", ChildBindings.Ended, ChildBindings.Ended).Reads("y1", Working, NotKnown);
        Assert.Equal(When(5), story.Child("x1")!.EndedAt);
    }

    // Rule 6: runs, and where a line is placed.

    [Fact]
    public void A_line_naming_its_process_is_placed_in_that_process_s_generation_wherever_it_sits_and_changes_that_generation_alone()
    {
        string[] first = [StartAt(P1, 0), StreamAt("started", "x1", 1, P1), EndAt(P1, false, 2), StartAt(P2, 3)];

        var late = new Story().Then(first).Here(P2, TokenReading.Watched).Then(StreamAt("started", "y1", 4, P1)).Reads("y1", ChildBindings.Lost);
        Assert.Equal(When(4), late.Child("y1")!.LastSeen);
        new Story().Then(first).Here(P2, TokenReading.Watched)
            .Then(StreamAt("started", "x1", 4, P2), StreamAt("ended", "x1", 5, P1, status: "completed")).Reads("x1", Working);
        new Story().Then(first).Here(P2, TokenReading.Watched).Then(StreamAt("ended", "x1", 5, P1, status: "completed")).Reads("x1", ChildBindings.Completed);
    }

    [Fact]
    public void From_the_file_s_first_start_a_line_with_no_process_or_one_no_start_names_is_unplaced_counted_once_and_moves_no_state()
    {
        var bound = new Story()
            .Then(HookAt("started", "h1", 0), StartAt(P1, 1), StartAt(P2, 2)).Here(P1, TokenReading.Watched).Here(P2, TokenReading.Watched)
            .Then(HookAt("started", "x1", 3, P2), HookAt("stopped", "x1", 4), HookAt("started", "y1", 5), HookAt("call", "y1", 6))
            .Then(HookAt("started", "z1", 7, "b000000000000009"))
            .Fold();

        Assert.Equal([("h1", NotKnown), ("x1", Working)], bound.Children.Select(child => (child.Native, child.State)));
        Assert.Equal(3, bound.Unplaced);
        Assert.Empty(ChildBindings.Fold([StartAt(P1, 0), HookAt("started", "y1", 1)], window: Watching(P1)).Children);
    }

    [Fact]
    public void An_open_codex_generation_reads_each_thread_unsaid_its_start_never_said()
    {
        var story = new Story().Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched).Then(CodexAt("t-root", null, 1, P1), CodexAt("t-a", "t-root", 2, P1));

        story.Reads("t-a", ChildBindings.Unsaid, NotKnown);
        Assert.Equal((false, When(2)), (story.Child("t-a")!.StartSaid, story.Child("t-a")!.Since));
    }

    [Fact]
    public void A_finished_child_started_again_works_again_and_a_late_call_reopens_nothing()
    {
        new Story().Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched)
            .Then(StreamAt("started", "a1", 1, P1), StreamAt("ended", "a1", 2, P1, status: "completed")).Reads("a1", ChildBindings.Completed)
            .Then(HookAt("call", "a1", 3, P1)).Reads("a1", ChildBindings.Completed)
            .Then(StreamAt("started", "a1", 4, P1)).Reads("a1", Working)
            .Then(StreamAt("ended", "a1", 5, P1, status: "completed"), StartAt(P2, 6)).Here(P2, TokenReading.Watched).Reads("a1", ChildBindings.Completed)
            .Then(StreamAt("started", "a1", 7, P2)).Reads("a1", Working);
    }

    [Fact]
    public void A_claude_agent_seen_only_by_its_calls_on_the_pipe_door_is_a_row_first_seen()
    {
        var child = new Story().Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched)
            .Then(HookCallAt("toolu_a", "a1", 1, P1), CallAt("toolu_a", 2, P1)).Child("a1")!;

        Assert.Equal((Working, false, (DateTimeOffset?)At(1), 1), (child.State, child.StartSaid, child.Since, child.Calls));
    }

    [Fact]
    public void A_call_joined_to_its_agent_only_by_an_unplaced_hook_line_is_that_agent_s_and_the_hook_line_moves_nothing()
    {
        var pipe = new Story().Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched).Then(HookCallAt("toolu_a", "a1", 1), CallAt("toolu_a", 2, P1)).Fold();
        var protocol = new Story().Then(StartAt(P1, 0)).Here(P1, TokenReading.Watched)
            .Then(AcpAt("started", "b1", 1, P1), HookCallAt("toolu_a", "a1", 2), CallAt("toolu_a", 3, P1)).Fold();

        var a1 = Assert.Single(pipe.Children);
        Assert.Equal(("a1", Working, (DateTimeOffset?)At(2), 1), (a1.Native, a1.State, a1.Since, pipe.Unplaced));
        Assert.Equal(["b1"], protocol.Children.Select(child => child.Native));
        Assert.Equal((1, 1), (protocol.Unmatched, protocol.Unplaced));
    }

    // Rule 7: the end's word and moment, and last seen.

    [Fact]
    public void The_wire_s_word_wins_over_the_hook_s_stop_whatever_their_order()
    {
        string[] before = [StartAt(P1, 0), AcpAt("started", "a1", 1, P1), HookAt("stopped", "a1", 2, P1), AcpAt("ended", "a1", 3, P1, state: "failed")];
        string[] after = [StartAt(P1, 0), AcpAt("started", "a1", 1, P1), AcpAt("ended", "a1", 2, P1, state: "failed"), HookAt("stopped", "a1", 3, P1)];

        Assert.All(
            new[] { before, after },
            lines => Assert.Equal((ChildBindings.Failed, (DateTimeOffset?)At(2)), (Assert.Single(ChildBindings.Fold(lines).Children).State, ChildBindings.Fold(lines).Children[0].EndedAt)));
    }

    [Fact]
    public void Stopped_is_a_word_and_an_end_with_no_word_or_one_none_of_the_five_reads_finished()
    {
        var pipe = ChildBindings.Fold(
        [
            StartAt(P1, 0),
            StreamAt("started", "a1", 1, P1), StreamAt("ended", "a1", 2, P1, status: "stopped"),
            StreamAt("started", "a2", 3, P1), StreamAt("ended", "a2", 4, P1, status: "exploded"),
        ]);
        var protocol = ChildBindings.Fold(
        [
            StartAt(P1, 0),
            AcpAt("started", "a3", 5, P1), AcpAt("ended", "a3", 6, P1),
            AcpAt("started", "a4", 7, P1), AcpAt("ended", "a4", 8, P1, state: "disconnected"),
        ]);

        Assert.Equal(
            [ChildBindings.Stopped, ChildBindings.Finished, ChildBindings.Finished, ChildBindings.Disconnected],
            pipe.Children.Concat(protocol.Children).Select(child => child.State));
    }

    [Fact]
    public void Last_seen_moves_only_while_its_run_is_open()
    {
        string[] closed = [StartAt(P1, 0), StreamAt("started", "a1", 1, P1), HookAt("call", "a1", 2, P1), EndAt(P1, false, 3), StartAt(P2, 4)];
        static (string, DateTimeOffset?, DateTimeOffset?, DateTimeOffset?) Read(IEnumerable<string> lines, string? kind = null)
        {
            var child = ChildBindings.Fold(lines, kind).Children.Single();
            return (child.State, child.Since, child.EndedAt, child.LastSeen);
        }

        // A delayed call of the first process after its own end and the second's start moves nothing.
        Assert.Equal(Read(closed), Read([.. closed, HookAt("call", "a1", 5, P1)]));
        // A call before the line that closed an unrecorded run moves its last seen; a not known run's moves with each later line.
        Assert.Equal(When(4), Read([HookAt("started", "a1", 1), HookAt("call", "a1", 4), HistoryEnd(true, 5)]).Item4);
        Assert.Equal(When(6), Read([StartAt(P1, 0), StreamAt("started", "a1", 1, P1), HookAt("call", "a1", 6, P1)]).Item4);
    }

    // Rule 8: the join.

    [Fact]
    public void On_a_part_with_announced_children_a_hook_id_joins_by_equal_id_or_generation_suffix_and_the_rest_are_counted_once()
    {
        var bound = ChildBindings.Fold(
        [
            StartAt(P1, 0), AcpAt("started", "a1", 1, P1), AcpAt("started", "a2:generation:2", 2, P1),
            HookAt("started", "a1", 3, P1), HookAt("started", "a2", 4, P1),
            HookAt("started", "h9", 5, P1), HookAt("call", "h9", 6, P1), HookAt("stopped", "h9", 7, P1),
        ], window: Watching(P1));

        Assert.Equal(["a1", "a2:generation:2"], bound.Children.Select(child => child.Native));
        Assert.All(bound.Children, child =>
        {
            Assert.Equal(Working, child.State);
            Assert.Equal(["hook", "acp"], child.Provenance);
        });
        Assert.Equal(1, bound.Unmatched);
    }

    [Fact]
    public void A_codex_part_with_announced_children_draws_its_rows_from_them_each_child_one_row_and_counts_the_threads_none_matched()
    {
        var bound = ChildBindings.Fold(
            [StartAt(P1, 0), AcpAt("started", "t-a", 1, P1), CodexAt("t-root", null, 2, P1), CodexAt("t-a", "t-root", 3, P1), CodexAt("t-b", "t-root", 4, P1)],
            window: Watching(P1));

        var child = Assert.Single(bound.Children);
        Assert.Equal(("t-a", "codex", Working, 1), (child.Native, child.Harness, child.State, child.Calls));
        Assert.Equal(1, bound.Unmatched);
    }

    /// <summary>
    /// COWORKVIEW1a's rule 13: a line appended after a reading wakes the read only where it moves what that reading holds; a call
    /// naming the root, a held working child, a counted unmatched or unplaced id moves nothing.
    /// </summary>
    [Fact]
    public void An_appended_line_wakes_the_read_only_where_it_moves_what_the_reading_holds()
    {
        string[] lines = [StartAt(P1, 0), HookAt("started", SiblingA, 1, P1), HookCallAt("t1", SiblingA, 2, P1), HookCallAt("t9", Grandchild, 3)];
        var read = ChildBindings.Fold(lines, window: Watching(P1));

        Assert.False(read.Wakes(HookCall("t2", null, P1)));
        Assert.False(read.Wakes(Call("t2", P1)));
        Assert.False(read.Wakes(HookCallAt("t3", SiblingA, 4, P1)));
        Assert.True(read.Wakes(HookCallAt("t4", SiblingB, 5, P1)));
        Assert.True(read.Wakes(StartAt(P2, 6)));
        Assert.True(read.Wakes(HookAt("stopped", SiblingA, 7, P1)));
        Assert.True(read.Wakes(EndAt(P1, clean: true, 8)));
        // Tied to no process: a new id is counted once, one counted already moves nothing.
        Assert.False(read.Wakes(HookCallAt("t5", Grandchild, 9)));
        Assert.True(read.Wakes(HookCallAt("t6", SiblingB, 10)));
        // A child read not known: its call moves its last seen.
        Assert.True(ChildBindings.Fold(lines).Wakes(HookCallAt("t7", SiblingA, 11, P1)));

        var codex = ChildBindings.Fold([StartAt(P1, 0), CodexAt("t-root", null, 1, P1), CodexAt("t-1", "t-root", 2, P1)], window: Watching(P1));
        Assert.False(codex.Wakes(CodexAt("t-other", null, 3, P1)));
        Assert.False(codex.Wakes(CodexAt("t-1", "t-root", 4, P1)));
        Assert.True(codex.Wakes(CodexAt("t-1", null, 5, P1)));
        Assert.True(codex.Wakes(CodexAt("t-2", "t-root", 6, P1)));
    }
}
