using System.ComponentModel;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// COWORK1d (D188's note of the owner's direction; the message-urgency design §4.2): every tool's answer ends with what waits for the
/// caller on its ask's channel, where something new waits, and <c>channel_read</c>'s never does, since it is the read. A server over
/// in-memory streams, as <see cref="CallLineageFilterTests"/> runs one, with the filter the host composes.
/// </summary>
public sealed class WaitingFilterTests
{
    [McpServerToolType]
    public sealed class StandInTools
    {
        [McpServerTool(Name = "echo")]
        [Description("Answers what it was told.")]
        public static string Echo([Description("Words.")] string? words = null) => $"echo {words}";

        [McpServerTool(Name = "channel_read")]
        [Description("Stands in for the channel's read.")]
        public static string Read() => "the channel";
    }

    [Fact]
    public async Task Every_answer_but_the_reads_ends_with_what_waits_and_says_nothing_when_nothing_does()
    {
        var waits = new Queue<string?>(["Daoris: 1 message waits for you.", null]);

        var answers = await CallAsync(
            _ => Task.FromResult(waits.Count > 0 ? waits.Dequeue() : null),
            new JsonObject { ["name"] = "channel_read" },
            new JsonObject { ["name"] = "echo", ["arguments"] = new JsonObject { ["words"] = "hi" } },
            new JsonObject { ["name"] = "echo", ["arguments"] = new JsonObject { ["words"] = "again" } });

        Assert.Equal(["the channel"], answers[0]);
        Assert.Equal(["echo hi", "Daoris: 1 message waits for you."], answers[1]);
        Assert.Equal(["echo again"], answers[2]);
    }

    /// <summary>A line that could not be read costs the line, never the tool's own answer.</summary>
    [Fact]
    public async Task A_line_that_fails_leaves_the_answer_as_it_was()
    {
        var answers = await CallAsync(
            _ => throw new InvalidOperationException("the store is gone"),
            new JsonObject { ["name"] = "echo", ["arguments"] = new JsonObject { ["words"] = "hi" } });

        Assert.Equal(["echo hi"], answers[0]);
    }

    /// <summary>The host's own composition tells it, so what the tests above hold is what a session's connector does.</summary>
    [Fact]
    public void The_host_tells_every_answer_what_waits()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.Contains(".TellWhatWaits(new WaitingLine(composed.Channel, intake).NextAsync)", program);
    }

    private static async Task<List<List<string>>> CallAsync(Func<CancellationToken, Task<string?>> waiting, params JsonObject[] calls)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .WithTools(toolTypes: [typeof(StandInTools)])
            .TellWhatWaits(waiting);

        var answers = new List<List<string>>();
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
                answers.Add([.. answer["result"]!["content"]!.AsArray().Select(block => (string)block!["text"]!)]);
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
