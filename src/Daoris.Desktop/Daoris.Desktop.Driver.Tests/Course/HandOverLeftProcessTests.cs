using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A hand-over waits for what the outgoing part left running before the next part starts
/// in the same tree. The old conversation's turn starts a command in the background through a step that exits at once, so the command is
/// nobody's child, out of reach of a walk of the agent's tree, as a dev server is; the turn ends with it still running. Handed over,
/// the new part's agent finds it gone as it starts, and the old record says what Daoris ended and waited for.
/// </summary>
/// <remarks>
/// Real processes in a real job, so the class is in the real-process half, and a job is Windows': elsewhere the case returns at once. The
/// in-process ledger of <see cref="ChatGoOnTests"/>, as <see cref="HandOverProcessTests"/> runs it.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOverLeftProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-handover-left-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public HandOverLeftProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        // A case that failed may leave the command running: it is ended here, never left on the machine.
        if (LeftPid() is { } pid)
        {
            try
            {
                using var left = Process.GetProcessById(pid);
                left.Kill();
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }

        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string PidFile => Path.Combine(_home, "left.pid");

    private int? LeftPid() => File.Exists(PidFile) && int.TryParse(File.ReadAllText(PidFile).Trim(), out var pid) ? pid : null;

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Heard, Middle(), Left(), PidFile],
                ["stub"] = ["node", TextAgent(), PidFile],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private ChatRunner Runner(ServiceClient client, SessionEvents events)
    {
        var adapters = ProviderRegistry.Built();
        return new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], state {ledger.State(id)}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

    private static string? CodeOf(SessionEvent e) => e.Kind == SessionEventKind.Note ? e.Parts?.FirstOrDefault(part => part.Code is not null)?.Code : null;

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 🔴 The old part's turn leaves a command running that no walk of its tree reaches. Handed over, the command is ended and waited for
    /// before the new part starts: the new part's agent finds it gone at its start, and the old record says Daoris ended what it left, before
    /// its last line, which says where it went.
    /// </summary>
    [Fact]
    public async Task A_hand_over_waits_for_a_command_the_old_part_left_in_the_background_before_the_new_part_starts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);
        var old = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "background"));
        await Poll.Until(() => events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, old));
        await Poll.Until(() => !runner.Taking(old), () => Seen(ledger, events, old));
        // The turn is over, and its command still runs, its parent gone.
        var pid = LeftPid();
        Assert.NotNull(pid);
        Assert.True(Alive(pid.Value), "the command the turn left is running as the turn ends");

        var handed = await runner.HandOverAsync(old, "stub", null, HandOverWhy.Press, HandOverBy.Person, config);

        Assert.True(handed.Kept, handed.Message);
        var next = handed.SessionId!;
        // 🔴 The new part's agent found nothing of the old part's running as it started.
        await Poll.Until(() => File.Exists(PidFile + ".seen"), () => Seen(ledger, events, next));
        Assert.Equal("gone", File.ReadAllText(PidFile + ".seen").Trim());
        Assert.False(Alive(pid.Value));

        // 🔴 Said on the old record, before the line that says where it went.
        await Poll.Until(() => events.After(old, 0).Events.Any(e => CodeOf(e) == "chat.handed-over"), () => Seen(ledger, events, old));
        var codes = events.After(old, 0).Events.Select(CodeOf).Where(code => code is not null).ToList();
        Assert.Contains("chat.left-ended", codes);
        Assert.True(codes.IndexOf("chat.left-ended") < codes.LastIndexOf("chat.handed-over"), string.Join(", ", codes));
        var left = events.After(old, 0).Events.First(e => CodeOf(e) == "chat.left-ended").Parts![0];
        Assert.True(int.TryParse(left.Value("count")?.ToString(), out var count) && count >= 1, $"count {left.Value("count")}");
        Assert.DoesNotContain("chat.left-running", codes);
    }

    /// <summary>The command a turn leaves running: it writes down its own id, then waits two minutes, long past any case.</summary>
    private string Left()
    {
        var script = Path.Combine(_home, "left.mjs");
        File.WriteAllText(script, """
            import { writeFileSync } from 'node:fs';
            writeFileSync(process.argv[2], String(process.pid));
            setTimeout(() => {}, 120000);
            """);
        return script;
    }

    /// <summary>The step that starts the command in the background and exits at once, so the command is nobody's child.</summary>
    private string Middle()
    {
        var script = Path.Combine(_home, "middle.mjs");
        File.WriteAllText(script, """
            import { spawn } from 'node:child_process';
            const [left, pidFile] = process.argv.slice(2);
            spawn(process.execPath, [left, pidFile], { detached: true, stdio: 'ignore', windowsHide: true }).unref();
            """);
        return script;
    }

    /// <summary>
    /// A stand-in ACP agent: names its conversation <c>conv-1</c>, writes down each prompt and answers it; a prompt <c>background</c> starts
    /// the command through the step and answers once the command has written its id.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-left-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync } from 'node:fs';
            import { spawn } from 'node:child_process';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, middle, left, pidFile] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const raw = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                const text = raw.replace(/(\n\n| \| )?Daoris's standing block for this session: [\s\S]*$/, '');
                note('prompt: ' + text);
                if (text === 'background') {
                  const step = spawn(process.execPath, [middle, left, pidFile], { stdio: 'ignore', windowsHide: true });
                  await new Promise((resolve) => step.on('exit', resolve));
                  while (!existsSync(pidFile)) await new Promise((resolve) => setTimeout(resolve, 20));
                }
                await new Promise((resolve) => setTimeout(resolve, 100));
                say('heard ' + text);
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>The new part's text-only agent: as it starts it writes down whether the old part's command still runs, then echoes each line.</summary>
    private string TextAgent()
    {
        var script = Path.Combine(_home, "text-left-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, readFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [pidFile] = process.argv.slice(2);
            if (pidFile && existsSync(pidFile) && !existsSync(pidFile + '.seen')) {
              let seen = 'gone';
              try { process.kill(Number(readFileSync(pidFile, 'utf8').trim()), 0); seen = 'alive'; } catch { seen = 'gone'; }
              appendFileSync(pidFile + '.seen', seen + '\n');
            }
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => console.log('heard: ' + line));
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
