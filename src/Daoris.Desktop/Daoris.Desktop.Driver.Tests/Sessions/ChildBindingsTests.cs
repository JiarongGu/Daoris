using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-C1a (D173, its FABRIC-C0 note; <c>docs/2026-10-17-fabric-contract-audit.md</c> §2): a part's native children, bound
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

    private static string Line(JsonObject line)
    {
        line["at"] ??= "2026-10-16T08:30:00.000Z";
        return line.ToJsonString();
    }

    private static string Call(string toolUse) =>
        Line(new JsonObject { ["by"] = "call", ["tool"] = "knowledge_search", ["harness"] = "claude-code", ["toolUse"] = toolUse });

    private static string HookCall(string toolUse, string? agent) => Line(new JsonObject
    {
        ["by"] = "hook", ["event"] = "call", ["agent"] = agent, ["type"] = agent is null ? null : "general-purpose",
        ["toolUse"] = toolUse, ["tool"] = Tool, ["session"] = Session,
    });

    private static string Hook(string @event, string agent) =>
        Line(new JsonObject { ["by"] = "hook", ["event"] = @event, ["agent"] = agent, ["type"] = "general-purpose", ["session"] = Session });

    private static string Started(string agent, string? parent, int depth) => Line(new JsonObject
    {
        ["by"] = "stream", ["event"] = "started", ["agent"] = agent, ["parent"] = parent, ["depth"] = depth, ["type"] = "general-purpose",
    });

    private static string Ended(string agent, string status = "completed") =>
        Line(new JsonObject { ["by"] = "stream", ["event"] = "ended", ["agent"] = agent, ["status"] = status });

    private static string End(bool clean) => ChildBindings.EndLine(clean, DateTimeOffset.UnixEpoch);

    /// <summary>The probe's family on Claude Code: the root, two siblings, and a grandchild under the first.</summary>
    private static IEnumerable<string> ClaudeFamily(bool stops)
    {
        yield return HookCall("toolu_root", agent: null);
        yield return Call("toolu_root");
        yield return Hook("started", SiblingA);
        yield return Started(SiblingA, parent: null, depth: 1);
        yield return HookCall("toolu_a", SiblingA);
        yield return Call("toolu_a");
        yield return Hook("started", Grandchild);
        yield return Started(Grandchild, parent: SiblingA, depth: 2);
        yield return HookCall("toolu_g", Grandchild);
        yield return Call("toolu_g");
        if (stops) yield return Hook("stopped", Grandchild);
        if (stops) yield return Ended(Grandchild);
        if (stops) yield return Hook("stopped", SiblingA);
        yield return Hook("started", SiblingB);
        yield return Started(SiblingB, parent: null, depth: 1);
        yield return HookCall("toolu_b", SiblingB);
        yield return Call("toolu_b");
        if (stops) yield return Hook("stopped", SiblingB);
    }

    [Fact]
    public void Claude_tells_a_child_a_grandchild_and_two_siblings_apart_bound_to_the_part()
    {
        var bound = ChildBindings.Fold(ClaudeFamily(stops: false));

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
            Assert.Equal(ChildBindings.Active, child.State);
            Assert.Equal(1, child.Calls);
        });
        Assert.False(bound.PartEnded);
    }

    [Fact]
    public void A_killed_root_marks_the_children_still_running_lost_and_one_that_stopped_before_it_finished()
    {
        var lines = ClaudeFamily(stops: false).Append(Hook("stopped", Grandchild)).Append(End(clean: false));

        var bound = ChildBindings.Fold(lines);

        Assert.True(bound.PartEnded);
        Assert.False(bound.Clean);
        Assert.Equal(
            [(SiblingA, ChildBindings.Lost), (Grandchild, ChildBindings.Finished), (SiblingB, ChildBindings.Lost)],
            bound.Children.Select(child => (child.Native, child.State)));
    }

    [Fact]
    public void A_clean_end_ends_the_children_with_the_part_and_their_own_stop_says_finished()
    {
        var stopped = ChildBindings.Fold(ClaudeFamily(stops: true).Append(End(clean: true)));
        var unsaid = ChildBindings.Fold(ClaudeFamily(stops: false).Append(End(clean: true)));

        Assert.All(stopped.Children, child => Assert.Equal(ChildBindings.Finished, child.State));
        Assert.All(unsaid.Children, child => Assert.Equal(ChildBindings.Ended, child.State));
        Assert.True(unsaid.Clean);
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

        var bound = ChildBindings.Fold(lines);

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
            // Codex says no child's end: each ended with the part, never "finished" (canary 9).
            Assert.Equal(ChildBindings.Ended, child.State);
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
    public void A_part_that_goes_on_after_an_end_keeps_what_the_end_said_of_its_children_and_binds_the_next_run_s_anew()
    {
        var lines = new[]
        {
            Hook("started", SiblingA), Started(SiblingA, null, 1), End(clean: true),
            Hook("started", SiblingB), Started(SiblingB, null, 1),
        };

        var bound = ChildBindings.Fold(lines);

        Assert.False(bound.PartEnded);
        Assert.Equal([(SiblingA, ChildBindings.Ended), (SiblingB, ChildBindings.Active)], bound.Children.Select(child => (child.Native, child.State)));
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
            """{"at":"1970-01-01T00:00:00.000Z","by":"stream","event":"started","agent":"aadc0410bd5fa6a1e","depth":1,"type":"general-purpose"}""",
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
    public void The_end_line_says_whether_the_part_ended_clean()
    {
        Assert.Equal("""{"at":"1970-01-01T00:00:00.000Z","by":"end","clean":false}""", ChildBindings.EndLine(false, DateTimeOffset.UnixEpoch));
    }
}
