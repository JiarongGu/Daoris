using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// An Ask Daoris connector bound to a workspace notes each call that named a workspace or a repository, after its tool answered,
/// in the session's place file the driver composes the turn's receipt from: what the call did, the place as named, what it
/// named of it, and how many a search's answer found where its tool said so. A call that names no place, one its tool refused, and a connector bound to none note nothing.
/// </summary>
public sealed class CallPlaceFilterTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-place-filter-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string PlacesPath => DaorisHome.Entry(_home, HomeEntry.Sessions, "s1", CallPlaceFile.FileName);

    private static IDictionary<string, JsonElement> Arguments(object arguments) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(arguments))!;

    [Theory]
    [InlineData("knowledge_search", "search")]
    [InlineData("quest_list", "search")]
    [InlineData("handback_read", "search")]
    [InlineData("checkout_read", "read")]
    [InlineData("go_propose", "propose")]
    [InlineData("quest_publish", "press")]
    public void Each_door_s_call_is_its_act(string tool, string act)
    {
        var place = CallPlace.Of(tool, Arguments(new { workspace = "Work" }));
        Assert.NotNull(place);
        Assert.Equal(act, place.Act);
        Assert.Equal("Work", place.Workspace);
    }

    [Fact]
    public void A_call_says_what_it_named_of_the_place()
    {
        Assert.Equal("README.md", CallPlace.Of("checkout_read", Arguments(new { repository = "api", path = "README.md" }))!.What);
        Assert.Equal("release", CallPlace.Of("knowledge_search", Arguments(new { query = "release", workspace = "all" }))!.What);
        Assert.Equal("go", CallPlace.Of("go_propose", Arguments(new { repository = "api" }))!.What);
        var read = CallPlace.Of("checkout_read", Arguments(new { repository = "api" }))!;
        Assert.Equal(("api", (string?)null), (read.Repository, read.Workspace));
    }

    [Fact]
    public void A_call_naming_no_place_or_an_unknown_tool_notes_nothing()
    {
        Assert.Null(CallPlace.Of("knowledge_search", Arguments(new { query = "release" })));
        Assert.Null(CallPlace.Of("knowledge_search", Arguments(new { workspace = "  " })));
        Assert.Null(CallPlace.Of("echo", Arguments(new { workspace = "Work" })));
        Assert.Null(CallPlace.Of(null, null));
    }

    [Fact]
    public void Only_a_connector_bound_to_a_workspace_for_a_session_has_a_file()
    {
        Assert.NotNull(new CallPlaceFile(_home, "s1", "Games").Path);
        Assert.Null(new CallPlaceFile(_home, "s1", null).Path);
        Assert.Null(new CallPlaceFile(_home, null, "Games").Path);
        Assert.Null(new CallPlaceFile(_home, "../s1", "Games").Path);
        Assert.Null(new CallPlaceFile(null, "s1", "Games").Path);
    }

    [Fact]
    public async Task Each_answered_call_naming_a_place_is_noted_after_its_tool_and_a_refused_one_is_not()
    {
        var answers = await CallAsync(
            new CallPlaceFile(_home, "s1", "Games"),
            Call("knowledge_search", new JsonObject { ["query"] = "release", ["workspace"] = "Work" }),
            Call("knowledge_search", new JsonObject { ["query"] = "release" }),
            Call("quest_list", new JsonObject { ["workspace"] = "Work" }),
            Call("checkout_read", new JsonObject { ["repository"] = "api", ["path"] = "README.md" }));

        Assert.Equal(["ran knowledge_search", "ran knowledge_search", "no", "ran checkout_read"], answers);
        var lines = File.ReadAllLines(PlacesPath).Select(line => JsonNode.Parse(line)!.AsObject()).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(("knowledge_search", "search", "Work", "release"),
            ((string?)lines[0]["tool"], (string?)lines[0]["act"], (string?)lines[0]["workspace"], (string?)lines[0]["what"]));
        Assert.Equal(("checkout_read", "read", "api", "README.md"),
            ((string?)lines[1]["tool"], (string?)lines[1]["act"], (string?)lines[1]["repository"], (string?)lines[1]["what"]));
        Assert.Null(lines[1]["workspace"]);
        Assert.True(DateTimeOffset.TryParse((string?)lines[0]["at"], out _));
        Assert.Equal(3, (int?)lines[0]["found"]);
        Assert.Null(lines[1]["found"]);
    }

    [Fact]
    public async Task A_connector_bound_to_no_workspace_notes_nothing()
    {
        await CallAsync(new CallPlaceFile(_home, "s1", null), Call("knowledge_search", new JsonObject { ["workspace"] = "Work" }));
        Assert.False(File.Exists(PlacesPath));
    }

    private static JsonObject Call(string tool, JsonObject arguments) => new() { ["name"] = tool, ["arguments"] = arguments };

    private static async Task<List<string>> CallAsync(CallPlaceFile file, params JsonObject[] calls)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        var tools = new[]
            {
                McpServerTool.Create(() =>
                {
                    CallFound.Say(3);
                    return "ran knowledge_search";
                }, new McpServerToolCreateOptions { Name = "knowledge_search", Description = "Stands in, finding three." }),
                McpServerTool.Create(() => "ran checkout_read", new McpServerToolCreateOptions { Name = "checkout_read", Description = "Stands in." }),
            }
            .Append(McpServerTool.Create(
                () => new ModelContextProtocol.Protocol.CallToolResult { IsError = true, Content = [new ModelContextProtocol.Protocol.TextContentBlock { Text = "no" }] },
                new McpServerToolCreateOptions { Name = "quest_list", Description = "Refuses, as a tool whose call failed." }));
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .WithTools(tools)
            .NoteCallPlaces(file);

        var answers = new List<string>();
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
                answers.Add((string)answer["result"]!["content"]![0]!["text"]!);
            }

            await toServer.Writer.CompleteAsync();
            await host.StopAsync();
        }

        return answers;
    }
}
