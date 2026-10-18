using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a (D176 points 1-4 and 8): a brainstorm's whole round trip on the real path, with no model. A request about a
/// repository whose checkout real git reads is frozen at a look (no service: the declared index's files answer), sent, and run on
/// two real processes, the native stub and the ACP stub, each answering through the file its result tool writes; the native stub
/// synthesizes; the document is kept with each member's provenance, the rooms go, and the asker's checkout is untouched.
/// </summary>
/// <remarks>
/// Real git and real processes, so the class is in the real-process half (MOD8): a branch's worktree does not run it, the parent
/// runs it on the merge. The service is a port nobody holds: a brainstorm asks it only for a search, and does without.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class BrainstormProcessTests : IDisposable
{
    private readonly GitTree _repository = new("brainstorm-pass");
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-pass-" + Guid.NewGuid().ToString("N")[..8]);

    public BrainstormProcessTests()
    {
        Directory.CreateDirectory(_home);
        _repository.Commit("report.md");
    }

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
    /// folder, then answer through the file the result tool writes: a generator two proposals, the synthesis one group of all.
    /// </summary>
    private string Work()
    {
        var home = _home.Replace("\\", "/");
        return $$"""
            const work = () => {
              const turn = process.env.DAORIS_BRAINSTORM_TURN;
              const packet = readFileSync(join(process.cwd(), 'packet.json'));
              mkdirSync('{{home}}/saw', { recursive: true });
              writeFileSync('{{home}}/saw/' + turn + '.json', JSON.stringify({
                cwd: process.cwd(), packet: createHash('sha256').update(packet).digest('hex'),
                repository: process.env.DAORIS_REPOSITORY ?? null, quest: process.env.DAORIS_QUEST_ID ?? null,
                files: readdirSync(process.cwd()).sort(),
              }));
              const answer = process.env.DAORIS_BRAINSTORM_ANSWER;
              mkdirSync(dirname(answer), { recursive: true });
              if (turn.startsWith('s')) {
                const handed = JSON.parse(readFileSync(join(process.cwd(), 'proposals.json'), 'utf8')).proposals.map((p) => p.id);
                writeFileSync(answer, JSON.stringify({ brainstorm: 1, groups: [{ title: 'Every idea', idea: 'All of them, merged.',
                  members: handed, rank: 1, why: 'Nothing told them apart.', flags: [] }], leftOut: [] }));
              } else {
                writeFileSync(answer, JSON.stringify({ brainstorm: 1, needs: [], none: null, proposals: [
                  { idea: 'Inclusive window from ' + turn, rationale: 'The report reads an exclusive window.', effort: 'small', risk: 'low',
                    reversible: 'yes', undo: null, confidence: 'medium', assumptions: [], sources: [] },
                ] }));
              }
            };
            """;
    }

    private const string Imports = """
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
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'conv-' + process.env.DAORIS_BRAINSTORM_TURN } });
              } else if (frame.method === 'session/prompt') {
                work();
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: frame.params.sessionId,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'answered through its tool' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
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
        var refs = _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\"");
        var head = _repository.Output("rev-parse HEAD");
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null),
            DriverConfig.Empty with
            {
                Adapter = "stub", Cap = 3, TimeoutMinutes = 2,
                Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Native()], ["acp-stub"] = ["node", Protocol()] },
            },
            adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
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

        // The rooms went, and the asker's checkout is as it was: no commit, no branch, no change.
        Assert.False(Directory.Exists(files.Rooms(id)));
        Assert.Equal(refs, _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\""));
        Assert.Equal("", _repository.Output("status --porcelain"));
    }
}
