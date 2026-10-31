using System.ComponentModel;
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
/// CONNARGS1: a connector tool called without a required argument is refused in plain words, not logged as an unhandled exception. On the
/// install a session called <c>quest_respond</c> with the wrong parameter names four times, and each time the SDK's argument marshaller threw
/// <see cref="ArgumentException"/> ("missing a value for the required parameter id"), which the machine log kept as an error and the usage
/// report counted as failed. A server over in-memory streams with the host's own tools, logging and filter, and the machine log read after.
/// </summary>
public sealed class ArgumentFilterTests : IDisposable
{
    [McpServerToolType]
    public sealed class StandInTools
    {
        [McpServerTool(Name = "stand_in_throws")]
        [Description("A tool whose own code throws an ArgumentException.")]
        public static string Throws([Description("Words.")] string words) =>
            throw new ArgumentException($"the tool's own complaint about {words}");

        [McpServerTool(Name = "stand_in_needs")]
        [Description("A tool with a required and an optional parameter.")]
        public static string Needs([Description("Required.")] string id, [Description("Optional.")] string? note = null) => $"{id} {note}";
    }

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-args-" + Guid.NewGuid().ToString("N")[..8]);

    public ArgumentFilterTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 Nothing given at all: refused as the tool's error result, naming what is missing and everything the tool takes.</summary>
    [Fact]
    public async Task A_missing_required_argument_is_refused_naming_it_and_what_the_tool_takes()
    {
        var answers = await CallAsync(("quest_respond", new JsonObject()));

        var answer = Assert.Single(answers);
        Assert.True(answer.IsError);
        Assert.Contains("`quest_respond` was called without `id`, `action`, which it needs.", answer.Text);
        Assert.Contains("It takes `id` (required), `action` (required),", answer.Text);
        Assert.Contains("`reason`", answer.Text);
        Assert.Contains("`answers`", answer.Text);
        Assert.DoesNotContain("which it does not take", answer.Text);
    }

    /// <summary>🔴 The incident's call: the right values under the wrong names. The names it gave are said not to be the tool's.</summary>
    [Fact]
    public async Task A_misnamed_argument_is_refused_naming_what_is_missing_and_what_was_given_instead()
    {
        var answers = await CallAsync(("quest_respond", new JsonObject { ["quest"] = "8b4ae06acd49", ["do"] = "take" }));

        var answer = Assert.Single(answers);
        Assert.True(answer.IsError);
        Assert.Contains("was called without `id`, `action`, which it needs.", answer.Text);
        Assert.Contains("It was given `do`, `quest`, which it does not take.", answer.Text);
        Assert.Contains("It takes `id` (required), `action` (required),", answer.Text);
        Assert.DoesNotContain("8b4ae06acd49", answer.Text);
    }

    /// <summary>A call that names every required parameter is the tool's: an unknown extra name is no refusal, as before.</summary>
    [Fact]
    public async Task A_call_naming_every_required_argument_reaches_its_tool()
    {
        var answers = await CallAsync(
            ("stand_in_needs", new JsonObject { ["id"] = "a" }),
            ("stand_in_needs", new JsonObject { ["id"] = "b", ["extra"] = 1 }));

        Assert.Equal(["a ", "b "], answers.Select(answer => answer.Text));
        Assert.All(answers, answer => Assert.False(answer.IsError));
        Assert.DoesNotContain(Lines(), line => line.Contains(ConnectorArguments.Event));
    }

    /// <summary>🔴 A refusal is in the machine log as one information line, never a warning or an error, and holds names, not values.</summary>
    [Fact]
    public async Task A_refusal_is_logged_as_a_refusal_not_as_an_error()
    {
        await CallAsync(("quest_respond", new JsonObject { ["quest"] = "a-private-value" }));

        var lines = Lines();
        var refused = Assert.Single(lines);
        Assert.Contains("\"level\":\"info\"", refused);
        Assert.Contains("\"event\":\"tool.refused\"", refused);
        Assert.Contains("\"tool\":\"quest_respond\"", refused);
        Assert.Contains("\"missing\":\"id,action\"", refused);
        Assert.Contains("\"unknown\":\"quest\"", refused);
        Assert.DoesNotContain("a-private-value", refused);
        Assert.DoesNotContain("unhandled", refused);
    }

    /// <summary>🔴 An <see cref="ArgumentException"/> a tool's own code throws is not the caller's mistake: still the SDK's error, still logged as one.</summary>
    [Fact]
    public async Task A_tools_own_ArgumentException_is_still_an_error()
    {
        var answers = await CallAsync(("stand_in_throws", new JsonObject { ["words"] = "x" }));

        var answer = Assert.Single(answers);
        Assert.True(answer.IsError);
        Assert.DoesNotContain("was called without", answer.Text);
        var error = Assert.Single(Lines(), line => line.Contains("\"level\":\"error\""));
        Assert.Contains("the tool's own complaint about x", error);
        Assert.DoesNotContain(Lines(), line => line.Contains(ConnectorArguments.Event));
    }

    /// <summary>
    /// 🔴 Every tool the host registers, over the wire: each that requires an argument is refused when it is left out, naming each
    /// required parameter and each it takes, and the log holds a refusal for it and no error. A tool added later is covered by its signature.
    /// </summary>
    [Fact]
    public async Task Every_registered_tool_that_requires_an_argument_is_refused_when_it_is_left_out()
    {
        var (advertised, answers, refusals) = await CallEveryToolAsync();

        Assert.Contains("quest_respond", refusals);
        Assert.Equal(KnowledgeServer.ToolTypes.Sum(CountTools) + 2, advertised.Count);
        foreach (var tool in advertised.Where(tool => tool.Required.Count > 0 && !tool.Name.StartsWith("stand_in_", StringComparison.Ordinal)))
        {
            var answer = answers[tool.Name];
            Assert.True(answer.IsError, tool.Name);
            Assert.Contains($"`{tool.Name}` was called without", answer.Text);
            foreach (var name in tool.Required) Assert.Contains($"`{name}` (required)", answer.Text);
            foreach (var name in tool.Takes) Assert.Contains($"`{name}`", answer.Text);
            Assert.Contains("which it does not take", answer.Text);
        }

        Assert.DoesNotContain(Lines(), line => line.Contains("\"level\":\"error\"") || line.Contains("\"level\":\"warn\""));
        Assert.Equal(refusals.Count, Lines().Count(line => line.Contains("\"event\":\"tool.refused\"")));
    }

    /// <summary>The host's own composition holds its calls to their required arguments, so what the tests above hold is what a session's connector does.</summary>
    [Fact]
    public void The_host_refuses_a_call_that_leaves_out_a_required_argument()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.Contains(".RefuseMissingArguments(log)", program);
    }

    /// <summary>
    /// 🔴 FILTERORDER1: a partner's call to a tool its role refuses, with a required argument left out, is answered with the role's denial
    /// and never the argument refusal, which would name the denied tool's parameters. Over the wire with every tool the host advertises,
    /// the two filters registered in the host's order, and nothing in the log for the argument refusal.
    /// </summary>
    [Fact]
    public async Task A_partners_call_to_a_refused_tool_missing_an_argument_gets_the_roles_denial()
    {
        var answers = await CallAsync(new SessionWork(ParticipantWork.Partner), ("quest_respond", new JsonObject()), ("quest_publish", new JsonObject { ["not_a_parameter"] = 1 }));

        Assert.All(answers, answer =>
        {
            Assert.Contains("is not a partner's to call", answer.Text);
            Assert.DoesNotContain("was called without", answer.Text);
            Assert.DoesNotContain("It takes", answer.Text);
            Assert.DoesNotContain("`id`", answer.Text);
            Assert.DoesNotContain("`action`", answer.Text);
        });
        Assert.DoesNotContain(Lines(), line => line.Contains(ConnectorArguments.Event));
    }

    /// <summary>🔴 A helper's record (it serves, writes no piece) is refused the quest tools: its denial comes first for a call missing an argument, too.</summary>
    [Fact]
    public async Task A_helpers_call_to_a_quest_tool_missing_an_argument_gets_the_roles_denial()
    {
        var answers = await CallAsync(new SessionWork(ParticipantWork.Help), ("quest_respond", new JsonObject()));

        var answer = Assert.Single(answers);
        Assert.Contains("is refused to a session that serves another", answer.Text);
        Assert.DoesNotContain("was called without", answer.Text);
        Assert.DoesNotContain("It takes", answer.Text);
        Assert.DoesNotContain(Lines(), line => line.Contains(ConnectorArguments.Event));
    }

    /// <summary>The role judges only what it refuses: a partner's call to a tool it may use, missing an argument, still gets the argument refusal.</summary>
    [Fact]
    public async Task A_partners_call_to_an_allowed_tool_missing_an_argument_still_gets_the_argument_refusal()
    {
        var answers = await CallAsync(new SessionWork(ParticipantWork.Partner), ("knowledge_search", new JsonObject { ["not_a_parameter"] = 1 }));

        var answer = Assert.Single(answers);
        Assert.True(answer.IsError);
        Assert.Contains("`knowledge_search` was called without", answer.Text);
        Assert.DoesNotContain("not a partner's to call", answer.Text);
    }

    /// <summary>The host registers the role's judgement before the argument refusal, so the first to see a call is the role (FILTERORDER1).</summary>
    [Fact]
    public void The_host_registers_the_role_before_the_argument_refusal()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        var role = program.IndexOf(".RefuseOutsideRole(", StringComparison.Ordinal);
        var arguments = program.IndexOf(".RefuseMissingArguments(", StringComparison.Ordinal);
        Assert.True(role >= 0 && arguments > role, "RefuseOutsideRole must be registered before RefuseMissingArguments");
    }

    private static int CountTools(Type type) =>
        type.GetMethods().Count(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false).Length > 0);

    private sealed record Advertised(string Name, IReadOnlyList<string> Required, IReadOnlyList<string> Takes);

    private sealed record Answer(bool IsError, string Text);

    private Task<List<Answer>> CallAsync(params (string Tool, JsonObject Arguments)[] calls) => CallAsync(null, calls);

    private async Task<List<Answer>> CallAsync(SessionWork? role, params (string Tool, JsonObject Arguments)[] calls)
    {
        List<Answer> answers = [];
        await RunAsync(async session =>
        {
            foreach (var (tool, arguments) in calls) answers.Add(await session.CallAsync(tool, arguments));
        }, role);
        return answers;
    }

    private async Task<(List<Advertised> Advertised, Dictionary<string, Answer> Answers, List<string> Refused)> CallEveryToolAsync()
    {
        List<Advertised> advertised = [];
        Dictionary<string, Answer> answers = [];
        List<string> refused = [];
        await RunAsync(async session =>
        {
            var listed = await session.SendAsync("tools/list", new JsonObject());
            foreach (var tool in listed["result"]!["tools"]!.AsArray())
            {
                var schema = tool!["inputSchema"]!;
                var required = schema["required"]?.AsArray().Select(name => (string)name!).ToList() ?? [];
                var takes = schema["properties"]?.AsObject().Select(property => property.Key).ToList() ?? [];
                advertised.Add(new Advertised((string)tool["name"]!, required, takes));
            }

            foreach (var tool in advertised.Where(tool => tool.Required.Count > 0))
            {
                // A name no tool takes, so the call says nothing a tool could mistake for its own.
                answers[tool.Name] = await session.CallAsync(tool.Name, new JsonObject { ["not_a_parameter"] = 1 });
                refused.Add(tool.Name);
            }
        });
        return (advertised, answers, refused);
    }

    private sealed class Session(Pipe toServer, StreamReader reader)
    {
        private int _id;

        public async Task<JsonNode> SendAsync(string method, JsonObject parameters)
        {
            var message = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = ++_id, ["method"] = method, ["params"] = parameters };
            await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes(message.ToJsonString() + "\n"));
            return JsonNode.Parse((await reader.ReadLineAsync().WaitAsync(Poll.Patience))!)!;
        }

        public async Task<Answer> CallAsync(string tool, JsonObject arguments)
        {
            var result = (await SendAsync("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments }))["result"]!;
            return new Answer(
                (bool?)result["isError"] == true,
                string.Join("\n", result["content"]!.AsArray().Select(block => (string)block!["text"]!)));
        }
    }

    /// <summary>The host's own tools, logging and filter over in-memory streams, the stand-ins beside them.</summary>
    private async Task RunAsync(Func<Session, Task> calls, SessionWork? role = null)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        using var log = new MachineLog(_home, "mcp");
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        McpHostLogging.Use(builder.Logging, log);
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .RefuseOutsideRole(_ => Task.FromResult(role ?? SessionWork.Unnamed))
            .RefuseMissingArguments(log)
            .WithKnowledgeTools()
            .WithTools(toolTypes: [typeof(StandInTools)]);

        using var host = builder.Build();
        await host.StartAsync();
        using var reader = new StreamReader(fromServer.Reader.AsStream());
        var session = new Session(toServer, reader);
        await session.SendAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "test", ["version"] = "0" },
        });
        await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"notifications/initialized"}""" + "\n"));

        await calls(session);

        await toServer.Writer.CompleteAsync();
        await host.StopAsync();
    }

    private string[] Lines()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
