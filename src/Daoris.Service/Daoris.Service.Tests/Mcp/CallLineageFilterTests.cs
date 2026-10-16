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
/// FABRIC-C1a: the connector reads every call's <c>_meta</c> before its tool runs, and writes what its harness said of the
/// call's lineage to the session's lineage file; the tool answers as it did. A server over in-memory streams, as
/// <see cref="DiscoverProbeTests"/> runs one, with the lineage filter the host composes.
/// </summary>
public sealed class CallLineageFilterTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-filter-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [McpServerToolType]
    public sealed class EchoTools
    {
        [McpServerTool(Name = "echo")]
        [Description("Answers what it was told.")]
        public static string Echo([Description("Words.")] string? words = null) => $"echo {words}";
    }

    [Fact]
    public async Task Each_call_s_lineage_is_written_before_its_tool_answers_and_the_answer_is_unchanged()
    {
        var answers = await CallAsync(
            new JsonObject
            {
                ["name"] = "echo",
                ["arguments"] = new JsonObject { ["words"] = "hi", ["thread_id"] = "forged" },
                ["_meta"] = new JsonObject { ["claudecode/toolUseId"] = "toolu_child", ["progressToken"] = 1 },
            },
            new JsonObject
            {
                ["name"] = "echo",
                ["_meta"] = new JsonObject
                {
                    ["x-codex-turn-metadata"] = """{"session_id":"t1","thread_id":"t2","parent_thread_id":"t1","subagent_kind":"thread_spawn","thread_source":"subagent"}""",
                },
            },
            new JsonObject { ["name"] = "echo" });

        Assert.Equal(["echo hi", "echo ", "echo "], answers);
        var lines = File.ReadAllLines(Path.Combine(_home, "sessions", "s1", LineageFile.FileName)).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(("claude-code", "toolu_child"), ((string)lines[0]["harness"]!, (string)lines[0]["toolUse"]!));
        // An id in the arguments is the model's, never the harness's: it is not read.
        Assert.Null(lines[0]["thread"]);
        Assert.Equal(("codex", "t2", "t1"), ((string)lines[1]["harness"]!, (string)lines[1]["thread"]!, (string)lines[1]["parent"]!));
        Assert.Null(lines[2]["harness"]);
        Assert.All(lines, line => Assert.Equal("echo", (string)line["tool"]!));
    }

    /// <summary>The host's own composition records it, so what the test above holds is what a session's connector does.</summary>
    [Fact]
    public void The_host_records_every_call_s_lineage()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.Contains(".RecordCallLineage(LineageFile.FromEnvironment())", program);
    }

    private async Task<List<string>> CallAsync(params JsonObject[] calls)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .WithTools(toolTypes: [typeof(EchoTools)])
            .RecordCallLineage(new LineageFile(_home, "s1"));

        var answers = new List<string>();
        using (var host = builder.Build())
        {
            await host.StartAsync();
            using var reader = new StreamReader(fromServer.Reader.AsStream());

            async Task<JsonNode> Send(JsonObject message)
            {
                await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes(message.ToJsonString() + "\n"));
                var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
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
                answers.Add((string)answer["result"]!["content"]![0]!["text"]!);
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
