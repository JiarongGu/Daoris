using System.Text.Json.Nodes;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-C1a (D173, its FABRIC-C0 note; <c>docs/2026-10-16-fabric-contract-audit.md</c> §2): the lineage a call's <c>_meta</c>
/// carries, as each harness wrote it, read by the connector on every call. Claude Code names the call (<c>claudecode/toolUseId</c>),
/// which its hook joins to the agent that made it; Codex names the thread, its parent and the spawn kind
/// (<c>x-codex-turn-metadata</c>, as JSON text or an object). Only ids are kept: the workspace's path Codex sends beside them,
/// and any value shaped like a path, never.
/// </summary>
/// <remarks>The rows are the C0 audit's and FABRIC-PROBE1's receipts, shaped as the harnesses wrote them.</remarks>
public sealed class CallLineageTests
{
    /// <summary>Spelled in pieces, as a test's example path is (the merge's universal gate refuses a literal one).</summary>
    private static readonly string Workspace = string.Join('\\', "C:", "work", "repo");

    private static JsonObject Meta(string json) => JsonNode.Parse(json)!.AsObject();

    private static JsonObject CodexMeta(JsonObject turn, bool asText) => new()
    {
        ["x-codex-turn-metadata"] = asText ? JsonValue.Create(turn.ToJsonString()) : turn,
        ["progressToken"] = 3,
    };

    private static JsonObject Turn(string thread, string? parent) => new()
    {
        ["session_id"] = "019a-root",
        ["thread_id"] = thread,
        ["parent_thread_id"] = parent,
        ["forked_from_thread_id"] = null,
        ["subagent_kind"] = parent is null ? null : "thread_spawn",
        ["thread_source"] = parent is null ? "user" : "subagent",
        ["workspaces"] = new JsonObject { [Workspace] = new JsonObject { ["commit"] = "abc123" } },
        ["cwd"] = Workspace,
    };

    [Fact]
    public void Claude_names_each_call_by_its_tool_use_id()
    {
        var lineage = CallLineage.Read(Meta("""{"claudecode/toolUseId":"toolu_01Jeu3xsam2ZQRW7nWaYpkuq","progressToken":2}"""));

        Assert.Equal(new CallLineage(CallLineage.Claude) { ToolUse = "toolu_01Jeu3xsam2ZQRW7nWaYpkuq" }, lineage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Codex_names_the_thread_its_parent_and_the_spawn_whether_its_metadata_is_text_or_an_object(bool asText)
    {
        var child = CallLineage.Read(CodexMeta(Turn("019a-child", "019a-root"), asText));
        var root = CallLineage.Read(CodexMeta(Turn("019a-root", null), asText));

        Assert.Equal(
            new CallLineage(CallLineage.Codex)
            {
                Thread = "019a-child", Parent = "019a-root", Kind = "thread_spawn", Source = "subagent", Root = "019a-root",
            },
            child);
        Assert.Equal(new CallLineage(CallLineage.Codex) { Thread = "019a-root", Source = "user", Root = "019a-root" }, root);
    }

    [Fact]
    public void The_workspace_path_codex_sends_beside_the_ids_is_never_kept()
    {
        var lineage = CallLineage.Read(CodexMeta(Turn("019a-child", "019a-root"), asText: true))!;
        var line = LineageFile.Line("knowledge_search", lineage, DateTimeOffset.UnixEpoch);

        Assert.DoesNotContain("work", line, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", line, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("C:\\work\\repo")]
    [InlineData("/srv/work/repo")]
    [InlineData("..")]
    [InlineData("a b")]
    [InlineData("")]
    public void A_value_that_is_not_an_id_is_not_kept(string value)
    {
        var turn = Turn("019a-child", "019a-root");
        turn["thread_id"] = value;
        turn["parent_thread_id"] = value;

        var lineage = CallLineage.Read(CodexMeta(turn, asText: false))!;
        var claude = CallLineage.Read(new JsonObject { ["claudecode/toolUseId"] = value });

        Assert.Null(lineage.Thread);
        Assert.Null(lineage.Parent);
        Assert.Null(claude);
    }

    [Fact]
    public void A_call_whose_meta_names_neither_harness_has_no_lineage()
    {
        Assert.Null(CallLineage.Read(null));
        Assert.Null(CallLineage.Read(new JsonObject()));
        Assert.Null(CallLineage.Read(Meta("""{"progressToken":1}""")));
        Assert.Null(CallLineage.Read(Meta("""{"x-codex-turn-metadata":"not json"}""")));
        Assert.Null(CallLineage.Read(Meta("""{"x-codex-turn-metadata":42}""")));
    }

    [Fact]
    public void Each_line_is_one_object_with_the_ids_it_has_and_nothing_else()
    {
        var at = new DateTimeOffset(2026, 10, 16, 8, 30, 0, 123, TimeSpan.Zero);

        Assert.Equal(
            """{"at":"2026-10-16T08:30:00.123Z","by":"call","tool":"knowledge_search","harness":"claude-code","toolUse":"toolu_1"}""",
            LineageFile.Line("knowledge_search", new CallLineage(CallLineage.Claude) { ToolUse = "toolu_1" }, at));
        Assert.Equal(
            """{"at":"2026-10-16T08:30:00.123Z","by":"call","tool":"knowledge_get","harness":"codex","thread":"t2","parent":"t1","kind":"thread_spawn","source":"subagent","root":"t1"}""",
            LineageFile.Line(
                "knowledge_get",
                new CallLineage(CallLineage.Codex) { Thread = "t2", Parent = "t1", Kind = "thread_spawn", Source = "subagent", Root = "t1" },
                at));
        // No lineage is said as none: a call the connector served whose harness wrote nothing it reads (canary 9).
        Assert.Equal("""{"at":"2026-10-16T08:30:00.123Z","by":"call","tool":"daoris_me"}""", LineageFile.Line("daoris_me", null, at));
        Assert.Equal("""{"at":"2026-10-16T08:30:00.123Z","by":"call"}""", LineageFile.Line("a tool\\name", null, at));
    }
}
