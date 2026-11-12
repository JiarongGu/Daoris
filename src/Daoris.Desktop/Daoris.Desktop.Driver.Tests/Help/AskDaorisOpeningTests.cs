using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask Daoris's opening is bounded: a harness that never opens its conversation, waiting on a server that never answers, fails the start
/// past its bound saying what did not come up, its process ended; and the person's stop reaches an opening whose process is not started.
/// </summary>
/// <remarks>A real process (node) speaking the protocol door, a stand-in server that never answers, and a stand-in service.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AskDaorisOpeningTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-ask-opening-" + Guid.NewGuid().ToString("N")[..8]);

    public AskDaorisOpeningTests()
    {
        Directory.CreateDirectory(Path.Combine(_home, "engine"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "daoris"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly HelpMachine Machine = new()
    {
        Adapter = "acp-stub",
        Helper = "acp-stub",
        Cap = 1,
        Repositories = [new("engine", "default") { Checkout = true, Drivable = true }],
    };

    private string Heard => Path.Combine(_home, "heard.txt");

    private (ChatRunner Runner, SessionEvents Events, SessionProcesses Processes, ServiceClient Client, DriverConfig Config) Runner(StandInService service)
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard, Server()] },
        };
        var adapters = ProviderRegistry.Built();
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var client = new ServiceClient(service.Url, null);
        var runner = new ChatRunner(
            client, adapters, _home, processes, events: events, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            OpeningBound = TimeSpan.FromSeconds(2),
        };
        return (runner, events, processes, client, config);
    }

    /// <summary>
    /// 🔴 The owner's case: a start sat at *Opening Ask Daoris* for nine minutes. A harness waiting on a server that never answers never
    /// opens its conversation; past the bound the start fails, said on the conversation with what did not come up, and the record ends.
    /// </summary>
    [Fact]
    public async Task A_server_that_never_answers_fails_the_start_past_its_bound_saying_what_did_not_come_up()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var (runner, events, _, client, config) = Runner(service);
        using var _c = client;
        using var _r = runner;

        var id = (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!;

        string Seen() => $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], state {service.State(id)}, record ["
            + string.Join(" | ", events.Page(id).Events.Select(e => $"{e.Kind}:{e.Text}")) + "]";
        await Poll.Until(() => service.State(id) == "failed", Seen);
        var said = Assert.Single(events.Page(id).Events, e => e.Parts?.Any(part => part.Code == "chat.help-not-up") == true);
        Assert.StartsWith("Ask Daoris could not start: `acp-stub`", said.Text);
        Assert.Contains("did not come up within 2 s", said.Text);
        // The servers it waits on that Daoris's own work needs are named; a plugin's never is.
        foreach (var server in StubFile.Lines(Heard).Where(line => line.StartsWith("waiting on ", StringComparison.Ordinal)))
        {
            if (server == $"waiting on {KnowledgeConnector.ServerName}") Assert.Contains($"`{KnowledgeConnector.ServerName}`", said.Text);
        }
    }

    /// <summary>
    /// The person's stop reaches an opening whose process is not started yet: its record ends as their stop, never as a failure, and the
    /// start answers in the stop's words. The opening here waits on the service's move of its record.
    /// </summary>
    [Fact]
    public async Task The_persons_stop_reaches_an_opening_before_its_process_is_started()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"), writeDelay: TimeSpan.FromSeconds(2));
        var (runner, _, processes, client, config) = Runner(service);
        using var _c = client;
        using var _r = runner;
        runner.OpeningBound = TimeSpan.FromSeconds(30);

        var placed = await runner.PlaceHelpAsync("acp-stub", config);
        var id = placed.SessionId!;
        var starting = runner.RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(Machine));
        // The person's stop, as STOP_SESSION makes it, pressed until the opening is there to reach.
        await Poll.Until(() => processes.Stop(id) || starting.IsCompleted, () => $"state {service.State(id)}");

        var start = await starting;

        Assert.Null(start.SessionId);
        Assert.Equal("stopped", service.State(id));
        Assert.Contains("the person ended the conversation", start.Message);
        Assert.False(File.Exists(Heard));
    }

    /// <summary>A server that reads its input and never answers, as the room's tool server did once stopped.</summary>
    private string Server()
    {
        var script = Path.Combine(_home, "silent-server.mjs");
        File.WriteAllText(script, """
            process.stdin.resume();
            setTimeout(() => process.exit(3), 120000);
            """);
        return script;
    }

    /// <summary>
    /// A protocol-door harness that, at <c>session/new</c>, brings up each server it is handed (here the silent stand-in in each one's
    /// place) and opens its conversation only once they answer, which they never do.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-opening-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { spawn } from 'node:child_process';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, silent] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { continue; }
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                for (const server of frame.params.mcpServers ?? []) {
                  note('waiting on ' + server.name);
                  const child = spawn(process.execPath, [silent], { stdio: ['pipe', 'pipe', 'ignore'] });
                  child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {} }) + '\n');
                }
                note('session/new waits');
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }
}
