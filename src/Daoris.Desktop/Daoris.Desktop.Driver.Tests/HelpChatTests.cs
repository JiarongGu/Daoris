using System.Text.Json;
using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask Daoris's conversation (HELP1a, D89): a chat in the room the driver writes under its home, on the
/// helper's agent — the knowledge connector handed, the room's allows and nothing of the person's, no
/// plugin server, and one conversation running at a time.
/// </summary>
/// <remarks>A real process (node) speaking the protocol door, and a stand-in service. No model, no account.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HelpChatTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-help-chat-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpChatTests() => Directory.CreateDirectory(Path.Combine(_home, "engine"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private static readonly HelpMachine Machine = new()
    {
        Adapter = "acp-stub",
        Helper = "acp-stub",
        Cap = 1,
        Repositories = [new("engine", "default") { Checkout = true, Drivable = true }],
    };

    private (ChatRunner Runner, SessionEvents Events, SessionUsage Usage, ServiceClient Client, DriverConfig Config) Runner(StandInService service)
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var usage = new SessionUsage(_home);
        var client = new ServiceClient(service.Url, null);
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")), usage: usage);
        return (runner, events, usage, client, config);
    }

    /// <remarks>
    /// What it may do is <see cref="HelpRoom.Rules"/>, held by its own test: a stub door takes no settings
    /// file, which only a harness that reads Claude Code's rules is handed.
    /// </remarks>
    [Fact]
    public async Task Ask_Daoris_talks_in_its_room_handed_the_connector_and_no_plugin()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var (runner, events, usage, client, config) = Runner(service);
        using var _ = client;
        using var __ = runner;

        var start = await runner.StartHelpAsync("acp-stub", config, Machine);

        var id = start.SessionId;
        Assert.NotNull(id);
        var room = HelpRoom.PathOf(_home);
        Assert.Contains("`engine`", File.ReadAllText(Path.Combine(room, "AGENTS.md")));
        string Seen() => $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], state {service.State(id!)}";
        // The record says working when the process starts, before the handshake has opened the session.
        await Until(() => service.State(id!) == "working"
                          && StubFile.Lines(Heard).Any(line => line.StartsWith("session/new ", StringComparison.Ordinal)), Seen);

        // 🔴 In the agent's own asking mode, whatever it started in: since D81 a session judges its own
        // actions, and a helper that could run a command could apply a change nobody confirmed (D89).
        await Until(() => StubFile.Lines(Heard).Contains("session/set_mode default"), Seen);

        // In the room, handed the knowledge connector and no plugin server.
        var opened = StubFile.Lines(Heard).Single(line => line.StartsWith("session/new ", StringComparison.Ordinal));
        using (var seen = JsonDocument.Parse(opened["session/new ".Length..]))
        {
            Assert.Equal(Path.GetFullPath(room), Path.GetFullPath(seen.RootElement.GetProperty("cwd").GetString()!));
            var servers = seen.RootElement.GetProperty("servers").EnumerateArray().Select(s => s.GetString()).ToList();
            Assert.DoesNotContain(servers, name => name != KnowledgeConnector.ServerName);
        }

        Assert.True(runner.Say(id!, "how do I drive a repository?"));
        await Until(() => events.Page(id!).Events.Any(e => e.Kind == SessionEventKind.Turn), Seen);
        var said = events.Page(id!).Events
            .Where(e => e.Kind is SessionEventKind.User or SessionEventKind.Message)
            .Select(e => $"{(e.Kind == SessionEventKind.User ? e.Origin : "agent")}: {e.Text}");
        Assert.Equal(["person: how do I drive a repository?", "agent: heard how do I drive a repository?"], said);

        Assert.True(runner.Finish(id!));
        await Until(() => service.State(id!) == "completed" && usage.Sessions.Count == 1, Seen);
        Assert.Equal(HelpRoom.Repository, Assert.Single(usage.Sessions).Repository);
    }

    /// <summary>
    /// Where the person is (HELP1b): handed to the agent ahead of their words, and kept in the record as
    /// what it was told — never as something the person said.
    /// </summary>
    [Fact]
    public async Task Where_the_person_is_reaches_the_agent_ahead_of_their_words_and_is_not_theirs_in_the_record()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var (runner, events, _, client, config) = Runner(service);
        using var _c = client;
        using var _r = runner;
        var id = (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!;
        string Seen() => $"heard [{string.Join(" | ", StubFile.Lines(Heard))}]";

        Assert.True(runner.Say(id, "why is this parked?", preface: "Where the person is now: the Sessions view."));
        Assert.True(runner.Say(id, "and now?"));
        await Until(() => events.Page(id).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, Seen);

        Assert.Equal(
            // Noted as JSON, so the blank line between the preface and the words is spelled `\n\n`.
            ["prompt: \"Where the person is now: the Sessions view.\\n\\nwhy is this parked?\"", "prompt: \"and now?\""],
            StubFile.Lines(Heard).Where(line => line.StartsWith("prompt: ", StringComparison.Ordinal)));
        var kept = events.Page(id).Events
            .Where(e => e.Kind is SessionEventKind.User or SessionEventKind.Note)
            .Select(e => $"{e.Kind}: {e.Text}");
        Assert.Equal(
            ["note: told where the person is: Where the person is now: the Sessions view.", "user: why is this parked?", "user: and now?"],
            kept);
    }

    /// <summary>One conversation per machine: a second while one runs is refused in the ledger's words, naming it.</summary>
    [Fact]
    public async Task A_second_conversation_while_one_runs_is_refused_naming_it()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var (runner, _, _, client, config) = Runner(service);
        using var _c = client;
        using var _r = runner;
        var first = (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!;
        await Until(() => service.State(first) == "working", () => $"state {service.State(first)}");

        var second = await runner.StartHelpAsync("acp-stub", config, Machine);

        Assert.Null(second.SessionId);
        Assert.Contains(first, second.Message);
    }

    /// <summary>
    /// HELP5: Claude Code defers every MCP tool behind a search step unless its environment says otherwise,
    /// so the helper paid a model round trip to find its own tools. The switch is the adapter's to name,
    /// since it is a fact about that harness; both Claude Code doors name it, and nothing else does.
    /// </summary>
    [Theory]
    [InlineData("claude-code", true)]
    [InlineData("claude-code-acp", true)]
    [InlineData("stub", false)]
    [InlineData("acp-stub", false)]
    [InlineData("dsh", false)]
    [InlineData("codex-acp", false)]
    public void Only_the_claude_code_doors_say_how_their_tools_load_up_front(string adapter, bool says)
    {
        var upFront = AdapterSet.Built().Resolve(adapter).ToolsUpFront;

        if (says) Assert.Equal(new Dictionary<string, string> { ["ENABLE_TOOL_SEARCH"] = "false" }, upFront);
        else Assert.Empty(upFront);
    }

    /// <summary>
    /// HELP5: the help room's spawn carries the adapter's switch on both doors, so its knowledge tools come
    /// with the first request. A repository's conversation keeps the harness's own default, and an adapter
    /// that declares no switch has nothing added.
    /// </summary>
    /// <remarks>The harness behind each door is a script that writes down what its environment said.</remarks>
    [Theory]
    [InlineData("claude-code", true, true)]
    [InlineData("claude-code-acp", true, true)]
    [InlineData("claude-code", false, false)]
    [InlineData("claude-code-acp", false, false)]
    [InlineData("stub", true, false)]
    [InlineData("acp-stub", true, false)]
    public async Task Only_Ask_Daoris_loads_its_tools_up_front_and_only_where_the_harness_says_how(
        string adapter, bool help, bool upFront)
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var seen = Path.Combine(_home, "environment.txt");
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { [adapter] = ["node", EnvironmentAgent(), seen] },
        };
        var adapters = AdapterSet.Built();
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var start = help
            ? await runner.StartHelpAsync(adapter, config, Machine)
            : await runner.StartAsync("engine", adapter, config);
        var id = start.SessionId ?? throw new InvalidOperationException(start.Message);
        await Until(() => File.Exists(seen), () => $"state {service.State(id)}");

        // What the machine running the test already carries is the harness's default here, not Daoris's.
        var ambient = Environment.GetEnvironmentVariable("ENABLE_TOOL_SEARCH") ?? "(unset)";
        Assert.Equal(upFront ? "false" : ambient, File.ReadAllText(seen));

        Assert.True(runner.Finish(id));
        await Until(() => service.State(id) is "completed" or "stopped", () => $"state {service.State(id)}");
    }

    [Fact]
    public async Task An_agent_that_names_no_harness_this_machine_knows_is_refused_before_any_record()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var (runner, _, _, client, config) = Runner(service);
        using var _c = client;
        using var _r = runner;

        await Assert.ThrowsAsync<DriverException>(() => runner.StartHelpAsync("no-such-agent", config, Machine));

        Assert.False(Directory.Exists(HelpRoom.PathOf(_home)));
    }

    private static async Task Until(Func<bool> condition, Func<string> seen)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException(seen());
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// A harness for either door that writes down what its environment says of <c>ENABLE_TOOL_SEARCH</c>,
    /// then answers the protocol door's requests plainly and ignores the native door's lines until input ends.
    /// </summary>
    private string EnvironmentAgent()
    {
        var script = Path.Combine(_home, "environment-agent.mjs");
        File.WriteAllText(script, """
            import { renameSync, writeFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            const argv = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.281 (Claude Code)'); process.exit(0); }
            if (argv.includes('auth')) { console.log('{"loggedIn": true}'); process.exit(0); }
            if (argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            // Written beside, then renamed, so the test never reads it half-written.
            writeFileSync(argv[0] + '.part', process.env.ENABLE_TOOL_SEARCH ?? '(unset)');
            renameSync(argv[0] + '.part', argv[0]);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { continue; }
              if (frame.id === undefined || !frame.method) continue;
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'up-front' } });
              } else {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }

    /// <summary>A protocol-door agent that writes down where its session opened and which servers it was handed.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-help-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const heard = process.argv[2];
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { note('RAW: ' + line); continue; }
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                note('session/new ' + JSON.stringify({ cwd: frame.params.cwd, servers: (frame.params.mcpServers ?? []).map((s) => s.name) }));
                // An agent that starts in its most permissive judging mode, as a person's own settings may make it.
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-help', modes: { currentModeId: 'auto',
                  availableModes: [{ id: 'default' }, { id: 'acceptEdits' }, { id: 'auto' }, { id: 'bypassPermissions' }] } } });
              } else if (frame.method === 'session/set_mode') {
                note('session/set_mode ' + frame.params.modeId);
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const said = frame.params.prompt[0].text;
                note('prompt: ' + JSON.stringify(said));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-help',
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard ' + said } } } });
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-help',
                  update: { sessionUpdate: 'usage_update', used: said.length * 1000, size: 200000 } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }
}
