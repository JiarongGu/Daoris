using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// The connector reads every call's <c>_meta</c> before its tool runs, writes what its harness said of the call's lineage to the
/// session's lineage file, and refuses no call by who made it: a subagent's call counts as its session's. A server over in-memory
/// streams, as <see cref="DiscoverProbeTests"/> runs one, with the lineage filter the host composes and a stand-in for every
/// tool the host registers.
/// </summary>
public sealed class CallLineageFilterTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-filter-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Every registered tool's name, and one the registration does not name.</summary>
    private static readonly IReadOnlyList<string> Tools = [.. ToolClassesTests.Advertised(), "echo"];

    private const string Agent = "a6aa3d41bcf080f4e";

    private string LineagePath => DaorisHome.Entry(_home, HomeEntry.Sessions, "s1", LineageFile.FileName);

    /// <summary>A hook line, as the driver's lineage hook writes it, for a Claude call made by <paramref name="agent"/> or the root.</summary>
    private void Hooked(string toolUse, string? agent)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LineagePath)!);
        var line = new JsonObject { ["by"] = "hook", ["event"] = "call", ["agent"] = agent, ["toolUse"] = toolUse, ["session"] = "c1" };
        if (agent is null) line.Remove("agent");
        File.AppendAllText(LineagePath, line.ToJsonString() + "\n");
    }

    private static JsonObject Claude(string toolUse) => new() { ["claudecode/toolUseId"] = toolUse };

    /// <summary>Codex's turn metadata as its protocol door writes it: a thread and its root, and no source.</summary>
    private static JsonObject Codex(string thread, string root = "t-root") =>
        new() { ["x-codex-turn-metadata"] = new JsonObject { ["session_id"] = root, ["thread_id"] = thread }.ToJsonString() };

    private static JsonObject Call(string tool, JsonObject? meta = null, JsonObject? arguments = null)
    {
        var call = new JsonObject { ["name"] = tool, ["arguments"] = arguments ?? new JsonObject() };
        if (meta is not null) call["_meta"] = meta;
        return call;
    }

    [Fact]
    public async Task Each_call_s_lineage_is_written_before_its_tool_answers_and_the_answer_is_unchanged()
    {
        var answers = await CallAsync(
            new LineageFile(_home, "s1"),
            Call("knowledge_search", new JsonObject { ["claudecode/toolUseId"] = "toolu_child", ["progressToken"] = 1 }, new JsonObject { ["thread_id"] = "forged" }),
            Call("knowledge_search", new JsonObject
            {
                ["x-codex-turn-metadata"] = """{"session_id":"t1","thread_id":"t2","parent_thread_id":"t1","subagent_kind":"thread_spawn","thread_source":"subagent"}""",
            }),
            Call("knowledge_search"));

        Assert.All(answers, answer => Assert.Equal((false, "ran knowledge_search"), answer));
        var lines = File.ReadAllLines(LineagePath).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(("claude-code", "toolu_child"), ((string)lines[0]["door"]!, (string)lines[0]["toolUse"]!));
        // An id in the arguments is the model's, never the harness's: it is not read.
        Assert.Null(lines[0]["thread"]);
        Assert.Equal(("codex", "t2", "t1"), ((string)lines[1]["door"]!, (string)lines[1]["thread"]!, (string)lines[1]["parent"]!));
        Assert.Null(lines[2]["door"]);
        Assert.All(lines, line => Assert.Equal("knowledge_search", (string)line["tool"]!));
    }

    /// <summary>
    /// 🔴 Every caller's every call runs: a child on either harness, a forged id in the arguments, a root, a call whose lineage cannot
    /// be read, and a tool no class names. What the harness said is only recorded, for the driver to bind a child to its session's part.
    /// </summary>
    [Fact]
    public async Task Every_caller_is_served_every_tool()
    {
        Hooked("toolu_root", null);
        Hooked("toolu_child", Agent);
        var forged = new JsonObject { ["thread_id"] = "t-root", ["session_id"] = "t-root", ["claudecode/toolUseId"] = "toolu_root" };
        var calls = new List<JsonObject>();
        foreach (var tool in Tools)
        {
            calls.Add(Call(tool, Codex("t-a")));
            calls.Add(Call(tool, Claude("toolu_child"), forged.DeepClone().AsObject()));
            calls.Add(Call(tool, Codex("t-root")));
            calls.Add(Call(tool, Claude("toolu_root")));
            calls.Add(Call(tool, Claude("toolu_unjoined")));
            calls.Add(Call(tool, new JsonObject { ["x-codex-turn-metadata"] = """{"session_id":"t-root","thread_id":"t-root","parent_thread_id":"a b"}""" }));
            calls.Add(Call(tool));
        }

        var answers = await CallAsync(new LineageFile(_home, "s1"), [.. calls]);

        Assert.Equal(Tools.SelectMany(tool => Enumerable.Repeat((false, "ran " + tool), 7)), answers);
    }

    /// <summary>🔴 A child's full <c>quest_publish</c> passes: attachments, a chain, requirements, a review choice, proposals, an ask as its sender, and as an intake's own.</summary>
    [Fact]
    public async Task A_child_s_full_quest_publish_passes()
    {
        JsonObject Full() => new()
        {
            ["from"] = AskDesk.SenderOf("a1"), ["to"] = "service", ["title"] = "A question", ["body"] = "Why.",
            ["attachments"] = new JsonArray("shot.png"),
            ["then"] = new JsonArray(new JsonObject { ["to"] = "web", ["title"] = "t", ["body"] = "b" }),
            ["requirements"] = new JsonArray(new JsonObject { ["quote"] = "q", ["check"] = "c" }),
            ["review"] = new JsonObject { ["choice"] = "on" },
            ["reviewProposal"] = new JsonObject { ["choice"] = "on", ["reason"] = "r" },
            ["workflowProposal"] = new JsonObject { ["kind"] = "bug", ["reason"] = "r" },
        };

        var answers = await CallAsync(new LineageFile(_home, "s1"), Call("quest_publish", Codex("t-a"), Full()));
        var asAsk = await CallAsync(new LineageFile(_home, "s1"), new IntakeScope("a1", "s1"), Call("quest_publish", Codex("t-a"), Full()));

        Assert.Equal([(false, "ran quest_publish")], answers.Concat(asAsk).Distinct());
    }

    /// <summary>The host's own composition records it, so what the tests above hold is what a session's connector does.</summary>
    [Fact]
    public void The_host_records_every_call_s_lineage()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.Contains(".RecordCallLineage(LineageFile.FromEnvironment())", program);
    }

    private Task<List<(bool Error, string Text)>> CallAsync(LineageFile file, params JsonObject[] calls) => CallAsync(file, null, calls);

    private async Task<List<(bool Error, string Text)>> CallAsync(LineageFile file, IntakeScope? intake, params JsonObject[] calls)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        if (intake is not null) builder.Services.AddSingleton(intake);
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .WithTools(Tools.Select(name => McpServerTool.Create(() => "ran " + name, new McpServerToolCreateOptions { Name = name, Description = "Stands in." })))
            .RecordCallLineage(file);

        var answers = new List<(bool, string)>();
        using (var host = builder.Build())
        {
            await host.StartAsync();
            using var reader = new StreamReader(fromServer.Reader.AsStream());

            async Task<JsonNode> Send(JsonObject message)
            {
                await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes(message.ToJsonString() + "\n"));
                var line = await reader.ReadLineAsync().WaitAsync(Poll.Patience);
                return JsonNode.Parse(line!)!;
            }

            await Send(new JsonObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 0, ["method"] = "initialize",
                ["params"] = new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject { ["name"] = "test", ["version"] = "0" },
                },
            });
            await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"notifications/initialized"}""" + "\n"));
            var id = 1;
            foreach (var call in calls)
            {
                var answer = await Send(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id++, ["method"] = "tools/call", ["params"] = call });
                var result = answer["result"]!;
                answers.Add(((bool?)result["isError"] ?? false, (string)result["content"]![0]!["text"]!));
            }

            await toServer.Writer.CompleteAsync();
            await host.StopAsync();
        }

        return answers;
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
