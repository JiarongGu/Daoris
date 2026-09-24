using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven session on the protocol door leaves its conversation beside its transcript (D76, CONV1).
/// </summary>
/// <remarks>
/// Through a REAL tick, as INT4i's tests are: node as the stand-in harness, a stand-in service on a
/// loopback port, no model and no account. What is under test is the whole path the page will read
/// back after a restart — the target the driver composed, what the wire said, and how the turn ended —
/// in the file the driver writes, not in a list a test handed it.
/// </remarks>
public sealed class SessionEventRecordTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-event-record-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public SessionEventRecordTests()
    {
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "conv1@example.com");
        Git("config", "user.name", "CONV1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_protocol_door_session_leaves_its_conversation_beside_its_transcript()
    {
        await using var service = DrivenSessionInputTests.StandInService.Start(_repository);
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var live = new List<SessionEvent>();
        events.Evented += (_, e) => { lock (live) live.Add(e); };

        await Driver(service, events).TickAsync();

        // Read back by a NEW instance, the way a page opened after a restart reads it.
        var page = new SessionEvents(Path.Combine(_home, "sessions")).Page("s1");
        var kinds = page.Events.Select(e => e.Kind).ToList();

        var target = page.Events[0];
        Assert.Equal(SessionEventKind.User, target.Kind);
        Assert.Equal("target", target.Origin);
        Assert.Contains("q1", target.Text);

        Assert.Contains(SessionEventKind.Message, kinds);
        Assert.Equal("looking at the budget", page.Events.First(e => e.Kind == SessionEventKind.Message).Text);
        Assert.Equal(SessionEventKind.Turn, kinds[^1]);
        Assert.Equal("end_turn", page.Events[^1].StopReason);

        // The same events went out live, in the same order, for a page that was watching.
        lock (live) Assert.Equal(page.Events.Select(e => e.Seq), live.Select(e => e.Seq));

        // And the transcript is still the verbatim text it always was.
        Assert.Contains("looking at the budget", File.ReadAllText(Path.Combine(_home, "sessions", "s1.log")));
    }

    private Daoris.Driver.Driver Driver(DrivenSessionInputTests.StandInService service, SessionEvents events)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent()] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")), events: events);
    }

    /// <summary>A protocol agent that says one thing, ends its turn, and exits when its input closes.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { continue; }
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1',
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'looking at the budget' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
