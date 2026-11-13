using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Daoris.Service.Tests;

/// <summary>
/// The connector lists every tool it registers to a session of any work and refuses no call by that work (ACCESS1 row h):
/// <c>fixtures/call-table.json</c> names every work and every registered tool, and a tool added later is listed to every work.
/// </summary>
public sealed class McpRoleFilterTests
{
    private static readonly string TableFile =
        Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "call-table.json");

    /// <summary>🔴 The file names every tool the host registers, and a column for every work, a brainstorm placed by its two turns.</summary>
    [Fact]
    public void The_table_names_every_registered_tool_and_every_work()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(TableFile));
        var registered = table.RootElement.GetProperty("registered").EnumerateArray().Select(name => name.GetString()!);
        var columns = table.RootElement.GetProperty("columns").EnumerateArray().Select(name => name.GetString()!).ToList();

        Assert.Equal(ToolClassesTests.Advertised(), registered.Order(StringComparer.Ordinal));
        string[] works =
            [.. ParticipantWork.Codes.Where(code => code != ParticipantWork.Brainstorm), "brainstorm:generator", "brainstorm:synthesizer", "unplaced", "unread"];
        Assert.Equal(works.Order(StringComparer.Ordinal), columns.Order(StringComparer.Ordinal));
        Assert.Contains("step", columns);
    }

    /// <summary>
    /// 🔴 Through the server: a connector naming a session of each work lists every registered tool, <c>knowledge_refresh</c> and the
    /// turns of a brainstorm among them.
    /// </summary>
    [Fact]
    public async Task Every_work_is_listed_every_registered_tool()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(TableFile));
        var columns = table.RootElement.GetProperty("columns").EnumerateArray().Select(name => name.GetString()!);

        foreach (var column in columns)
        {
            var listed = await ListAsync(new IntakeScope(null, "s-" + column.Replace(':', '-')));
            Assert.True(ToolClassesTests.Advertised().SequenceEqual(listed.Order(StringComparer.Ordinal)), column);
        }

        Assert.Contains("knowledge_refresh", ToolClassesTests.Advertised());
    }

    /// <summary>The host composes no filter that judges a call by its session's work, so what the tests above hold is what a session's connector does.</summary>
    [Fact]
    public void The_host_judges_no_call_by_the_sessions_work()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        var program = File.ReadAllText(Path.Combine(at!.FullName, "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.DoesNotContain("RefuseOutsideRole", program);
        Assert.DoesNotContain("SessionWorks", program);
    }

    /// <summary>The names a session's <c>tools/list</c> answers through the server composed as the host composes it.</summary>
    private static async Task<List<string>> ListAsync(IntakeScope intake)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddSingleton(intake);
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .RecordCallLineage(new LineageFile(null, intake.Session))
            .WithKnowledgeTools();

        using var host = builder.Build();
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
        var answer = await Send(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/list" });
        var names = answer["result"]!["tools"]!.AsArray().Select(tool => (string)tool!["name"]!).ToList();

        await toServer.Writer.CompleteAsync();
        await host.StopAsync();
        return names;
    }
}
