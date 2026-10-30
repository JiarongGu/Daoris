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
/// FABRIC-C1a: the connector reads every call's <c>_meta</c> before its tool runs, and writes what its harness said of the call's
/// lineage to the session's lineage file. FABRIC-C2a (D173's FABRIC-C2a note): for a connector that names a session, a child's
/// call, or one whose lineage cannot be read, is refused every tool but the reads and a plain quest. A server over in-memory
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

    /// <summary>Every registered tool's name, and one the table does not class.</summary>
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

    /// <summary>Codex's turn metadata as its protocol door writes it: a thread and its root, and no source (D173's 2026-10-31 note).</summary>
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
        Assert.Equal(("claude-code", "toolu_child"), ((string)lines[0]["harness"]!, (string)lines[0]["toolUse"]!));
        // An id in the arguments is the model's, never the harness's: it is not read.
        Assert.Null(lines[0]["thread"]);
        Assert.Equal(("codex", "t2", "t1"), ((string)lines[1]["harness"]!, (string)lines[1]["thread"]!, (string)lines[1]["parent"]!));
        Assert.Null(lines[2]["harness"]);
        Assert.All(lines, line => Assert.Equal("knowledge_search", (string)line["tool"]!));
    }

    /// <summary>
    /// The row's proof and canary 3: a stand-in harness's child, on either harness, is served each read and refused every other
    /// tool, a tool no class names among them; the refusal is an error that says nothing was done and that the reads remain.
    /// </summary>
    [Fact]
    public async Task A_stand_in_child_is_served_each_read_and_refused_each_restricted_tool()
    {
        var restricted = Tools.Where(tool => ToolClasses.Of(tool) is not (ToolClass.Read or ToolClass.Publish)).ToList();
        Assert.Contains("echo", restricted);
        var calls = new List<JsonObject>();
        foreach (var tool in Tools)
        {
            calls.Add(Call(tool, Codex("t-a")));
            Hooked("toolu_" + tool, Agent);
            calls.Add(Call(tool, Claude("toolu_" + tool)));
        }

        var answers = await CallAsync(new LineageFile(_home, "s1"), [.. calls]);

        for (var i = 0; i < Tools.Count; i++)
        {
            var tool = Tools[i];
            foreach (var (error, text) in answers.Skip(i * 2).Take(2))
            {
                if (!restricted.Contains(tool))
                {
                    Assert.Equal((false, "ran " + tool), (error, text));
                    continue;
                }

                Assert.True(error, tool);
                Assert.StartsWith($"Nothing was done: `{tool}` was not run. This call came from a subagent", text);
                Assert.Contains("knowledge_search", text);
                Assert.Contains("quest", text);
            }
        }
    }

    /// <summary>The row's proof: an id in the arguments, which the model writes, changes nothing; only the harness's <c>_meta</c> is read.</summary>
    [Fact]
    public async Task A_forged_id_in_the_arguments_changes_nothing()
    {
        Hooked("toolu_root", null);
        Hooked("toolu_child", Agent);
        var forged = new JsonObject
        {
            ["thread_id"] = "t-root", ["session_id"] = "t-root", ["thread_source"] = "user", ["claudecode/toolUseId"] = "toolu_root",
            ["_meta"] = new JsonObject { ["claudecode/toolUseId"] = "toolu_root" },
        };

        var answers = await CallAsync(
            new LineageFile(_home, "s1"),
            Call("helper_ask", Codex("t-a"), forged.DeepClone().AsObject()),
            Call("helper_ask", Claude("toolu_child"), forged.DeepClone().AsObject()),
            Call("helper_ask", null, forged));

        Assert.All(answers, answer => Assert.True(answer.Error));
        Assert.Contains("came from a subagent", answers[1].Text);
        Assert.Contains("could not verify", answers[2].Text);
    }

    /// <summary>
    /// The row's proof: a proven root calls every tool as before. A Claude root by its hook line naming no agent; a Codex root by
    /// its protocol door's shape, which answered its quests and helpers in the 128 calls measured (D173's 2026-10-31 note).
    /// </summary>
    [Fact]
    public async Task The_root_s_calls_are_unchanged()
    {
        var calls = new List<JsonObject>();
        foreach (var tool in Tools)
        {
            calls.Add(Call(tool, Codex("t-root")));
            Hooked("toolu_" + tool, null);
            calls.Add(Call(tool, Claude("toolu_" + tool)));
        }

        var answers = await CallAsync(new LineageFile(_home, "s1"), [.. calls]);

        Assert.Equal(Tools.SelectMany(tool => new[] { (false, "ran " + tool), (false, "ran " + tool) }), answers);
    }

    /// <summary>Fail closed: missing, unjoined or malformed lineage is refused a restricted tool, and never said to be a subagent's.</summary>
    [Fact]
    public async Task A_call_whose_lineage_cannot_be_read_is_refused_and_never_called_a_subagent_s()
    {
        var answers = await CallAsync(
            new LineageFile(_home, "s1"),
            Call("quest_respond"),
            Call("quest_respond", Claude("toolu_unjoined")),
            Call("quest_respond", new JsonObject { ["x-codex-turn-metadata"] = """{"session_id":"t-root","thread_id":"t-root","parent_thread_id":"a b"}""" }),
            Call("quest_publish", Claude("toolu_unjoined"), new JsonObject { ["attachments"] = new JsonArray("shot.png") }),
            Call("knowledge_get"));

        Assert.All(answers.Take(4), answer =>
        {
            Assert.True(answer.Error);
            Assert.Contains("could not verify which agent made this call", answer.Text);
            Assert.DoesNotContain("came from a subagent", answer.Text);
        });
        Assert.Equal((false, "ran knowledge_get"), answers[4]);
    }

    /// <summary>
    /// Canary 8: a child may ask another repository with a plain quest, and with nothing that writes to an ask or carries more.
    /// </summary>
    [Fact]
    public async Task A_child_may_publish_a_plain_quest_and_nothing_more()
    {
        JsonObject Plain() => new() { ["from"] = "web", ["to"] = "service", ["title"] = "A question", ["body"] = "Why.", ["links"] = new JsonArray("https://example.test/a") };
        JsonObject With(string name, JsonNode value)
        {
            var arguments = Plain();
            arguments[name] = value;
            return arguments;
        }

        var answers = await CallAsync(
            new LineageFile(_home, "s1"),
            Call("quest_publish", Codex("t-a"), Plain()),
            Call("quest_publish", Codex("t-a"), With("attachments", new JsonArray("shot.png"))),
            Call("quest_publish", Codex("t-a"), With("then", new JsonArray(new JsonObject { ["to"] = "web", ["title"] = "t", ["body"] = "b" }))),
            Call("quest_publish", Codex("t-a"), With("requirements", new JsonArray(new JsonObject { ["quote"] = "q", ["check"] = "c" }))),
            Call("quest_publish", Codex("t-a"), With("review", new JsonObject { ["choice"] = "on" })),
            Call("quest_publish", Codex("t-a"), With("reviewProposal", new JsonObject { ["choice"] = "on", ["reason"] = "r" })),
            Call("quest_publish", Codex("t-a"), With("workflowProposal", new JsonObject { ["kind"] = "bug", ["reason"] = "r" })),
            Call("quest_publish", Codex("t-a"), With("from", JsonValue.Create(AskDesk.SenderOf("a1")))),
            Call("quest_publish", Codex("t-a"), With("Attachments", new JsonArray("shot.png"))));
        var asAsk = await CallAsync(new LineageFile(_home, "s1"), new IntakeScope("a1", "s1"), Call("quest_publish", Codex("t-a"), Plain()));

        Assert.Equal((false, "ran quest_publish"), answers[0]);
        Assert.All(answers.Skip(1).Concat(asAsk), answer =>
        {
            Assert.True(answer.Error);
            Assert.StartsWith("Nothing was done: `quest_publish` was not run, and no quest was published.", answer.Text);
        });
    }

    /// <summary>A connector told no session, a person's own agent's, keeps today's behaviour: this guard is a session's.</summary>
    [Fact]
    public async Task A_connector_naming_no_session_is_not_guarded()
    {
        var answers = await CallAsync(new LineageFile(_home, null), Call("helper_ask", Codex("t-a")), Call("echo"));

        Assert.Equal([(false, "ran helper_ask"), (false, "ran echo")], answers);
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
            .RecordCallLineage(file, joinBudget: TimeSpan.FromMilliseconds(50));

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
