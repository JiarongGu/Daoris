using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;
using SaidStandIn = Daoris.Desktop.Driver.Tests.SessionMessagesTickTests.SaidStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCESSSWAP1b (D183's note, D184's AUTONOMY1b) through REAL ticks: a driven session's changed setup applies at its next turn. The
/// session starts under `asking`; while its first turn runs the person holds a word for it and the terminal's <c>daoris-driver access</c>
/// writes `unrestricted` to the driver's file from outside the run. At that turn's end the run reads the file, ends there, never
/// mid-turn, and hands the word back to its record, which concludes completed, neither parked nor failed; the next look goes on in the
/// same conversation under the new setup, saying the change. On the protocol door and on the native door.
/// </summary>
/// <remarks>The stand-in service is <see cref="SessionMessagesTickTests.SaidStandIn"/>: words kept on an ended record, the setup kept.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AccessSwapTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-swap-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public AccessSwapTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "swap@example.com");
        Git("config", "user.name", "ACCESSSWAP1b");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private string Go => Path.Combine(_home, "go.flag");

    private string Taken => Path.Combine(_home, "taken.flag");

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events;

    /// <summary>The terminal's door, as `daoris-driver access engine permissions=…` writes the driver's file from another process.</summary>
    private void Terminal(string permissions)
    {
        var ask = AccessCommand.Read(["engine", $"permissions={permissions}"], out var problem) ?? throw new InvalidOperationException(problem);
        Assert.Equal(0, AccessCommand.Run(ask, ConfigPath, place: null, TextWriter.Null));
    }

    /// <summary>A look's driver, as the watch builds one: the file as it reads now, and its path for a run to read again.</summary>
    private Daoris.Driver.Driver Look(SaidStandIn service, AdapterSet adapters, SessionProcesses processes) => new(
        new ServiceClient(service.Url, null), DriverConfig.Load(ConfigPath), adapters, _home, processes: processes,
        harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
    {
        ConfigPath = ConfigPath,
    };

    /// <summary>
    /// 🔴 The protocol door: the word held at the first turn's end is never prompted into the process opened under `asking`. The run
    /// ends, the record concludes completed with the change said and the word kept on it; the next look resumes the same conversation
    /// in the agent's mode that asks nothing, says the change, keeps the new setup on the record, and the word is its prompt.
    /// </summary>
    [Fact]
    public async Task A_protocol_session_whose_setup_changed_mid_turn_ends_at_its_turns_end_and_goes_on_under_the_new_one()
    {
        (DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", ProtocolAgent(), Log, Go] },
        }).WithAccess(new AccessEdit { Repository = "engine", Set = [new(AccessRules.Permissions, AccessRules.Asking)] }).Save(ConfigPath);
        await using var service = SaidStandIn.Start(_repository);
        var built = AdapterSet.Built();
        var adapters = new AdapterSet(built.Names.ToDictionary(
            name => name, name => name == "acp-stub" ? new PosturedStub() : built.Resolve(name), StringComparer.OrdinalIgnoreCase));
        var processes = new SessionProcesses();

        var tick = Look(service, adapters, processes).RunOnceAsync();
        await Poll.Until(() => service.Status("q1") == "Taken" && processes.InboxOf("s1") is not null, () => "the first run never took its quest");
        Assert.True(processes.InboxOf("s1")!.Hold(new ChatMessage("Also log the port.", [])));
        Terminal(AccessRules.Unrestricted);
        File.WriteAllText(Go, "");
        await tick.WaitAsync(Poll.Patience);

        var first = $"record: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(service.State("s1") == "completed", first);
        Assert.Equal("Taken", service.Status("q1"));
        Assert.True(service.Said("s1").Count == 1, first);
        Assert.Contains("its permission prompts changed while it worked, from `asking` to `unrestricted`", service.Note("s1"));
        Assert.Equal(AccessRules.Asking, service.Permissions("s1"));
        // Never prompted into the process opened under the old setup.
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Contains(Events("s1"), e => e.Parts?.Any(part => part.Code == NoteCodes.EndedWordsSetup.Code) == true);
        Assert.DoesNotContain(Events("s1"), e => e.Text?.Contains("never reached", StringComparison.Ordinal) == true);

        var look = await Look(service, adapters, processes).RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.Said("s1").Count == 0, seen);
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), seen);
        var heard = Heard();
        var resumedAt = heard.ToList().FindIndex(line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.True(resumedAt >= 0, seen);
        Assert.Equal("conv-s1", heard[resumedAt]["conversation"]!.GetValue<string>());
        Assert.Equal("bypassPermissions", heard.Skip(resumedAt).Single(line => line["method"]!.GetValue<string>() == "session/set_mode")["mode"]!.GetValue<string>());
        var prompt = heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>(), seen);
        Assert.Equal(["Also log the port."], prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.Note, Text: { } text }
            && text.StartsWith("Permission prompts changed since `s1` started: it started under `asking`, and this run goes on under `unrestricted`.", StringComparison.Ordinal));
        Assert.Equal(AccessRules.Unrestricted, service.Permissions("s1"));
    }

    /// <summary>
    /// The native door: each turn after the first is a resumed run, which went on under the start's setup inside the same record. A word
    /// held when the first run ends is handed back where the file now names another setup: the record concludes, its quest's done kept
    /// and the change said, and the next look resumes the conversation with `bypassPermissions` beside `--resume`.
    /// </summary>
    [Fact]
    public async Task A_native_session_whose_setup_changed_mid_turn_goes_on_under_the_new_one_in_its_next_run()
    {
        var profile = DaorisHome.Entry(_home, HomeEntry.AgentHomes, "claude-code", "work");
        Directory.CreateDirectory(profile);
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Harnesses), """{"defaults":{"claude-code":"work"}}""");
        (DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "claude-code",
            TimeoutMinutes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", NativeHarness(), Log, Taken, Go] },
        }).WithAccess(new AccessEdit { Repository = "engine", Set = [new(AccessRules.Permissions, AccessRules.Asking)] }).Save(ConfigPath);
        await using var service = SaidStandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var processes = new SessionProcesses();

        var tick = Look(service, adapters, processes).RunOnceAsync();
        await Poll.Until(() => File.Exists(Taken) && processes.InboxOf("s1") is not null, () => "the first run never took its quest");
        Assert.True(processes.InboxOf("s1")!.Hold(new ChatMessage("Also log the port.", [])));
        Terminal(AccessRules.Unrestricted);
        File.WriteAllText(Go, "");
        await tick.WaitAsync(Poll.Patience);

        var first = $"record: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        var runs = Heard();
        Assert.True(runs.Length == 1, first);
        Assert.Equal("default", runs[0]["mode"]!.GetValue<string>());
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), first);
        Assert.True(service.Said("s1").Count == 1, first);
        Assert.Contains("its permission prompts changed while it worked, from `asking` to `unrestricted`", service.Note("s1"));

        var look = await Look(service, adapters, processes).RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        runs = Heard();
        Assert.True(runs.Length == 2, seen);
        Assert.Equal(("native-1", "Also log the port.", "bypassPermissions"),
            (runs[1]["resume"]!.GetValue<string>(), runs[1]["prompt"]!.GetValue<string>(), runs[1]["mode"]!.GetValue<string>()));
        Assert.True(service.SessionCount == 1 && service.Said("s1").Count == 0, seen);
        Assert.Equal(AccessRules.Unrestricted, service.Permissions("s1"));
    }

    /// <summary>The protocol stub with postures: Claude's words for its auto, asking and unrestricted modes.</summary>
    private sealed class PosturedStub : ISessionAdapter
    {
        private readonly AcpStubAdapter _stub = new();

        public string Name => _stub.Name;

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public string? AcpPosture => "auto";

        public string? AskingPosture => "default";

        public string? FullPosture => "bypassPermissions";

        public HarnessToolchain? Toolchain => _stub.Toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => _stub.Prepare(target, command);

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => _stub.PrepareChat(target, command);
    }

    /// <summary>
    /// The protocol door's stand-in: it resumes, and offers Claude's modes on each session, a mode set going to the log. Its first
    /// prompt takes the quest and holds its turn until the test writes <c>go.flag</c>; a prompt to a resumed conversation closes it.
    /// A later prompt in the same process is what the swap must never send; it answers it so the run would still end.
    /// </summary>
    private string ProtocolAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync } from 'node:fs';
            const [log, go] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const modes = { modes: { currentModeId: 'auto', availableModes: [{ id: 'default' }, { id: 'auto' }, { id: 'bypassPermissions' }] } };
            let conversation = null;
            let resumed = false;
            let prompts = 0;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation, ...modes } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: { ...modes } });
              } else if (frame.method === 'session/set_mode') {
                heard({ method: 'session/set_mode', mode: frame.params?.modeId });
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                prompts += 1;
                if (resumed) {
                  say('Logged the port, as you said.');
                  await respond({ action: 'done', reason: 'served the report and logged its port' });
                } else if (prompts === 1) {
                  await respond({ action: 'take' });
                  for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
                  say('Serving it; the port is next.');
                } else {
                  say('Took your words in the process opened under the old setup.');
                }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>
    /// A stand-in for Claude Code's native door that logs the permission mode it was started in: a first run takes the quest, says so
    /// in a file, waits for the test's word to go on, and closes it; a resumed run logs what it was resumed with.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, writeFileSync } from 'node:fs';
            const [log, taken, go, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            const after = (flag) => { const at = argv.indexOf(flag); return at < 0 ? null : argv[at + 1]; };
            const prompt = after('-p');
            const resume = after('--resume');
            const url = process.env.DAORIS_SERVICE_URL;
            const line = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            appendFileSync(log, JSON.stringify({ resume, prompt: resume ? prompt : null, mode: after('--permission-mode') }) + '\n');
            line({ type: 'system', subtype: 'init', session_id: resume ?? 'native-1', cwd: process.cwd(), tools: [], model: 'stub' });
            if (!resume) {
              await respond({ action: 'take' });
              writeFileSync(taken, '');
              for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
              await respond({ action: 'done', reason: 'served the report' });
            }
            line({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: resume ? 'Logged the port.' : 'Served.' }] } });
            line({ type: 'result', subtype: 'success', is_error: false, result: 'ok' });
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
