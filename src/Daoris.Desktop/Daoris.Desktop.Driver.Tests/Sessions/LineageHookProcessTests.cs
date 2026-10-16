using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-C1a: the lineage hook as Claude Code runs it, node and the shipped script with the hook's input on stdin. It writes
/// the ids the input names and none of its paths, decides nothing, and exits 0 on an input it cannot read.
/// </summary>
/// <remarks>The inputs carry the keys Claude Code 2.1.299 handed the probe kit's logger (FABRIC-PROBE1).</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LineageHookProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-hook-" + Guid.NewGuid().ToString("N")[..8]);

    public LineageHookProcessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>Spelled in pieces, as a test's example path is.</summary>
    private static readonly string Place = string.Join('\\', "C:", "work", "repo");

    private (int Exit, string Out) Run(LineageHook hook, string input)
    {
        var info = new ProcessStartInfo(hook.Node)
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
        };
        info.ArgumentList.Add(hook.Script);
        info.ArgumentList.Add(hook.File);
        using var process = Process.Start(info)!;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);
        return (process.ExitCode, output);
    }

    [Fact]
    public void Each_event_is_one_line_of_ids_with_no_path_and_no_decision()
    {
        var hook = ChildLineage.HookFor(_home, "s1")!;
        var common = new JsonObject
        {
            ["session_id"] = "858a71dc-99cf-4136-8787-da86714459d2", ["prompt_id"] = "b8e70889-02d3-4e8d-9e2e-535f0ee9f272",
            ["cwd"] = Place, ["transcript_path"] = Place + "\\t.jsonl", ["permission_mode"] = "acceptEdits",
        };
        JsonObject With(JsonObject more)
        {
            var input = common.DeepClone().AsObject();
            foreach (var (name, value) in more) input[name] = value?.DeepClone();
            return input;
        }

        var said = new[]
        {
            Run(hook, With(new JsonObject
            {
                ["hook_event_name"] = "PreToolUse", ["tool_name"] = "mcp__daoris-knowledge__knowledge_search", ["tool_use_id"] = "toolu_1",
                ["agent_id"] = "a6aa3d41bcf080f4e", ["agent_type"] = "general-purpose", ["tool_input"] = new JsonObject { ["query"] = Place },
                ["mcp_server"] = new JsonObject { ["name"] = "daoris-knowledge", ["source"] = "dynamic" },
            }).ToJsonString()),
            Run(hook, With(new JsonObject { ["hook_event_name"] = "SubagentStart", ["agent_id"] = "a6aa3d41bcf080f4e", ["agent_type"] = "general-purpose" }).ToJsonString()),
            Run(hook, With(new JsonObject
            {
                ["hook_event_name"] = "SubagentStop", ["agent_id"] = "a6aa3d41bcf080f4e", ["agent_type"] = "general-purpose",
                ["agent_transcript_path"] = Place, ["last_assistant_message"] = "done",
            }).ToJsonString()),
            Run(hook, With(new JsonObject { ["hook_event_name"] = "Stop" }).ToJsonString()),
            Run(hook, "not json"),
        };

        Assert.All(said, run => Assert.Equal((0, ""), run));
        var text = File.ReadAllText(hook.File);
        Assert.DoesNotContain("work", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!.AsObject()).ToList();
        Assert.Equal(["call", "started", "stopped"], lines.Select(line => (string)line["event"]!));
        Assert.All(lines, line => Assert.Equal("hook", (string)line["by"]!));
        Assert.Equal(
            ["at", "by", "event", "agent", "type", "toolUse", "tool", "session"],
            lines[0].Select(pair => pair.Key));
        Assert.Equal(("a6aa3d41bcf080f4e", "toolu_1"), ((string)lines[0]["agent"]!, (string)lines[0]["toolUse"]!));

        var child = Assert.Single(ChildLineage.Read(_home, "s1").Children);
        Assert.Equal((ChildBindings.Finished, "general-purpose"), (child.State, child.Type));
    }

    [Fact]
    public void A_root_s_call_names_no_agent_and_the_part_s_root_is_its_session()
    {
        var hook = ChildLineage.HookFor(_home, "s1")!;

        Run(hook, """{"hook_event_name":"PreToolUse","tool_name":"mcp__daoris-knowledge__knowledge_search","tool_use_id":"toolu_r","session_id":"c1"}""");
        ChildLineage.Append(_home, "s1", """{"by":"call","tool":"knowledge_search","harness":"claude-code","toolUse":"toolu_r"}""");

        var bound = ChildLineage.Read(_home, "s1");
        Assert.Equal(("c1", 1, 0), (bound.Root, bound.RootCalls, bound.Unjoined));
        Assert.Empty(bound.Children);
    }
}
