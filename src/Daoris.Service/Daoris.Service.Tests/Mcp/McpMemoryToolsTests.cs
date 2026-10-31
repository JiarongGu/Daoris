using System.IO.Pipelines;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2b (the memory coordinator design §2): the memory tools over a stand-in engine. Every door that takes or yields a reference
/// answers from the repositories the registry places in the caller's workspace, read again at each call: a named session's from its
/// record, an unnamed caller's from where it runs, and none resolved refuses. A read changes nothing, and until the engine lands no tool
/// keeps anything.
/// </summary>
public sealed class McpMemoryToolsTests : McpToolsFixture
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly StandInMemory _memory = new();
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;

    /// <summary>Asker and Owner in workspace `work`, Checker in `elsewhere`, and one entry about the map in each one's memory.</summary>
    private async Task SeedAsync()
    {
        if (_ledger is not null) return;
        foreach (var row in await _service.RegistryAsync())
        {
            await _service.RegisterAsync(row with { Workspace = row.Repository == "Checker" ? "elsewhere" : "work" }, Now);
        }

        _sessions = await SessionStore.OpenAsync(_connection);
        _ledger = new SessionLedger(_quests, _sessions, _service, await AskStore.OpenAsync(_connection));
        foreach (var name in new[] { "Asker", "Owner", "Checker" }) _memory.Engine(name).Keep($"{name} draws the map in tiles.");
    }

    private string Room(string repository) => Path.Combine(_root, "family", repository);

    /// <summary>The memory tools as <paramref name="caller"/>'s connector holds them, over the stand-in, or over no engine where memory is off.</summary>
    private async Task<MemoryTools> AsAsync(string caller, bool memoryOn = true)
    {
        await SeedAsync();
        var engines = memoryOn ? _memory : null;
        Task<Session> Record(SessionKind kind = SessionKind.Chat, string repository = "Owner", string? serves = null) =>
            _sessions.CreateAsync(null, repository, "claude-code", Now, workspace: "work", kind: kind, serves: serves);
        MemoryTools Named(Session record, AmbientWorkspace? ambient = null) =>
            new(_service, ambient ?? new AmbientWorkspace(Room(record.Repository)), new IntakeScope(null, record.Id), _ledger, engines);

        return caller switch
        {
            "build" => Named(await Record(SessionKind.Driven)),
            "chat" => Named(await Record()),
            "help" => Named(await Record(serves: (await Record(SessionKind.Driven)).Id)),
            "bound" => Named(await Record(repository: SessionLedger.HelpRepository), new AmbientWorkspace(Path.Combine(_root, "home")) { Bound = "work" }),
            "unnamed" => new MemoryTools(_service, new AmbientWorkspace(Room("Asker")), engines: engines),
            _ => throw new ArgumentOutOfRangeException(nameof(caller)),
        };
    }

    /// <summary>
    /// 🔴 Another workspace's reference is refused at every door, and never yielded by a search, for a build, a chat, a helper, Ask Daoris
    /// bound to its workspace and an unnamed caller; the stand-in is left as it was.
    /// </summary>
    [Theory]
    [InlineData("build")]
    [InlineData("chat")]
    [InlineData("help")]
    [InlineData("bound")]
    [InlineData("unnamed")]
    public async Task Another_workspaces_reference_is_refused_at_each_door(string caller)
    {
        var tools = await AsAsync(caller);
        var before = _memory.State();

        var found = await tools.SearchAsync("map");
        var answers = new[]
        {
            await tools.GetAsync("Checker:m1"),
            await tools.GetAsync("Checker:m1", expand: true),
            await tools.WriteAsync("The map needs a legend.", "disputes", "Checker:m1"),
            await tools.EndAsync("Checker:m1", 1, "The map is drawn in vectors now."),
            await tools.ProposeAsync("Checker:m1", "disputes", "The map is drawn in vectors now.", 1),
        };

        Assert.Contains("`Owner:m1`", found);
        Assert.Contains("`Asker:m1`", found);
        Assert.DoesNotContain("Checker", found);
        Assert.All(answers, answer =>
        {
            Assert.StartsWith("Refused", answer);
            Assert.Contains("`Checker`", answer);
            Assert.Contains("workspace `work`", answer);
            Assert.DoesNotContain("draws the map", answer);
        });
        Assert.Equal(before, _memory.State());
    }

    /// <summary>🔴 An unnamed caller running in no registered repository has no workspace, so every door refuses, its own reads too.</summary>
    [Fact]
    public async Task With_no_ambient_workspace_every_door_refuses()
    {
        await SeedAsync();
        var tools = new MemoryTools(_service, new AmbientWorkspace(Path.Combine(_root, "room")), engines: _memory);
        var before = _memory.State();

        var answers = new[]
        {
            await tools.SearchAsync("map"),
            await tools.GetAsync("Owner:m1"),
            await tools.WriteAsync("The map needs a legend."),
            await tools.EndAsync("Owner:m1", 1, "The map is drawn in vectors now."),
            await tools.ProposeAsync("Owner:m1", "end", "The map is drawn in vectors now.", 1),
        };

        Assert.All(answers, answer =>
        {
            Assert.StartsWith("Refused", answer);
            Assert.Contains("which workspace", answer);
            Assert.DoesNotContain("draws the map", answer);
        });
        Assert.Equal(before, _memory.State());
    }

    /// <summary>🔴 A repository the registry moves to another workspace is refused at the caller's next call, and its entries no longer found.</summary>
    [Fact]
    public async Task A_repository_moved_out_of_the_workspace_is_refused_at_its_next_call()
    {
        var tools = await AsAsync("build");
        Assert.Contains("Asker draws the map", await tools.GetAsync("Asker:m1"));

        var asker = (await _service.RegistryAsync()).Single(row => row.Repository == "Asker");
        await _service.RegisterAsync(asker with { Workspace = "elsewhere" }, Now);
        var moved = await tools.GetAsync("Asker:m1");

        Assert.StartsWith("Refused", moved);
        Assert.Contains("`Asker`", moved);
        Assert.DoesNotContain("draws the map", moved);
        Assert.DoesNotContain("Asker", await tools.SearchAsync("map"));
    }

    /// <summary>
    /// 🔴 A search, a read and an expansion leave every effect the stand-in counts as it was: the engine is opened with no reinforcement,
    /// so a read touches, co-activates and logs nothing of the owner's.
    /// </summary>
    [Fact]
    public async Task No_read_tool_writes()
    {
        var tools = await AsAsync("build");
        var legend = _memory.Engine("Owner").Keep("The legend sits under the map.");
        await _memory.Open("Owner", MemoryReinforcement.None)!.RelateAsync(legend.Id, "m1", "amends");
        var before = _memory.State();

        var found = await tools.SearchAsync("map legend");
        var expanded = await tools.GetAsync($"Owner:{legend.Id}", expand: true);
        var read = await tools.GetAsync("Asker:m1");

        Assert.Contains($"`Owner:{legend.Id}`", found);
        Assert.Contains("amends `Owner:m1`", expanded);
        Assert.Contains("Owner draws the map", expanded);
        Assert.Contains("Asker draws the map", read);
        Assert.Equal(before, _memory.State());
    }

    /// <summary>
    /// 🔴 Where memory is off every tool says so and keeps nothing; where it is on, a write and an end are kept staged under the session
    /// (KNOWCENTRAL2d) and the engine is left as it was, and a proposal still waits for its quest (2e).
    /// </summary>
    [Fact]
    public async Task Where_memory_is_off_each_tool_says_so_and_where_on_a_change_is_only_staged()
    {
        var off = await AsAsync("build", memoryOn: false);
        var on = await AsAsync("chat");
        var before = _memory.State();

        var answers = new[]
        {
            await off.SearchAsync("map"),
            await off.GetAsync("Owner:m1"),
            await off.WriteAsync("The map needs a legend."),
            await off.EndAsync("Owner:m1", 1, "The map is drawn in vectors now."),
            await off.ProposeAsync("Asker:m1", "end", "The map is drawn in vectors now.", 1),
            await on.ProposeAsync("Asker:m1", "amends", "The legend sits under the map.", 1),
        };
        var wrote = await on.WriteAsync("The map needs a legend.", "amends", "Owner:m1");
        var ended = await on.EndAsync("Owner:m1", 1, "The map is drawn in vectors now.");

        Assert.All(answers, answer => Assert.Equal(MemoryTools.NotOn, answer));
        Assert.Contains("staged", wrote);
        Assert.Contains("staged", ended);
        Assert.Equal(before, _memory.State());
    }

    /// <summary>🔴 A write naming an entry it supersedes is refused, naming Lyntai's request for one change (the design §4, §6 A), and keeps nothing.</summary>
    [Fact]
    public async Task A_write_that_supersedes_is_refused_naming_the_request()
    {
        var tools = await AsAsync("chat");
        var before = _memory.State();

        var answer = await tools.WriteAsync("The map is drawn in vectors.", supersedes: "Owner:m1");

        Assert.Equal(MemoryTools.SupersedesWaits, answer);
        Assert.Contains("§6 A", answer);
        Assert.Equal(before, _memory.State());
        Assert.DoesNotContain(await _ledger.Memory.ListAsync(MemoryOpState.Staged), op => op.Change.Text == "The map is drawn in vectors.");
    }

    /// <summary>
    /// 🔴 An intake and Ask Daoris work in no repository of their own, so the call table refuses them the write and the end in its words;
    /// an unnamed caller, which the table passes, is refused both by the tools and still reads. Nothing is kept.
    /// </summary>
    [Fact]
    public async Task An_intake_Ask_Daoris_and_an_unnamed_caller_are_refused_the_write_and_the_end()
    {
        foreach (var work in new[] { ParticipantWork.Intake, ParticipantWork.AskDaoris })
        {
            foreach (var tool in new[] { "memory_write", "memory_end" })
            {
                var refusal = CallTable.Refusal(new SessionWork(work), tool);
                Assert.Equal(CallTable.ForbiddenWork, refusal?.Code);
                Assert.Contains("changes no repository's memory", refusal!.Text);
            }

            Assert.True(CallTable.Served(new SessionWork(work), "memory_propose"));
        }

        var unnamed = await AsAsync("unnamed");
        var before = _memory.State();
        var answers = new[]
        {
            await unnamed.WriteAsync("The map needs a legend."),
            await unnamed.EndAsync("Asker:m1", 1, "The map is drawn in vectors now."),
        };

        Assert.True(CallTable.Served(SessionWork.Unnamed, "memory_write"));
        Assert.All(answers, answer =>
        {
            Assert.StartsWith("Refused", answer);
            Assert.Contains("names no such session", answer);
        });
        Assert.Contains("Asker draws the map", await unnamed.GetAsync("Asker:m1"));
        Assert.Equal(before, _memory.State());
    }

    /// <summary>
    /// 🔴 Through the connector's lineage guard: a build's subagent is served the memory reads, answered from the stand-in, and refused the
    /// write, the end and the proposal before they run; the stand-in is left as it was.
    /// </summary>
    [Fact]
    public async Task A_child_is_served_the_memory_reads_alone()
    {
        await SeedAsync();
        var build = await _sessions.CreateAsync("q1", "Owner", "codex-acp", Now, workspace: "work");
        var child = new JsonObject { ["x-codex-turn-metadata"] = new JsonObject { ["session_id"] = "t-root", ["thread_id"] = "t-child" }.ToJsonString() };
        var before = _memory.State();

        var answers = await CallAsync(build.Id, child,
            ("memory_search", new JsonObject { ["query"] = "map" }),
            ("memory_get", new JsonObject { ["reference"] = "Owner:m1" }),
            ("memory_write", new JsonObject { ["text"] = "The map needs a legend." }),
            ("memory_end", new JsonObject { ["reference"] = "Owner:m1", ["revision"] = 1, ["reason"] = "The map is drawn in vectors now." }),
            ("memory_propose", new JsonObject
            {
                ["reference"] = "Asker:m1", ["change"] = "end", ["reason"] = "The map is drawn in vectors now.", ["revision"] = 1,
            }));

        Assert.False(answers[0].Error);
        Assert.Contains("`Owner:m1`", answers[0].Text);
        Assert.False(answers[1].Error);
        Assert.Contains("Owner draws the map", answers[1].Text);
        foreach (var (tool, (error, text)) in new[] { "memory_write", "memory_end", "memory_propose" }.Zip(answers.Skip(2)))
        {
            Assert.True(error, tool);
            Assert.StartsWith($"Nothing was done: `{tool}` was not run. This call came from a subagent", text);
        }

        Assert.Equal(before, _memory.State());
    }

    /// <summary>
    /// 🔴 Over Lyntai's engine (KNOWCENTRAL2d): a search, a read and an expansion, by a build's own call and by its subagent's, leave each
    /// entry's decay state and revision, its links and the review rows as they were. A reinforcing recall would move them, which the
    /// reading sees.
    /// </summary>
    [Fact]
    public async Task Lyntai_reads_by_a_root_and_its_child_change_nothing()
    {
        await SeedAsync();
        await using var lyntai = LyntaiFile.Open("memory-reads");
        var owner = lyntai.Engine("Owner");
        var map = await owner.RememberAsync("Owner draws the map in tiles.");
        var legend = await owner.RememberAsync("The legend sits under the map.");
        await owner.RelateAsync(legend.Id, map.Id, "amends");
        var build = await _sessions.CreateAsync("q1", "Owner", "codex-acp", Now, workspace: "work");
        var before = await lyntai.StateAsync("Owner", map.Id, legend.Id);
        var child = new JsonObject { ["x-codex-turn-metadata"] = new JsonObject { ["session_id"] = "t-root", ["thread_id"] = "t-child" }.ToJsonString() };

        foreach (var meta in new[] { new JsonObject(), child })
        {
            var answers = await CallAsync(build.Id, meta, lyntai.Memory,
                ("memory_search", new JsonObject { ["query"] = "map" }),
                ("memory_get", new JsonObject { ["reference"] = $"Owner:{map.Id}" }),
                ("memory_get", new JsonObject { ["reference"] = $"Owner:{legend.Id}", ["expand"] = true }));

            Assert.Contains($"`Owner:{legend.Id}`", answers[0].Text);
            Assert.Contains("Owner draws the map in tiles.", answers[1].Text);
            Assert.Contains($"amends `Owner:{map.Id}`", answers[2].Text);
        }

        Assert.Equal(before, await lyntai.StateAsync("Owner", map.Id, legend.Id));
        await lyntai.Memory.Open("Owner", MemoryReinforcement.OnRecall)!.SearchAsync("map", 10);
        Assert.NotEqual(before, await lyntai.StateAsync("Owner", map.Id, legend.Id));
    }

    /// <summary>Each call made through a connector naming <paramref name="session"/>, with <paramref name="meta"/> as its harness wrote it.</summary>
    private Task<List<(bool Error, string Text)>> CallAsync(string session, JsonObject meta, params (string Tool, JsonObject Arguments)[] calls) =>
        CallAsync(session, meta, _memory, calls);

    private async Task<List<(bool Error, string Text)>> CallAsync(
        string session, JsonObject meta, IMemoryEngines engines, params (string Tool, JsonObject Arguments)[] calls)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services
            .AddSingleton(_service).AddSingleton(new AmbientWorkspace(Room("Owner"))).AddSingleton(new IntakeScope(null, session))
            .AddSingleton(_ledger).AddSingleton(engines)
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .WithTools(toolTypes: [typeof(MemoryTools)])
            .RecordCallLineage(new LineageFile(Path.Combine(_root, "home"), session), joinBudget: TimeSpan.FromMilliseconds(50));

        var answers = new List<(bool, string)>();
        using (var host = builder.Build())
        {
            await host.StartAsync();
            using var reader = new StreamReader(fromServer.Reader.AsStream());

            async Task<JsonNode> Send(JsonObject message)
            {
                await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes(message.ToJsonString() + "\n"));
                return JsonNode.Parse((await reader.ReadLineAsync().WaitAsync(Poll.Patience))!)!;
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
            foreach (var (tool, arguments) in calls)
            {
                var call = new JsonObject { ["name"] = tool, ["arguments"] = arguments, ["_meta"] = meta.DeepClone() };
                var result = (await Send(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id++, ["method"] = "tools/call", ["params"] = call }))["result"]!;
                answers.Add(((bool?)result["isError"] ?? false, (string)result["content"]![0]!["text"]!));
            }

            await toServer.Writer.CompleteAsync();
            await host.StopAsync();
        }

        return answers;
    }

    /// <summary>No memory tool takes a workspace or a repository: the scope is the caller's record's, never an argument's.</summary>
    [Fact]
    public void No_memory_tool_takes_a_workspace_or_a_repository()
    {
        var tools = typeof(MemoryTools).GetMethods().Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null).ToList();

        Assert.Equal(
            ["memory_end", "memory_get", "memory_propose", "memory_search", "memory_write"],
            tools.Select(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            tools.SelectMany(method => method.GetParameters()).Select(parameter => parameter.Name!),
            name => name.Contains("workspace", StringComparison.OrdinalIgnoreCase) || name.Contains("repository", StringComparison.OrdinalIgnoreCase));
    }
}
