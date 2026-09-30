using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The hook wire (D64 §4, PLUG5): a plugin that speaks, driven against a fake over in-memory streams
/// — and once against a real process, because the environment a plugin is started with is the one
/// thing a fake cannot prove.
/// </summary>
public sealed class HookTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-hooks-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        // FLAKE1: a real hook's process runs in its plugin folder, and Windows refuses to delete a folder
        // a process still stands in; the process lets go a moment after it is told to stop. So the
        // cleanup waits a little for it rather than failing a test that passed.
        for (var attempt = 0; Directory.Exists(_home); attempt++)
        {
            try
            {
                Directory.Delete(_home, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }
    }

    /// <summary>A scripted plugin: a handler turns each frame the host sends into the frame it gets back.</summary>
    private sealed class FakePlugin
    {
        private readonly Channel<string> _toHost = Channel.CreateUnbounded<string>();
        private readonly Func<JsonElement, FakePlugin, string?> _handle;

        public FakePlugin(Func<JsonElement, FakePlugin, string?> handle) => _handle = handle;

        public List<string> Sent { get; } = [];

        public TextReader Incoming => new ChannelReader(_toHost);

        public TextWriter Outgoing => new HandlerWriter(line =>
        {
            Sent.Add(line);
            var frame = JsonDocument.Parse(line).RootElement;
            var reply = _handle(frame, this);
            if (reply is not null) Push(reply);
        });

        public void Push(string json) => _toHost.Writer.TryWrite(json);

        public void Close() => _toHost.Writer.TryComplete();

        public JsonElement Frame(int index) => JsonDocument.Parse(Sent[index]).RootElement;

        private sealed class ChannelReader(Channel<string> channel) : TextReader
        {
            public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();

            public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
                await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false)
                && channel.Reader.TryRead(out var line) ? line : null;
        }

        private sealed class HandlerWriter(Action<string> onLine) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override Task WriteLineAsync(string? value)
            {
                if (value is not null) onLine(value);
                return Task.CompletedTask;
            }
        }
    }

    private static string Ok(JsonElement request, string resultJson) =>
        $$"""{"jsonrpc":"2.0","id":{{request.GetProperty("id").GetRawText()}},"result":{{resultJson}}}""";

    private static string? Method(JsonElement frame) =>
        frame.TryGetProperty("method", out var m) ? m.GetString() : null;

    private static HookPeer Peer(FakePlugin plugin, TimeSpan? patience = null, Action<string>? onLine = null) =>
        new(plugin.Incoming, plugin.Outgoing, "acme.gate", onLine, patience);

    private static Consideration Start(string id, string title = "Expose a streaming budget") => new(
        new QuestView(id, "game", "engine", title, "World streaming needs a per-frame cap.", "Open"),
        StartVerdict.Start, "start", "D:/fam/engine", "default");

    // ——— the wire

    [Fact]
    public async Task The_handshake_names_the_host_the_plugin_and_its_points_and_takes_the_points_it_actually_listens_on()
    {
        var plugin = new FakePlugin((frame, _) => Method(frame) switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"points":["quest/consider"]}"""),
            _ => null,
        });
        var peer = Peer(plugin);

        await peer.InitializeAsync("C:/somewhere/data", "C:/somewhere/data/plugins/.data/acme.gate", ["quest/consider", "session/ended"], CancellationToken.None);

        var sent = plugin.Frame(0).GetProperty("params");
        Assert.Equal(1, sent.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("acme.gate", sent.GetProperty("plugin").GetString());
        Assert.Equal("C:/somewhere/data", sent.GetProperty("home").GetString());
        Assert.Equal("C:/somewhere/data/plugins/.data/acme.gate", sent.GetProperty("data").GetString());
        // The process is the fact; the manifest was the claim. It listens on one of the two it declared.
        Assert.Equal(["quest/consider"], peer.Points);
    }

    [Fact]
    public async Task A_plugin_listening_beyond_its_manifest_or_on_a_point_this_build_lacks_is_refused_naming_it()
    {
        var beyond = new FakePlugin((frame, _) => Ok(frame, """{"protocolVersion":1,"points":["session/ended"]}"""));
        var error = await Assert.ThrowsAsync<DriverException>(() =>
            Peer(beyond).InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None));
        Assert.Contains("session/ended", error.Message);
        Assert.Contains("manifest does not declare", error.Message);

        var unknown = new FakePlugin((frame, _) => Ok(frame, """{"protocolVersion":1,"points":["quest/teleport"]}"""));
        var refused = await Assert.ThrowsAsync<DriverException>(() =>
            Peer(unknown).InitializeAsync("h", "d", ["quest/teleport"], CancellationToken.None));
        Assert.Contains("quest/teleport", refused.Message);
        Assert.Contains("not a point this build has", refused.Message);
    }

    [Fact]
    public async Task A_plugin_speaking_another_wire_version_is_refused_naming_both()
    {
        var plugin = new FakePlugin((frame, _) => Ok(frame, """{"protocolVersion":2,"points":[]}"""));

        var error = await Assert.ThrowsAsync<DriverException>(() =>
            Peer(plugin).InitializeAsync("h", "d", [], CancellationToken.None));

        Assert.Contains("2", error.Message);
        Assert.Contains("speaks 1", error.Message);
    }

    [Fact]
    public async Task A_decision_is_allow_or_hold_with_its_reason_and_anything_else_is_not_a_decision()
    {
        var answers = new Queue<string>([
            """{"kind":"allow"}""",
            """{"kind":"hold","reason":"outside working hours"}""",
            """{"kind":"hold"}""",
            """{"verdict":"maybe"}""",
        ]);
        var plugin = new FakePlugin((frame, _) => Method(frame) switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"points":["quest/consider"]}"""),
            "hook/quest/consider" => Ok(frame, answers.Dequeue()),
            _ => null,
        });
        var peer = Peer(plugin);
        await peer.InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None);

        Assert.True((await peer.ConsiderAsync(new { }, CancellationToken.None)).Allowed);

        var held = await peer.ConsiderAsync(new { }, CancellationToken.None);
        Assert.False(held.Allowed);
        Assert.Equal("outside working hours", held.Reason);

        Assert.Equal("no reason given", (await peer.ConsiderAsync(new { }, CancellationToken.None)).Reason);

        var error = await Assert.ThrowsAsync<DriverException>(() => peer.ConsiderAsync(new { }, CancellationToken.None));
        Assert.Contains("not a decision", error.Message);
        Assert.Contains("acme.gate", error.Message);
    }

    /// <summary>
    /// WSR4 (D100): a landing's answer is whether the plugin pushed, the pull request it opened where it
    /// opened one, and its own sentence. Anything else is not an answer, in a sentence naming the plugin
    /// — which a landing says as the plugin's step failing, never as the branch undone.
    /// </summary>
    [Theory]
    [InlineData("""{"pushed":true,"pullRequest":"https://example.test/example-org/engine/pull/7","message":"opened it"}""", true, "https://example.test/example-org/engine/pull/7", "opened it")]
    [InlineData("""{"pushed":false,"message":"gh is not signed in"}""", false, null, "gh is not signed in")]
    [InlineData("""{"pushed":true}""", true, null, "no sentence given")]
    [InlineData("""{"pushed":true,"pullRequest":null,"message":"pushed; no pull request"}""", true, null, "pushed; no pull request")]
    public async Task A_landing_answer_is_pushed_its_pull_request_and_its_sentence(string result, bool pushed, string? pullRequest, string message)
    {
        var plugin = new FakePlugin((frame, _) => Method(frame) switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"points":["work/land"]}"""),
            "hook/work/land" => Ok(frame, result),
            _ => null,
        });
        var peer = Peer(plugin);
        await peer.InitializeAsync("h", "d", ["work/land"], CancellationToken.None);

        var answer = await peer.LandAsync(new { branch = "feature/x" }, CancellationToken.None);

        Assert.Equal(new PluginLanding("acme.gate", pushed, pullRequest, message), answer);
        Assert.Equal("feature/x", plugin.Frame(1).GetProperty("params").GetProperty("branch").GetString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"message":"no word on the push"}""")]
    [InlineData("""{"pushed":"yes"}""")]
    [InlineData("""{"pushed":true,"pullRequest":7}""")]
    [InlineData("""{"pushed":true,"pullRequest":"javascript:alert(1)"}""")]
    [InlineData("""{"pushed":true,"message":5}""")]
    public async Task A_landing_answer_of_the_wrong_shape_is_a_sentence_naming_the_plugin(string result)
    {
        var plugin = new FakePlugin((frame, _) => Method(frame) switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"points":["work/land"]}"""),
            "hook/work/land" => Ok(frame, result),
            _ => null,
        });
        var peer = Peer(plugin);
        await peer.InitializeAsync("h", "d", ["work/land"], CancellationToken.None);

        var error = await Assert.ThrowsAsync<DriverException>(() => peer.LandAsync(new { }, CancellationToken.None));

        Assert.Contains("acme.gate", error.Message);
        Assert.Contains("not a landing's answer", error.Message);
    }

    /// <summary>
    /// A plugin that speaks only at a landing is started by the landing, for that landing — the loop has
    /// nothing to ask it, so it never keeps one running beside the ticks.
    /// </summary>
    [Fact]
    public async Task A_plugin_that_speaks_only_at_a_landing_is_not_started_by_the_loop()
    {
        var started = new List<string>();
        await using var set = new HookSet(_home, start: (plugin, _, _) =>
        {
            started.Add(plugin.Manifest.Id);
            return Task.FromResult<IHookChannel>(new FakeChannel(plugin.Manifest.Hooks!.Points));
        });

        await set.ReconcileAsync(
            Catalog(_home, ("acme.lands", ["work/land"]), ("acme.both", ["work/land", "session/ended"])), CancellationToken.None);

        Assert.Equal(["acme.both"], started);
    }

    /// <summary>
    /// 🔴 REV3: an answer of the wrong JSON SHAPE threw InvalidOperationException from a property read —
    /// not a DriverException — so it escaped the tick's catch and killed every tick, naming no plugin.
    /// Every shape the wire can carry is a sentence naming the plugin.
    /// </summary>
    [Theory]
    [InlineData("initialize", "null")]
    [InlineData("initialize", "\"not a handshake\"")]
    [InlineData("initialize", """{"protocolVersion":"1"}""")]
    [InlineData("consider", "null")]
    [InlineData("consider", """{"kind":5}""")]
    [InlineData("consider", """{"kind":"hold","reason":7}""")]
    [InlineData("consider", "[]")]
    public async Task An_answer_of_the_wrong_shape_is_a_sentence_naming_the_plugin(string call, string result)
    {
        var plugin = new FakePlugin((frame, _) => Method(frame) switch
        {
            "initialize" => Ok(frame, call == "initialize" ? result : """{"protocolVersion":1,"points":["quest/consider"]}"""),
            "hook/quest/consider" => Ok(frame, result),
            _ => null,
        });
        var peer = Peer(plugin);

        if (call == "initialize")
        {
            var refused = await Assert.ThrowsAsync<DriverException>(() =>
                peer.InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None));
            Assert.Contains("acme.gate", refused.Message);
            return;
        }

        await peer.InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None);
        if (result.Contains("\"reason\":7", StringComparison.Ordinal))
        {
            // A hold is a hold; a reason that is not words is "no reason given", as an absent one is.
            Assert.Equal("no reason given", (await peer.ConsiderAsync(new { }, CancellationToken.None)).Reason);
            return;
        }

        var error = await Assert.ThrowsAsync<DriverException>(() => peer.ConsiderAsync(new { }, CancellationToken.None));
        Assert.Contains("acme.gate", error.Message);
    }

    /// <summary>A JSON-RPC error that is not the object the wire promises is still the plugin's refusal, in a sentence.</summary>
    [Fact]
    public async Task An_error_of_the_wrong_shape_is_still_the_plugin_s_refusal()
    {
        var plugin = new FakePlugin((frame, _) =>
            $$"""{"jsonrpc":"2.0","id":{{frame.GetProperty("id").GetRawText()}},"error":"no"}""");

        var refused = await Assert.ThrowsAsync<DriverException>(() =>
            Peer(plugin).InitializeAsync("h", "d", [], CancellationToken.None));

        Assert.Contains("acme.gate", refused.Message);
    }

    [Fact]
    public async Task A_plugin_that_does_not_answer_in_time_is_a_sentence_naming_the_plugin_and_the_call()
    {
        var plugin = new FakePlugin((frame, _) => Method(frame) switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"points":["quest/consider"]}"""),
            _ => null, // never answers a consider
        });
        var peer = Peer(plugin, patience: TimeSpan.FromMilliseconds(200));
        await peer.InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None);

        var error = await Assert.ThrowsAsync<DriverException>(() => peer.ConsiderAsync(new { }, CancellationToken.None));

        Assert.Contains("did not answer", error.Message);
        Assert.Contains("hook/quest/consider", error.Message);
    }

    [Fact]
    public async Task A_plugin_whose_stream_ends_is_dead_and_every_waiting_call_learns_so()
    {
        var plugin = new FakePlugin((frame, self) =>
        {
            if (Method(frame) == "initialize") return Ok(frame, """{"protocolVersion":1,"points":["quest/consider"]}""");
            self.Close();
            return null;
        });
        var peer = Peer(plugin);
        await peer.InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None);

        var error = await Assert.ThrowsAsync<DriverException>(() => peer.ConsiderAsync(new { }, CancellationToken.None));

        Assert.Contains("stopped answering", error.Message);
        Assert.False(peer.Alive);
    }

    [Fact]
    public async Task A_line_that_is_not_a_frame_is_shown_under_the_plugin_rather_than_dropped()
    {
        var lines = new List<string>();
        var plugin = new FakePlugin((frame, self) =>
        {
            self.Push("plugin diagnostics, printed to stdout by mistake");
            return Ok(frame, """{"protocolVersion":1,"points":[]}""");
        });

        await Peer(plugin, onLine: lines.Add).InitializeAsync("h", "d", [], CancellationToken.None);

        Assert.Contains("plugin diagnostics, printed to stdout by mistake", lines);
    }

    /// <summary>
    /// PLUG8: a JSON value that is not an object, and an object that is neither an answer nor a request,
    /// are noise like any other line. A bare `42` on stdout threw InvalidOperationException in the pump,
    /// which its catch did not name, so the wire ended and every call after it said the plugin had stopped.
    /// </summary>
    [Theory]
    [InlineData("42")]
    [InlineData("\"hello\"")]
    [InlineData("[1,2]")]
    [InlineData("""{"level":"info","msg":"a logging library's line"}""")]
    public async Task A_json_line_that_is_not_a_frame_is_noise_and_the_wire_goes_on(string noise)
    {
        var lines = new List<string>();
        var plugin = new FakePlugin((frame, self) =>
        {
            self.Push(noise);
            return Method(frame) switch
            {
                "initialize" => Ok(frame, """{"protocolVersion":1,"points":["quest/consider"]}"""),
                _ => Ok(frame, """{"kind":"allow"}"""),
            };
        });
        var peer = Peer(plugin, onLine: lines.Add);

        await peer.InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None);
        var decision = await peer.ConsiderAsync(new { }, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.True(peer.Alive);
        Assert.Equal([noise, noise], lines);
    }

    // ——— the set

    private sealed class FakeChannel(IReadOnlyList<string> points, Func<object, HookDecision>? consider = null, Action<object>? ended = null)
        : IHookChannel
    {
        public IReadOnlyList<string> Points => points;
        public bool Alive { get; set; } = true;
        public List<object> Asked { get; } = [];
        public bool Disposed { get; private set; }

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct)
        {
            Asked.Add(payload);
            return Task.FromResult(consider is null ? HookDecision.Allow : consider(payload));
        }

        public Task EndedAsync(object payload, CancellationToken ct)
        {
            Asked.Add(payload);
            ended?.Invoke(payload);
            return Task.CompletedTask;
        }

        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) =>
            throw new DriverException("the loop never asks a plugin to land work");

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            Alive = false;
            return ValueTask.CompletedTask;
        }
    }

    private static PluginEntry Plugin(string id, params string[] points) =>
        new(new PluginManifest(id, 1, id, "1.0.0", "", [], new PluginHooks(["node", "hooks.mjs"], points), []),
            $"C:/somewhere/data/plugins/{id}", $"C:/somewhere/data/plugins/.data/{id}", Enabled: true, Problem: null);

    private static PluginCatalog Catalog(string home, params (string Id, string[] Points)[] plugins)
    {
        Directory.CreateDirectory(Path.Combine(home, "plugins"));
        foreach (var (id, points) in plugins)
        {
            var folder = Path.Combine(home, "plugins", id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
            {
                id,
                hooks = new { command = new[] { "node", "hooks.mjs" }, points },
            }));
        }
        return PluginCatalog.Load(home);
    }

    [Fact]
    public async Task The_waterfall_asks_in_catalogue_order_and_the_first_hold_ends_it_with_the_plugin_named()
    {
        var a = new FakeChannel(["quest/consider"]);
        var b = new FakeChannel(["quest/consider"], _ => HookDecision.Hold("outside working hours"));
        var c = new FakeChannel(["quest/consider"]);
        var channels = new Dictionary<string, IHookChannel> { ["a.first"] = a, ["b.second"] = b, ["c.third"] = c };
        await using var set = new HookSet(_home, start: (plugin, _, _) => Task.FromResult(channels[plugin.Manifest.Id]));
        await set.ReconcileAsync(Catalog(_home, ("c.third", ["quest/consider"]), ("a.first", ["quest/consider"]), ("b.second", ["quest/consider"])), CancellationToken.None);

        var decision = await set.ConsiderAsync(Start("abc123"), CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("plugin `b.second` holds it: outside working hours", decision.Reason);
        Assert.Single(a.Asked);
        Assert.Single(b.Asked);
        Assert.Empty(c.Asked);
    }

    [Fact]
    public async Task A_plugin_that_cannot_decide_holds_the_quest_naming_itself_and_the_way_out()
    {
        var broken = new FakeChannel(["quest/consider"], _ => throw new DriverException("did not answer `hook/quest/consider` within 10s."));
        await using var set = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(broken));
        await set.ReconcileAsync(Catalog(_home, ("acme.gate", ["quest/consider"])), CancellationToken.None);

        var decision = await set.ConsiderAsync(Start("abc123"), CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Contains("plugin `acme.gate` could not decide", decision.Reason);
        Assert.Contains("daoris plugin disable acme.gate", decision.Reason);
    }

    /// <summary>
    /// One plugin's process is stopped NOW, not at the next tick — what removing it needs first, since
    /// on Windows a running hook holds its folder (REV3 modules F5).
    /// </summary>
    [Fact]
    public async Task One_plugin_can_be_stopped_now_and_the_others_keep_running()
    {
        var gate = new FakeChannel(["quest/consider"]);
        var watch = new FakeChannel(["session/ended"]);
        var channels = new Dictionary<string, IHookChannel> { ["acme.gate"] = gate, ["acme.watch"] = watch };
        await using var set = new HookSet(_home, start: (plugin, _, _) => Task.FromResult(channels[plugin.Manifest.Id]));
        await set.ReconcileAsync(
            Catalog(_home, ("acme.gate", ["quest/consider"]), ("acme.watch", ["session/ended"])), CancellationToken.None);

        Assert.True(await set.StopAsync("acme.gate"));

        Assert.True(gate.Disposed);
        Assert.False(watch.Disposed);
        Assert.Equal(["acme.watch"], set.Running);
        Assert.False(await set.StopAsync("acme.gate"));
    }

    [Fact]
    public async Task Nobody_listening_is_allow_and_a_plugin_listening_elsewhere_is_not_asked()
    {
        var observer = new FakeChannel(["session/ended"]);
        await using var set = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(observer));
        await set.ReconcileAsync(Catalog(_home, ("acme.watch", ["session/ended"])), CancellationToken.None);

        Assert.True((await set.ConsiderAsync(Start("abc123"), CancellationToken.None)).Allowed);
        Assert.Empty(observer.Asked);
    }

    [Fact]
    public async Task An_observation_is_fanned_out_and_contained()
    {
        var seen = new List<object>();
        var fine = new FakeChannel(["session/ended"], ended: seen.Add);
        var broken = new FakeChannel(["session/ended"], ended: _ => throw new DriverException("stopped answering"));
        var channels = new Dictionary<string, IHookChannel> { ["a.fine"] = fine, ["b.broken"] = broken };
        await using var set = new HookSet(_home, start: (plugin, _, _) => Task.FromResult(channels[plugin.Manifest.Id]));
        await set.ReconcileAsync(Catalog(_home, ("a.fine", ["session/ended"]), ("b.broken", ["session/ended"])), CancellationToken.None);

        var lines = await set.EndedAsync(
            new SessionEnded("s1", "engine", "completed", ByPerson: false, Quest: "abc123", Adapter: "acp-stub"),
            CancellationToken.None);

        Assert.Single(seen);
        var line = Assert.Single(lines);
        Assert.Contains("b.broken", line);
        Assert.Contains("stopped answering", line);
    }

    [Fact]
    public async Task Reconciling_starts_what_the_catalogue_has_stops_what_it_lost_and_retries_a_start_that_failed()
    {
        var starts = 0;
        var channel = new FakeChannel(["quest/consider"]);
        await using var set = new HookSet(_home, start: (_, _, _) =>
        {
            starts += 1;
            return starts == 1
                ? throw new DriverException("`node` is not on this machine's PATH")
                : Task.FromResult<IHookChannel>(channel);
        });

        var catalog = Catalog(_home, ("acme.gate", ["quest/consider"]));
        var first = await set.ReconcileAsync(catalog, CancellationToken.None);
        Assert.Contains(first, l => l.Contains("could not start") && l.Contains("PATH"));
        Assert.Empty(set.Running);

        var second = await set.ReconcileAsync(catalog, CancellationToken.None);
        Assert.Contains(second, l => l.Contains("acme.gate: started, listening on quest/consider"));
        Assert.Equal(["acme.gate"], set.Running);

        // The same catalogue again: nothing to say, nothing restarted.
        Assert.Empty(await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None));
        Assert.Equal(2, starts);

        // Disabled between ticks: stopped at the next one, with its registrations — the process IS them.
        PluginState.Disable(_home, "acme.gate");
        var third = await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Contains(third, l => l.Contains("acme.gate: stopped"));
        Assert.True(channel.Disposed);
        Assert.Empty(set.Running);
    }

    [Fact]
    public async Task A_plugin_that_only_declares_is_never_started()
    {
        var started = false;
        await using var set = new HookSet(_home, start: (_, _, _) =>
        {
            started = true;
            return Task.FromResult<IHookChannel>(new FakeChannel([]));
        });
        Directory.CreateDirectory(Path.Combine(_home, "plugins", "acme.agent"));
        File.WriteAllText(Path.Combine(_home, "plugins", "acme.agent", "plugin.json"),
            """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");

        Assert.Empty(await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None));
        Assert.False(started);
    }

    // ——— a real process

    /// <summary>
    /// The one thing a fake cannot prove: what a plugin's program is started with. A real script,
    /// started as the driver would, reads its id, its folder and its data folder from the environment
    /// and answers over the wire — and its diagnostics reach the console under its own name.
    /// </summary>
    [Fact]
    public async Task A_real_hook_process_is_started_in_its_folder_with_its_data_and_id_in_the_environment()
    {
        var folder = Path.Combine(_home, "plugins", "acme.gate");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.gate", "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider", "session/ended"] } }
            """);
        File.WriteAllText(Path.Combine(folder, "hooks.mjs"), """
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            console.error('gate: up in ' + process.cwd());
            const lines = createInterface({ input: process.stdin });
            for await (const line of lines) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points,
                  env: { id: process.env.DAORIS_PLUGIN_ID, folder: process.env.DAORIS_PLUGIN_FOLDER, data: process.env.DAORIS_PLUGIN_DATA, home: process.env.DAORIS_HOME } } });
              } else if (frame.method === 'hook/quest/consider') {
                const hold = frame.params.quest.title.includes('[hold]');
                send({ jsonrpc: '2.0', id: frame.id, result: hold ? { kind: 'hold', reason: 'the title says so' } : { kind: 'allow' } });
              } else if (frame.method === 'hook/session/ended') {
                console.error('gate: saw ' + frame.params.session + ' end ' + frame.params.state);
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'shutdown') {
                process.exit(0);
              }
            }
            """);
        var lines = new List<string>();
        var plugin = PluginCatalog.Load(_home).Plugins.Single();

        await using var process = await HookProcess.StartAsync(plugin, _home, line => { lock (lines) lines.Add(line); }, CancellationToken.None);

        Assert.True(process.Alive);
        Assert.Equal(["quest/consider", "session/ended"], process.Points);
        Assert.True((await process.ConsiderAsync(new { quest = new { title = "Expose a budget" } }, CancellationToken.None)).Allowed);
        var held = await process.ConsiderAsync(new { quest = new { title = "[hold] Rename everything" } }, CancellationToken.None);
        Assert.Equal("the title says so", held.Reason);
        await process.EndedAsync(new { session = "s1", state = "completed" }, CancellationToken.None);

        // stderr reaches the console; the cwd is the plugin's own folder.
        await Task.Delay(200);
        lock (lines)
        {
            Assert.Contains(lines, l => l.StartsWith("gate: up in ") && Path.GetFullPath(l["gate: up in ".Length..]) == Path.GetFullPath(folder));
            Assert.Contains("gate: saw s1 end completed", lines);
        }
        Assert.True(Directory.Exists(plugin.Data));
    }
}
