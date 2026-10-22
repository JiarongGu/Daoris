using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 points 2 to 4; the co-agent design §2.3, §8.3): a partner's whole piece on the real path, with no model. The person's
/// ask on file is taken at a look, judged, admitted by a stand-in's measured row, and started in a clone made by real git at the ask's
/// exact base. The partner is a real process (node, a stand-in shaped on codex-acp, named as <c>codex-acp</c>'s command) started through
/// the built <c>daoris-piece-start</c> launcher laid out beside the home, in its strict job. Its files are kept by the real keeper as one
/// commit frozen at <c>refs/daoris/pieces/&lt;id&gt;</c>, and its answer reaches the lead as a word under the piece's key, through a
/// stand-in for the local host's doors. A start with no launcher laid out is refused and runs nothing.
/// </summary>
/// <remarks>
/// Real git, a real job and real processes against a stand-in service on a loopback port, so the class is in the real-process half
/// (MOD8), and a strict job is Windows': elsewhere the cases return at once. Mirrors <see cref="HelperProcessTests"/>.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class PartnerProcessTests : IDisposable
{
    private const string Piece = "p7a1b2c3";

    private const string Version = "0.0.0-stand-in";

    private readonly GitTree _repository = new("partner-pass");
    private readonly string _install = Path.Combine(Path.GetTempPath(), "daoris-partner-pass-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;
    private readonly string _base;

    public PartnerProcessTests()
    {
        _home = Path.Combine(_install, "data");
        Directory.CreateDirectory(_home);
        Directory.CreateDirectory(Path.Combine(_repository.Root, "src"));
        _repository.Commit("src/window.ts");
        _base = _repository.Output("rev-parse HEAD");
    }

    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_install, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_install, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private PartnerFiles Files => new(_home);

    private void Ask() => Files.WriteAsk(new PartnerAsk(
        Piece, "s1", "Make the report's window seven days.", ["src"], "codex-acp", _base, [], PartnerAsk.ByPerson, DateTimeOffset.UtcNow));

    /// <summary>The built launcher laid out in an install beside the home, as the installer lays it out (<see cref="PieceStarts.LauncherPath"/>).</summary>
    private void LayOutLauncher()
    {
        File.WriteAllText(Path.Combine(_install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var bin = Path.Combine([_install, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.PieceStart", "bin", build.Parent.Name, build.Name);
        Assert.True(File.Exists(Path.Combine(built, PieceLauncher.Program)), "the launcher is built before these tests: " + built);
        foreach (var each in Directory.EnumerateFiles(built)) File.Copy(each, Path.Combine(bin, Path.GetFileName(each)));
        Assert.NotNull(PieceStarts.LauncherPath(_home));
    }

    /// <summary>
    /// The look's driver: the partner chosen as <c>codex-acp</c> on the stand-in's command, admitted by a stand-in's row of exactly the
    /// configuration its start hands, the lead's tree read as the repository's checkout.
    /// </summary>
    private Daoris.Driver.Driver Driver(PartnerStandIn service)
    {
        var config = DriverConfig.Empty with
        {
            Adapter = "claude-code-acp",
            Cap = 3,
            // `codex-acp` runs as `codex`'s accounts: both are the stand-in, so nothing here falls back to an agent on the PATH.
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["codex-acp"] = ["node", Agent()], ["codex"] = ["node", Agent()] },
        };
        var adapters = AdapterSet.Built();
        var choice = new ReviewerChoice("codex-acp", "another-maker", null, "codex-acp writes it.")
        {
            Selection = new HarnessSelection(null, Version: Version),
        };
        var plain = new Daoris.Driver.Driver(new ServiceClient(service.Url, null), config, adapters, _home);
        var row = PartnerStandIns.Row(plain, "codex-acp", Version, null, "reports");
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            PartnerChoices = (_, _, _) => Task.FromResult(choice),
            PartnerMeasured = [row],
            PartnerTrees = (_, _) => Task.FromResult<string?>(_repository.Root),
        };
    }

    private async Task SettleAsync(Daoris.Driver.Driver driver, Func<PartnerState?, bool> done)
    {
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);
        var until = DateTime.UtcNow.AddSeconds(60);
        while (!done(Files.ReadState(Piece)))
        {
            Assert.True(DateTime.UtcNow < until, $"the piece did not settle: {File.ReadAllText(Files.PathOf(Piece, "state"))}");
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// 🔴 The whole piece: asked, admitted, started through the launcher in its job with its state naming its record before its turn, its
    /// files kept as one commit on the ask's base frozen at its ref, the copy gone, and its answer handed to the lead under the piece's
    /// key, naming the line that takes it in. The repository's branches are untouched: only the piece's ref is new.
    /// </summary>
    [Fact]
    public async Task A_piece_goes_from_the_persons_ask_through_the_launcher_to_its_frozen_ref_and_the_leads_answer()
    {
        if (!OperatingSystem.IsWindows()) return;
        LayOutLauncher();
        var branches = _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\" refs/heads");
        Ask();
        await using var service = PartnerStandIn.Start(_repository.Root);
        var driver = Driver(service);

        await driver.PiecesAsync(new Snapshot([], [], []), 0, [], CancellationToken.None);
        await SettleAsync(driver, state => state is { State: PartnerStates.Handed });

        // The record the host opened: serves the lead, carries the piece, on codex-acp, in a clone under the home's trees.
        var opened = Assert.Single(service.Opened);
        Assert.Equal(("s1", Piece, "codex-acp"), (opened["lead"]!.GetValue<string>(), opened["piece"]!.GetValue<string>(), opened["adapter"]!.GetValue<string>()));
        Assert.Equal(_base, opened["baseCommit"]!.GetValue<string>());
        Assert.StartsWith(DaorisHome.Entry(_home, HomeEntry.Trees), opened["tree"]!.GetValue<string>());
        Assert.Equal(["starting", "working", "completed"], service.States("pt1"));

        // 🔴 What the partner saw at its turn: its state already named it and its job (partner_answer finds its piece by that), in its
        // copy at the ask's base, set to codex-acp's posture.
        var saw = JsonNode.Parse(File.ReadAllText(Path.Combine(_install, "saw.json")))!;
        Assert.Equal("pt1", saw["session"]!.GetValue<string>());
        Assert.Equal(_base, saw["head"]!.GetValue<string>());
        Assert.Equal("agent", saw["mode"]!.GetValue<string>());
        var seen = JsonNode.Parse(saw["state"]!.GetValue<string>())!;
        Assert.Equal((PartnerStates.Started, "pt1", PieceJobs.NameOf(Piece)),
            (seen["state"]!.GetValue<string>(), seen["partner"]!.GetValue<string>(), seen["job"]!.GetValue<string>()));

        // 🔴 The frozen ref: one commit on the ask's base, holding the partner's files and nothing of its own commit.
        var state = Files.ReadState(Piece)!;
        Assert.Equal(state.Commit, _repository.Output($"rev-parse {PieceRefs.Of(Piece)}"));
        Assert.Equal(_base, _repository.Output($"rev-parse {PieceRefs.Of(Piece)}^"));
        Assert.Equal(
            ["notes/beyond.md", "src/window.ts"],
            _repository.Output($"diff --name-only {_base} {PieceRefs.Of(Piece)}").Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
        Assert.Equal("export const windowDays = 7;", _repository.Output($"show {PieceRefs.Of(Piece)}:src/window.ts"));
        Assert.True(state.CopyRemoved);
        Assert.False(Directory.Exists(state.Copy));
        Assert.Equal(branches, _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\" refs/heads"));

        // The facts Daoris read, never the partner's words: the file beyond the piece's paths among them.
        var facts = Files.ReadFacts(Piece)!;
        Assert.Equal(["notes/beyond.md"], facts.Beyond);

        // 🔴 The lead's answer: one word under the piece's key on the lead, naming the partner, its summary as a claim, and the merge.
        var hand = Assert.Single(service.Hands);
        Assert.Equal("pt1", hand.Partner);
        Assert.Contains("The window is seven days now.", hand.Text);
        Assert.Contains($"git merge --no-ff {PieceRefs.Of(Piece)}", hand.Text);
        var word = Assert.Single(service.Words);
        Assert.Equal(($"partner:{Piece}", "pt1"), (word["key"]!.GetValue<string>(), word["helper"]!.GetValue<string>()));
        Assert.Equal(PartnerDelivery.Taken, Files.ReadReceipt(Piece)!.Outcome);
    }

    /// <summary>A start with no launcher laid out beside the home is refused `no-job`: no record is opened, no copy kept, nothing run.</summary>
    [Fact]
    public async Task A_piece_with_no_launcher_laid_out_is_refused_and_nothing_of_the_partners_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        Ask();
        await using var service = PartnerStandIn.Start(_repository.Root);
        var driver = Driver(service);

        await driver.PiecesAsync(new Snapshot([], [], []), 0, [], CancellationToken.None);
        await SettleAsync(driver, state => state is { State: PartnerStates.Refused });

        Assert.Equal(PartnerWhy.NoJob, Files.ReadState(Piece)!.Code);
        Assert.Empty(service.Opened);
        Assert.False(File.Exists(Path.Combine(_install, "saw.json")), "the partner ran with no launcher to join its job");
        Assert.False(Directory.Exists(PartnerPass.CopyFor(_home, null, "reports", Piece)));
        Assert.Equal("", _repository.Output($"for-each-ref {PieceRefs.Prefix}"));
    }

    /// <summary>
    /// The partner: a stand-in shaped on codex-acp, a real process with no model. It declares no prompt queueing and offers steering,
    /// opens in <c>read-only</c> among codex-acp's three modes, and says what it saw at its turn (its record, its copy's head, its mode,
    /// and the piece's state file). It writes the piece's file and one beyond its paths, makes a commit of its own (which the keeper never
    /// imports), answers through the file <c>partner_answer</c> writes, and ends its turn with no counts.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_install, "partner-agent.mjs");
        var partners = DaorisHome.Entry(_home, HomeEntry.Partners).Replace("\\", "/");
        var saw = Path.Combine(_install, "saw.json").Replace("\\", "/");
        File.WriteAllText(script, $$"""
            import { execFileSync } from 'node:child_process';
            import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const partners = '{{partners}}';
            let mode = 'read-only';
            const conversation = 'codex-like-1';
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: false }, _meta: { steering: { supported: true } } } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation, modes: { currentModeId: mode, availableModes: [
                  { id: 'read-only', name: 'Read Only' }, { id: 'agent', name: 'Agent' }, { id: 'agent-full-access', name: 'Agent (full access)' }] } } });
              } else if (frame.method === 'session/set_mode') {
                mode = frame.params.modeId;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                let state = null;
                try { state = readFileSync(join(partners, '{{Piece}}.state.json'), 'utf8'); } catch {}
                const head = execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
                writeFileSync('{{saw}}', JSON.stringify({ session: process.env.DAORIS_SESSION_ID ?? null, cwd: process.cwd(), head, mode, state }));
                mkdirSync('notes', { recursive: true });
                writeFileSync('src/window.ts', 'export const windowDays = 7;\n');
                writeFileSync('notes/beyond.md', '# a note beyond the piece\n');
                execFileSync('git', ['-c', 'user.name=Partner', '-c', 'user.email=partner@example.invalid', 'commit', '-qam', 'the partner commits'], { stdio: 'ignore' });
                writeFileSync(join(partners, '{{Piece}}.answer.json'), JSON.stringify({
                  id: '{{Piece}}', partner: process.env.DAORIS_SESSION_ID, summary: 'The window is seven days now.', left: null, at: new Date().toISOString(),
                }));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'Changed the window.' } } } });
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
    /// The service's doors a piece crosses, on a real loopback listener: the lead's record (an ended driven session in its tree), the
    /// partner's record once opened, the host's partner door (<c>POST /api/partners</c>), the ledger's state door and the hand door that
    /// keeps the answer on the lead under the piece's key.
    /// </summary>
    private sealed class PartnerStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly Dictionary<string, List<string>> _states = [];
        private JsonObject? _partner;

        public string Url { get; }

        public List<JsonObject> Opened { get; } = [];

        public List<(string Partner, string Text)> Hands { get; } = [];

        public List<JsonObject> Words { get; } = [];

        private PartnerStandIn(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _serving = ServeAsync();
        }

        public static PartnerStandIn Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new PartnerStandIn(listener, url, root);
        }

        public IReadOnlyList<string> States(string session)
        {
            lock (_states) return _states.TryGetValue(session, out var moved) ? [.. moved] : [];
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = Answer(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        private (int, string) Answer(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

            lock (_states)
            {
                switch (request.HttpMethod, path)
                {
                    case ("GET", "/api/sessions"):
                    {
                        var lead = new JsonObject
                        {
                            ["id"] = "s1", ["quest"] = "q1", ["repository"] = "reports", ["state"] = "completed", ["kind"] = "driven",
                            ["adapter"] = "claude-code-acp", ["tree"] = _root, ["said"] = new JsonArray([.. Words.Select(word => word.DeepClone())]),
                        };
                        var records = new JsonArray(lead);
                        if (_partner is not null)
                        {
                            var partner = (JsonObject)_partner.DeepClone();
                            partner["state"] = _states["pt1"].LastOrDefault() ?? "queued";
                            records.Add(partner);
                        }

                        return (200, records.ToJsonString());
                    }

                    case ("POST", "/api/partners"):
                    {
                        var body = Body();
                        Opened.Add(body);
                        _states["pt1"] = [];
                        _partner = new JsonObject
                        {
                            ["id"] = "pt1", ["repository"] = "reports", ["state"] = "queued", ["kind"] = "chat", ["adapter"] = body["adapter"]!.GetValue<string>(),
                            ["serves"] = body["lead"]!.GetValue<string>(), ["piece"] = body["piece"]!.GetValue<string>(), ["tree"] = body["tree"]!.GetValue<string>(),
                        };
                        return (200, new JsonObject { ["message"] = "Session `pt1` writes the piece in a clone of its own.", ["session"] = _partner.DeepClone() }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/state".Length];
                        var state = Body()["state"]!.GetValue<string>();
                        if (!_states.TryGetValue(id, out var moved)) return (404, """{"error":"no such session"}""");
                        moved.Add(state);
                        return (200, new JsonObject { ["session"] = new JsonObject { ["id"] = id, ["state"] = state }, ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", "/api/partners/pt1/hand"):
                    {
                        var text = Body()["text"]!.GetValue<string>();
                        Hands.Add(("pt1", text));
                        var word = new JsonObject
                        {
                            ["id"] = "pw1", ["text"] = text, ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true,
                            ["helper"] = "pt1", ["key"] = $"partner:{Piece}",
                        };
                        Words.Add(word);
                        return (200, new JsonObject { ["message"] = "Handed.", ["session"] = new JsonObject { ["id"] = "s1" }, ["said"] = word.DeepClone() }.ToJsonString());
                    }

                    default:
                        return (404, $$"""{"error":"the stand-in has no {{request.HttpMethod}} {{path}}"}""");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
