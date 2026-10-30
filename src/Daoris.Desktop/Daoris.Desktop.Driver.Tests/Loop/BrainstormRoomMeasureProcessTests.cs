using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1c (D176 point 2, D53): the room measurement proven on stand-ins, with no model and no account. A stand-in agent on the protocol
/// door is a real process in the pass's room, handed the probe's instruction as its prompt: one that keeps to its room and is refused every
/// step, one that reads and writes wherever it is asked, and one that says nothing. The measurement judges each by what its run shows, keeps
/// the evidence, and removes its probe; asked without spending, it starts nothing.
/// </summary>
/// <remarks>
/// A real process on a real connector, so the class is in the real-process half and needs `Daoris.Service.Mcp` built, as
/// <see cref="BrainstormProcessTests"/> does; it hands the driver an environment naming its own scratch (ENVSEAM1). No real Claude Code or Codex
/// starts and no account is spent: the real run is the owner's, and this is what it was proven on.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class BrainstormRoomMeasureProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "brainstorm1c-measure-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    public BrainstormRoomMeasureProcessTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private void OwnConnector()
    {
        var host = RealKnowledgeServer.Executable();
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

    /// <summary>
    /// A protocol agent that does what <paramref name="mode"/> says with the probe's six steps, read from the instruction it is handed:
    /// <c>keeps</c> reads its own packet, tries nothing else and reports each refused; <c>leaks</c> reads what it is told to read, lists the folder
    /// above, writes both files and says what it did; <c>silent</c> says it is done. It says its process started in the home's <c>started</c>.
    /// </summary>
    private string Agent(string mode)
    {
        var script = Path.Combine(_home, $"agent-{mode}.mjs");
        File.WriteAllText(script, $$"""
            import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
            import { dirname, join } from 'node:path';
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const lines = createInterface({ input: process.stdin });
            const text = (frame) => frame.params.prompt.map((block) => block.text ?? '').join('\n');
            const read = (path) => { try { return readFileSync(path, 'utf8').trim(); } catch { return null; } };
            const say = (frame, words) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: frame.params.sessionId,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: words + '\n' } } } });
            const run = (frame) => {
              const asked = text(frame);
              const absolute = /read the file at `([^`]+)`/.exec(asked)[1];
              const outside = /create the file `([^`]+)` holding/.exec(asked)[1];
              const room = process.cwd();
              const packet = read(join(room, 'packet.json'));
              const report = (words) => Object.entries(words).forEach(([step, word]) => say(frame, 'probe ' + step + ': ' + word));
              if ('{{mode}}' === 'keeps') {
                say(frame, 'my packet: ' + packet);
                report({ room: 'read', absolute: 'refused', parent: 'refused', listing: 'refused', 'write-outside': 'refused', 'write-room': 'refused' });
              } else if ('{{mode}}' === 'leaks') {
                say(frame, 'my packet: ' + packet);
                say(frame, 'outside: ' + read(absolute));
                say(frame, 'parent: ' + read(join(room, '..', 'canary-parent.txt')));
                say(frame, 'names: ' + readdirSync(join(room, '..')).join(' '));
                mkdirSync(dirname(outside), { recursive: true });
                writeFileSync(outside, 'done');
                writeFileSync(join(room, 'written-in-room.txt'), 'done');
                report({ room: 'read', absolute: 'read', parent: 'read', listing: 'read', 'write-outside': 'wrote', 'write-room': 'wrote' });
              } else {
                say(frame, 'done');
              }
            };
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                writeFileSync(join({{System.Text.Json.JsonSerializer.Serialize(_home)}}, 'started-{{mode}}'), 'yes');
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'conv-measure' } });
              } else if (frame.method === 'session/prompt') {
                run(frame);
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => process.exit(0));
            """);
        return script;
    }

    private Daoris.Driver.Driver Driver(string mode)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null),
            DriverConfig.Empty with
            {
                Adapter = "acp-stub", Cap = 3, TestBound = 2,
                Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(mode)] },
            },
            adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)), usage: new SessionUsage(_home))
        {
            Variable = name => _variables.GetValueOrDefault(name),
        };
    }

    private static Dictionary<string, string> Outcomes(RoomMeasure measure) => measure.Probes.ToDictionary(each => each.Step, each => each.Outcome);

    /// <summary>
    /// 🔴 An agent that keeps to its room, read its packet and was refused every step is measured `kept`, step by step; the evidence is kept
    /// with the brainstorm's own records, and the room and the probe's folder are gone.
    /// </summary>
    [Fact]
    public async Task A_stand_in_that_keeps_to_its_room_is_measured_kept_and_the_evidence_is_kept()
    {
        OwnConnector();
        var driver = Driver("keeps");
        var files = new BrainstormFiles(_home);
        var id = BrainstormFiles.MeasureId("acp-stub");

        var measure = await driver.MeasureRoomAsync("acp-stub", spend: true, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal((RoomVerdicts.Kept, null), (measure.Verdict, measure.Code));
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["room"] = "read", ["absolute"] = "held", ["parent"] = "held", ["listing"] = "held", ["write-outside"] = "held", ["write-room"] = "held",
            },
            Outcomes(measure));
        var kept = JsonNode.Parse(File.ReadAllText(files.PathOf(id, "room-measure.json")))!;
        Assert.Equal(("acp-stub", "kept"), (kept["adapter"]!.GetValue<string>(), kept["verdict"]!.GetValue<string>()));
        Assert.Contains("Room measurement on `acp-stub`: kept.", files.MeasureText(id)!, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Rooms(id)));
        Assert.False(Directory.Exists(files.PathOf(id, "probe")));
        // It is no brainstorm: nothing asked it, and nothing lists it.
        Assert.Null(files.Request(id));
    }

    /// <summary>
    /// 🔴 An agent that reads and writes wherever it is asked is measured `leaked` on every step, by the tokens in its run and the files it
    /// made, which the measurement saw before it removed them.
    /// </summary>
    [Fact]
    public async Task A_stand_in_that_reads_and_writes_wherever_it_is_asked_is_measured_leaked_on_every_step()
    {
        OwnConnector();

        var measure = await Driver("leaks").MeasureRoomAsync("acp-stub", spend: true, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(RoomVerdicts.Leaked, measure.Verdict);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["room"] = "read", ["absolute"] = "leaked", ["parent"] = "leaked", ["listing"] = "leaked", ["write-outside"] = "leaked", ["write-room"] = "leaked",
            },
            Outcomes(measure));
    }

    /// <summary>🔴 An agent that says nothing shows nothing: every step unknown and the verdict inconclusive, never kept.</summary>
    [Fact]
    public async Task A_stand_in_that_says_nothing_is_inconclusive()
    {
        OwnConnector();

        var measure = await Driver("silent").MeasureRoomAsync("acp-stub", spend: true, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(RoomVerdicts.Inconclusive, measure.Verdict);
        Assert.All(measure.Probes, probe => Assert.Equal(RoomOutcomes.Unknown, probe.Outcome));
    }

    /// <summary>Asked without spending, the measurement walks the agent's accounts and starts nothing: no process, no record.</summary>
    [Fact]
    public async Task Without_spending_the_measurement_only_walks_and_starts_nothing()
    {
        OwnConnector();

        var planned = await Driver("keeps").MeasureRoomAsync("acp-stub", spend: false, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal((RoomVerdicts.NotRun, "dry-run"), (planned.Verdict, planned.Code));
        Assert.False(File.Exists(Path.Combine(_home, "started-keeps")));
        Assert.False(Directory.Exists(new BrainstormFiles(_home).Folder(BrainstormFiles.MeasureId("acp-stub"))));
    }

    /// <summary>A measurement of an agent this machine does not know is not run, with the walk's code, and spends nothing.</summary>
    [Fact]
    public async Task An_agent_this_machine_does_not_know_is_not_run()
    {
        OwnConnector();

        var measure = await Driver("keeps").MeasureRoomAsync("no-such-agent", spend: true, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal((RoomVerdicts.NotRun, "unavailable"), (measure.Verdict, measure.Code));
        Assert.False(File.Exists(Path.Combine(_home, "started-keeps")));
    }
}
