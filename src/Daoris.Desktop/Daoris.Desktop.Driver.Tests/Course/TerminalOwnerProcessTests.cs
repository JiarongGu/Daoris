using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation a terminal owns, with a real protocol-door process (CLITURN1): the terminal's runner creates its own registry, so
/// another Daoris process on the home is left to the owner's word for the turns, and to the owner for a stop. Two registries over
/// one home stand for the two processes: the terminal's, which runs the harness, and the window's, which only shares its markers.
/// </summary>
/// <remarks>The conversation runs on the <c>acp-stub</c> adapter, whose prompt is answered only once a file appears, so a turn is running for as long as the test says.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class TerminalOwnerProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-owner-proc-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public TerminalOwnerProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Release => Path.Combine(_home, "release");

    private string Markers => DaorisHome.Entry(_home, HomeEntry.SessionPids);

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Release] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private static string Seen(TurnMarks marks, string id) => $"mark {marks.Read(id)}";

    /// <summary>
    /// A turn the terminal runs is told from its start to its end: the mark the home keeps says taking while the harness works, and
    /// not taking, with the moment it ended, once it answered. The window's registry holds nothing of it.
    /// </summary>
    [Fact]
    public async Task A_turn_a_terminal_runs_is_told_from_its_start_to_its_end()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        using var client = ledger.Client();
        var terminal = new SessionProcesses(Markers);
        using var runner = new ChatRunner(client, AdapterSet.Built(), _home, terminal);
        var id = (await runner.StartAsync("engine", "acp-stub", Config())).SessionId!;
        await using var owner = new TerminalOwner(_home, id, runner, terminal, looking: TimeSpan.FromMilliseconds(50));
        var marks = new TurnMarks(_home);
        var window = new SessionProcesses(Markers);

        Assert.True(runner.Say(id, "Make the port configurable."));
        await Poll.Until(() => marks.Read(id)?.Taking == true, () => Seen(marks, id));
        Assert.True(window.AliveOnThisMachine(id));
        Assert.False(window.RunsHere(id));

        await File.WriteAllTextAsync(Release, "go");
        await Poll.Until(() => marks.Read(id) is { Taking: false, LastTurnEnded: not null }, () => Seen(marks, id));

        runner.Finish(id);
    }

    /// <summary>
    /// A stop the window asks of a chat it does not run is taken by the terminal that does: the harness is stopped as the person's,
    /// and the terminal's leaving takes its mark with it.
    /// </summary>
    [Fact]
    public async Task A_stop_asked_by_another_process_is_taken_by_the_terminal_that_owns_the_chat()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        using var client = ledger.Client();
        var terminal = new SessionProcesses(Markers);
        using var runner = new ChatRunner(client, AdapterSet.Built(), _home, terminal);
        var id = (await runner.StartAsync("engine", "acp-stub", Config())).SessionId!;
        var marks = new TurnMarks(_home);
        var window = new SessionProcesses(Markers);
        var owner = new TerminalOwner(_home, id, runner, terminal, looking: TimeSpan.FromMilliseconds(50));

        // The harness is marked once its door has opened, a moment after the start answers.
        await Poll.Until(() => window.AliveOnThisMachine(id), () => $"markers [{string.Join(", ", Directory.GetFiles(Markers))}]");
        var asked = await SessionMoves.StopAsync(window, null, id);
        Assert.True(asked.Elsewhere);

        Assert.True(await SessionMoves.StopThroughOwnerAsync(
            _home, id, Poll.Patience, TimeSpan.FromMilliseconds(25), () => DateTimeOffset.UtcNow));

        // Taken is the owner stopping it, a moment before the stop is made; the record then says the person stopped it.
        await Poll.Until(() => ledger.State(id) == "stopped", () => $"the record says {ledger.State(id)}");
        await owner.DisposeAsync();
        Assert.Null(marks.Read(id));
    }

    /// <summary>A stand-in ACP agent that answers a prompt only once the file it is handed exists.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-held-agent.mjs");
        File.WriteAllText(script, """
            import { existsSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [release] = process.argv.slice(2);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'conv-1' } });
              } else if (frame.method === 'session/prompt') {
                while (!existsSync(release)) await new Promise((resolve) => setTimeout(resolve, 25));
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
