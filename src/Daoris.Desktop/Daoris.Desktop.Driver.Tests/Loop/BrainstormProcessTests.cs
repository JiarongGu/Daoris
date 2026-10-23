using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a (D176 points 1-4 and 8): a brainstorm's whole round trip on the real path, with no model. A request about a
/// repository whose checkout real git reads is frozen at a look (no service: the declared index's files answer), sent, and run on
/// two real processes, the native stub and the ACP stub; the native stub synthesizes; the document is kept with each member's
/// provenance, the rooms go, and the asker's checkout is untouched. BRAINSTORM1a2: the ACP stub answers as a real maker does, by
/// calling `brainstorm_give` on the one connector its turn was handed, over MCP, the development build's connector; the native stub
/// still writes the file the tool writes.
/// </summary>
/// <remarks>
/// Real git and real processes, so the class is in the real-process half (MOD8): a branch's worktree does not run it, the parent
/// runs it on the merge. The service is a port nobody holds: a brainstorm asks it only for a search, and does without. It needs
/// `Daoris.Service.Mcp` built, as <see cref="KnowledgeConnectorStartTests"/> does, and hands the driver an environment naming its own
/// scratch for home, connector and store (ENVSEAM1), so the connector it starts never opens another home. It sets nothing of this
/// process's, so it runs beside the rest of the Process half (PROCPAR1).
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class BrainstormProcessTests : IDisposable
{
    private readonly GitTree _repository = new("brainstorm-pass");
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-pass-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The environment the driver is handed (ENVSEAM1): this run's scratch, and nothing of this process's.</summary>
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    public BrainstormProcessTests()
    {
        Directory.CreateDirectory(_home);
        _repository.Commit("report.md");
    }

    /// <summary>
    /// The connector the driver offers a pass found and started on this run's scratch alone: the development build named outright,
    /// its home this run's, and its store and the folder it reads empty scratch of their own. The driver reads them from the
    /// environment it is handed (<see cref="Variable"/>).
    /// </summary>
    private void OwnConnector()
    {
        var host = KnowledgeConnector.Candidates(null, null, WorkspaceRoot.Folder)
            .Where(path => path.Contains(Path.Combine("Daoris.Service.Mcp", "bin")))
            .FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("build src/Daoris.Service/Daoris.Service.Mcp first");
        var store = Directory.CreateDirectory(Path.Combine(_home, "store")).FullName;
        foreach (var (name, value) in new[]
                 {
                     (KnowledgeConnector.PathVariable, host), (DaorisHome.Variable, _home),
                     ("DAORIS_KNOWLEDGE_ROOT", Directory.CreateDirectory(Path.Combine(store, "family")).FullName),
                     ("DAORIS_KNOWLEDGE_DB", Path.Combine(store, "knowledge.db")),
                 })
        {
            _variables[name] = value;
        }
    }

    /// <summary>A variable of the environment the driver is handed.</summary>
    private string? Variable(string name) => _variables.GetValueOrDefault(name);

    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// What both stubs do in a turn: say what they saw (their folder, the packet's hash, the variables) in the home's <c>saw</c>
    /// folder, then answer: <c>work</c> writes the file the result tool writes (a generator one proposal, the synthesis one group of
    /// all), and <c>give</c> calls <c>brainstorm_give</c> on the turn's connector over MCP, started from its entry's own variables
    /// alone as Codex starts one: first an answer the validator refuses, then the same answer whole, each tool answer kept under
    /// the home's <c>tool</c> folder with the servers the turn was handed.
    /// </summary>
    private string Work()
    {
        var home = _home.Replace("\\", "/");
        return $$"""
            const see = () => {
              const turn = process.env.DAORIS_BRAINSTORM_TURN;
              const packet = readFileSync(join(process.cwd(), 'packet.json'));
              mkdirSync('{{home}}/saw', { recursive: true });
              writeFileSync('{{home}}/saw/' + turn + '.json', JSON.stringify({
                cwd: process.cwd(), packet: createHash('sha256').update(packet).digest('hex'),
                repository: process.env.DAORIS_REPOSITORY ?? null, quest: process.env.DAORIS_QUEST_ID ?? null,
                files: readdirSync(process.cwd()).sort(),
              }));
            };
            const proposal = (turn) => ({ idea: 'Inclusive window from ' + turn, rationale: 'The report reads an exclusive window.',
              effort: 'small', risk: 'low', reversible: 'yes', undo: null, confidence: 'medium', assumptions: [], sources: [] });
            const give = async (servers) => {
              see();
              const turn = process.env.DAORIS_BRAINSTORM_TURN;
              const server = servers.find((each) => each.name === 'daoris-knowledge');
              const env = Object.fromEntries((server?.env ?? []).map((each) => [each.name, each.value]));
              const said = { servers: servers.map((each) => each.name), env: Object.keys(env).sort() };
              if (server) {
                const child = spawn(server.command, server.args ?? [], { env, stdio: ['pipe', 'pipe', 'ignore'] });
                const waiting = new Map();
                createInterface({ input: child.stdout }).on('line', (line) => {
                  let frame;
                  try { frame = JSON.parse(line); } catch { return; }
                  if (frame.id !== undefined && waiting.has(frame.id)) { waiting.get(frame.id)(frame); waiting.delete(frame.id); }
                });
                let next = 1;
                const ask = (method, params) => new Promise((resolve) => {
                  const id = next++;
                  waiting.set(id, resolve);
                  setTimeout(() => resolve({ error: 'no answer in 60 seconds' }), 60000);
                  child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n');
                });
                const text = (frame) => frame.result?.content?.map((each) => each.text).join('') ?? JSON.stringify(frame.error ?? frame);
                await ask('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'acp-stub', version: '0' } });
                child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');
                said.refused = text(await ask('tools/call', { name: 'brainstorm_give', arguments: { proposals: [{ ...proposal(turn), effort: 'tiny' }] } }));
                said.kept = text(await ask('tools/call', { name: 'brainstorm_give', arguments: { proposals: [proposal(turn)], needs: [] } }));
                child.stdin.end();
                child.kill();
              }
              mkdirSync('{{home}}/tool', { recursive: true });
              writeFileSync('{{home}}/tool/' + turn + '.json', JSON.stringify(said));
            };
            const work = () => {
              see();
              const turn = process.env.DAORIS_BRAINSTORM_TURN;
              const answer = process.env.DAORIS_BRAINSTORM_ANSWER;
              mkdirSync(dirname(answer), { recursive: true });
              if (turn.startsWith('s')) {
                const handed = JSON.parse(readFileSync(join(process.cwd(), 'proposals.json'), 'utf8')).proposals.map((p) => p.id);
                writeFileSync(answer, JSON.stringify({ brainstorm: 1, groups: [{ title: 'Every idea', idea: 'All of them, merged.',
                  members: handed, rank: 1, why: 'Nothing told them apart.', flags: [] }], leftOut: [] }));
              } else {
                writeFileSync(answer, JSON.stringify({ brainstorm: 1, needs: [], none: null, proposals: [proposal(turn)] }));
              }
            };
            """;
    }

    private const string Imports = """
        import { spawn } from 'node:child_process';
        import { createHash } from 'node:crypto';
        import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
        import { dirname, join } from 'node:path';
        import { createInterface } from 'node:readline';
        """;

    private string Native()
    {
        var script = Path.Combine(_home, "stub-agent.mjs");
        File.WriteAllText(script, $$"""
            {{Imports}}
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            {{Work()}}
            work();
            console.log('stub: answered through its tool');
            """);
        return script;
    }

    private string Protocol()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, $$"""
            {{Imports}}
            {{Work()}}
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const servers = [];
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                servers.push(...(frame.params.mcpServers ?? []));
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'conv-' + process.env.DAORIS_BRAINSTORM_TURN } });
              } else if (frame.method === 'session/prompt') {
                if (process.env.DAORIS_BRAINSTORM_TURN.startsWith('g')) await give(servers);
                else work();
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: frame.params.sessionId,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'answered through its tool' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn', _meta: { quota: { model_usage: [{ model: 'brainstorm-reported' }] } } } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => process.exit(0));
            """);
        return script;
    }

    /// <summary>
    /// 🔴 The whole round trip on the native stub and the ACP stub: one packet, byte for byte, in every room; each answer validated
    /// and its provenance re-attached; the document kept and saying stubs are no maker diversity; the rooms gone; and no commit,
    /// branch or change in the asker's checkout.
    /// </summary>
    [Fact]
    public async Task Two_stub_doors_answer_a_sent_brainstorm_and_the_document_is_kept_with_nothing_changed_in_the_checkout()
    {
        OwnConnector();
        var refs = _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\"");
        var head = _repository.Output("rev-parse HEAD");
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null),
            DriverConfig.Empty with
            {
                Adapter = "stub", Cap = 3, TestBound = 2,
                Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Native()], ["acp-stub"] = ["node", Protocol()] },
            },
            adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Variable = Variable,
        };
        var files = new BrainstormFiles(_home);
        var id = files.WriteRequest("Why does the daily report drop its last day?", "reports", ["stub", "acp-stub"], DateTimeOffset.UtcNow);
        var snapshot = new Snapshot([], [new RepoView("reports", true, _repository.Root)], []);

        await driver.BrainstormsAsync(snapshot, 0, [], CancellationToken.None);
        var state = files.ReadState(id)!;
        Assert.Equal(BrainstormStates.Frozen, state.State);
        var packet = BrainstormPackets.Read(files.PacketBytes(id)!)!;
        Assert.Equal((BrainstormPackets.Files, head), (packet.Tier, packet.Sources.FirstOrDefault()?.Revision ?? head));

        files.WriteSent(id, state.Packet!, DateTimeOffset.UtcNow);
        var events = new List<string>();
        await driver.BrainstormsAsync(snapshot, 0, events, CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(120));

        Assert.True(files.ReadState(id)!.State == BrainstormStates.Ended, string.Join('\n', events) + "\n" + File.ReadAllText(files.PathOf(id, "state.json")));

        // USAGECAP1a4: a pass's turns are kept under the account its walk chose; the stubs have a toolchain, so each owns its name.
        var passTurns = UsageHistory.ReadTurns(_home).Turns.Where(turn => turn.Session.StartsWith($"brainstorm-{id}-", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(passTurns);
        Assert.All(passTurns, turn => Assert.NotNull(turn.Agent));

        // One packet, byte for byte, in every room; each room the packet and its instruction (and the synthesis's proposals) alone.
        var frozen = Convert.ToHexStringLower(SHA256.HashData(files.PacketBytes(id)!));
        var saw = Directory.EnumerateFiles(Path.Combine(_home, "saw")).Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToList();
        Assert.Equal(3, saw.Count);
        Assert.All(saw, each => Assert.Equal(frozen, each["packet"]!.GetValue<string>()));
        Assert.All(saw, each => Assert.Null(each["quest"]));
        Assert.All(saw, each => Assert.Null(each["repository"]));
        Assert.All(saw, each => Assert.StartsWith(files.Rooms(id), each["cwd"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(saw, each => each["files"]!.AsArray().Select(name => name!.GetValue<string>()).SequenceEqual(["instruction.md", "packet.json", "proposals.json"]));

        // The document: ranked by the stub synthesis, each member's provenance re-attached, and no maker diversity claimed.
        var document = JsonNode.Parse(File.ReadAllText(files.PathOf(id, "document.json")))!;
        Assert.True(document["ranked"]!.GetValue<bool>());
        var members = document["rows"]!.AsArray().Single()!["members"]!.AsArray();
        Assert.Equal([("g1", "stub", "Inclusive window from g1"), ("g2", "acp-stub", "Inclusive window from g2")],
            members.Select(member => (member!["pass"]!.GetValue<string>(), member["adapter"]!.GetValue<string>(), member["idea"]!.GetValue<string>())));
        Assert.Contains("stubs are no maker diversity", files.DocumentText(id)!, StringComparison.Ordinal);

        // BRAINSTORM1a2: the ACP stub's generator answered through `brainstorm_give` on its one connector, over MCP. The connector
        // carried the turn whole; the validator's refusal came back as the tool's answer, by code; the answer given whole again was
        // kept in the tool's own shape, and the driver read it from the file the tool wrote. Its pass took one turn, no repair.
        var tool = JsonNode.Parse(File.ReadAllText(Path.Combine(_home, "tool", "g2.json")))!;
        Assert.Equal(["daoris-knowledge"], tool["servers"]!.AsArray().Select(name => name!.GetValue<string>()));
        var carried = tool["env"]!.AsArray().Select(name => name!.GetValue<string>()).ToList();
        Assert.Superset(
            new HashSet<string> { BrainstormVariables.Id, BrainstormVariables.Turn, BrainstormVariables.Answer, BrainstormVariables.Room, DaorisHome.Variable },
            carried.ToHashSet());
        Assert.Contains("field:effort", tool["refused"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("kept: 1 proposal", tool["kept"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("{\n  \"brainstorm\": 1,", File.ReadAllText(files.AnswerPath(id, "g2")), StringComparison.Ordinal);
        var pass = JsonNode.Parse(File.ReadAllText(files.PathOf(id, "passes", "g2.json")))!;
        Assert.Equal(("answered", 1), (pass["state"]!.GetValue<string>(), pass["turns"]!.GetValue<int>()));
        Assert.Equal("brainstorm", pass["actor"]!.GetValue<string>());
        Assert.Equal("before", pass["choice"]!.GetValue<string>());
        Assert.Equal("acp-stub", pass["asked"]!["agent"]!.GetValue<string>());
        Assert.Equal("brainstorm-reported", pass["ran"]!["models"]![0]!.GetValue<string>());
        Assert.Contains("brainstorm-reported", files.DocumentText(id)!, StringComparison.Ordinal);

        // The rooms went, and the asker's checkout is as it was: no commit, no branch, no change.
        Assert.False(Directory.Exists(files.Rooms(id)));
        Assert.Equal(refs, _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\""));
        Assert.Equal("", _repository.Output("status --porcelain"));
    }

    /// <summary>
    /// A protocol agent that never answers its prompt: it says its process's id, then holds the turn open, as an agent past its
    /// minutes would.
    /// </summary>
    private string Hanging()
    {
        var script = Path.Combine(_home, "hanging-agent.mjs");
        File.WriteAllText(script, $$"""
            {{Imports}}
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'conv-' + process.env.DAORIS_BRAINSTORM_TURN } });
              } else if (frame.method === 'session/prompt') {
                writeFileSync(join({{System.Text.Json.JsonSerializer.Serialize(_home)}}, 'hanging.pid'), String(process.pid));
                setTimeout(() => process.exit(0), 120_000);
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            """);
        return script;
    }

    /// <summary>
    /// 🔴 BRAINSTORM1b (D176 point 6): a real agent past its pass's minutes is stopped, its process ended, and its pass said as
    /// timed out; the other maker's answer is still synthesized and ranked, and the document says who did not answer. The minute is
    /// shortened to a second, so the five-minute pass ends in five seconds while the agent would hold its turn for two minutes.
    /// </summary>
    [Fact]
    public async Task A_real_agent_past_its_minutes_is_stopped_and_what_answered_is_ranked()
    {
        OwnConnector();
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null),
            DriverConfig.Empty with
            {
                Adapter = "stub", Cap = 3, TestBound = 2,
                Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Native()], ["acp-stub"] = ["node", Hanging()] },
                Brainstorms = new BrainstormRule(BrainstormRules.Ask, [], new BrainstormMinutes(5, 10, 10, 30)),
            },
            adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            BrainstormMinute = TimeSpan.FromSeconds(1),
            Variable = Variable,
        };
        var files = new BrainstormFiles(_home);
        var id = files.WriteRequest("Why does the daily report drop its last day?", null, ["stub", "acp-stub"], DateTimeOffset.UtcNow);
        var snapshot = new Snapshot([], [], []);
        await driver.BrainstormsAsync(snapshot, 0, [], CancellationToken.None);
        files.WriteSent(id, files.ReadState(id)!.Packet!, DateTimeOffset.UtcNow);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var events = new List<string>();
        await driver.BrainstormsAsync(snapshot, 0, events, CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);
        clock.Stop();

        Assert.True(files.ReadState(id)!.State == BrainstormStates.Ended, string.Join('\n', events));
        var pass = JsonNode.Parse(File.ReadAllText(files.PathOf(id, "passes", "g2.json")))!;
        Assert.Equal(("failed", "timed-out"), (pass["state"]!.GetValue<string>(), string.Join(",", pass["codes"]!.AsArray().Select(code => code!.GetValue<string>()))));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"the brainstorm took {clock.Elapsed}");

        // The agent's process was ended with its pass, not left holding its turn.
        var pid = int.Parse(File.ReadAllText(Path.Combine(_home, "hanging.pid")), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(Gone(pid), $"the hanging agent's process {pid} still runs");

        var document = JsonNode.Parse(File.ReadAllText(files.PathOf(id, "document.json")))!;
        Assert.True(document["ranked"]!.GetValue<bool>());
        Assert.Contains("Did not answer: `acp-stub` (maker stub): timed-out", files.DocumentText(id)!, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Rooms(id)));
    }

    private static bool Gone(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.WaitForExit(TimeSpan.FromSeconds(10));
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
