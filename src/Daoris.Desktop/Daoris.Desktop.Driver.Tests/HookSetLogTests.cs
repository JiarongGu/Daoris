using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What Daoris did with a plugin, in the machine log (PLUGUI1d, D119 §4.2): the hook set's starts, calls,
/// failures and stops, a landing's and a hand-off's one frame, the servers a session was handed and a
/// trial, each a line of names, counts, flags and times, and never the plugin's words.
/// </summary>
/// <remarks>
/// Driven through fakes on the wire's channel (and once through the wire itself over in-memory streams),
/// so no process starts and the class stays in the fast half. The clock is the test's.
/// </remarks>
public sealed class HookSetLogTests : IDisposable
{
    /// <summary>Words the tests hand a plugin to say, which no line may carry.</summary>
    private const string Words = "SECRET-WORDS";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-hooklog-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 10, 1, 9, 12, 3, TimeSpan.Zero);

    private readonly MachineLog _log;

    public HookSetLogTests()
    {
        Directory.CreateDirectory(_home);
        _log = new MachineLog(_home, "desktop", () => _now);
    }

    public void Dispose()
    {
        _log.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ——— the hook set

    [Fact]
    public async Task A_start_each_call_and_a_stop_are_lines_of_names_counts_and_times()
    {
        var channel = new FakeChannel(["quest/consider", "session/ended"]);
        await using var set = Set(_ => Task.FromResult<IHookChannel>(channel));
        await set.ReconcileAsync(Catalog(("acme.gate", ["quest/consider", "session/ended"], "1.0.0")), CancellationToken.None);

        await set.ConsiderAsync(Start("q1"), CancellationToken.None);
        await set.EndedAsync(Ended(), CancellationToken.None);
        PluginState.Disable(_home, "acme.gate");
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        var lines = Lines();
        Assert.Equal(["plugin.started", "plugin.called", "plugin.called", "plugin.stopped"], lines.Select(l => l.Event));
        Assert.All(lines, line => Assert.Equal("acme.gate", Text(line, "plugin")));

        var started = lines[0];
        Assert.Equal("info", started.Level);
        Assert.Equal("quest/consider,session/ended", Text(started, "points"));
        Assert.Equal("loop", Text(started, "by"));
        Assert.Equal(JsonValueKind.Number, started.Data.GetProperty("ms").ValueKind);
        Assert.Equal(["plugin", "points", "ms", "by"], Keys(started));

        Assert.Equal(("quest/consider", "allow"), (Text(lines[1], "point"), Text(lines[1], "answer")));
        Assert.Equal(("session/ended", "answered"), (Text(lines[2], "point"), Text(lines[2], "answer")));
        Assert.Equal(["plugin", "point", "answer", "ms"], Keys(lines[1]));

        Assert.Equal("off", Text(lines[3], "why"));
        Assert.Equal(["plugin", "why", "by"], Keys(lines[3]));
    }

    /// <summary>🔴 D94 §5: a hold's reason and what the plugin wrote to stderr stay in the console's ring, never the log.</summary>
    [Fact]
    public async Task A_holds_reason_and_a_plugins_stderr_are_in_no_line()
    {
        var output = new SessionOutput();
        var channel = new FakeChannel(["quest/consider"], _ => HookDecision.Hold($"outside working hours — {Words}"));
        await using var set = new HookSet(_home, output, start: (_, say, _) =>
        {
            say($"{Words} written to stderr");
            return Task.FromResult<IHookChannel>(channel);
        }, log: _log);
        await set.ReconcileAsync(Catalog(("acme.gate", ["quest/consider"], "1.0.0")), CancellationToken.None);

        var decision = await set.ConsiderAsync(Start("q1"), CancellationToken.None);
        channel.Throw = Mark(new DriverException($"plugin `acme.gate` refused the call: {Words}"), PluginEvents.Errored);
        await set.ConsiderAsync(Start("q2"), CancellationToken.None);

        Assert.False(decision.Allowed);
        var answer = Assert.Single(Lines(), l => l.Event == "plugin.called");
        Assert.Equal("hold", Text(answer, "answer"));
        Assert.Contains(Lines(), l => l.Event == "plugin.failed");
        Assert.DoesNotContain(Words, Raw());
        Assert.DoesNotContain("outside working hours", Raw());
        // The words are where they always were: the console's ring, under the plugin's name, for this run.
        Assert.Contains(output.Tail("plugin:acme.gate", 0).Lines, line => line.Text.Contains(Words));
    }

    [Fact]
    public async Task A_failed_call_is_a_warn_line_naming_the_point_its_kind_and_how_long_it_took()
    {
        var channel = new FakeChannel(["quest/consider", "session/ended"])
        {
            Throw = Mark(new DriverException("plugin `acme.gate` did not answer `hook/quest/consider` within 10s."), PluginEvents.Late),
        };
        await using var set = Set(_ => Task.FromResult<IHookChannel>(channel));
        await set.ReconcileAsync(Catalog(("acme.gate", ["quest/consider", "session/ended"], "1.0.0")), CancellationToken.None);

        await set.ConsiderAsync(Start("q1"), CancellationToken.None);
        channel.Throw = Mark(new DriverException("plugin `acme.gate` stopped answering"), PluginEvents.Exited);
        channel.Code = 3;
        await set.EndedAsync(Ended(), CancellationToken.None);

        var failed = Lines().Where(l => l.Event == "plugin.failed").ToList();
        Assert.Equal(2, failed.Count);
        Assert.All(failed, line => Assert.Equal("warn", line.Level));
        Assert.Equal(["plugin", "where", "kind", "code", "ms", "by"], Keys(failed[0]));
        Assert.Equal(("quest/consider", "late", "loop"), (Text(failed[0], "where"), Text(failed[0], "kind"), Text(failed[0], "by")));
        Assert.Equal(JsonValueKind.Null, failed[0].Data.GetProperty("code").ValueKind);
        Assert.Equal(JsonValueKind.Number, failed[0].Data.GetProperty("ms").ValueKind);
        Assert.Equal(("session/ended", "exited"), (Text(failed[1], "where"), Text(failed[1], "kind")));
        Assert.Equal(3, failed[1].Data.GetProperty("code").GetInt32());
    }

    /// <summary>
    /// A start that fails is a line at <c>start</c>, of the kind the wire marked or <c>unstartable</c>. The same
    /// failure again is not a second line, since the loop retries at every look; a different one is.
    /// </summary>
    [Fact]
    public async Task A_start_that_fails_is_one_line_until_it_fails_differently_or_starts()
    {
        var attempts = 0;
        var channel = new FakeChannel(["quest/consider"]);
        await using var set = Set(_ =>
        {
            attempts++;
            return attempts switch
            {
                <= 2 => throw new DriverException("plugin `acme.gate`'s hook process could not start — `node`: not found"),
                3 => throw Mark(new DriverException("plugin `acme.gate` speaks hook wire 2; this build speaks 1."), PluginEvents.Unreadable),
                _ => Task.FromResult<IHookChannel>(channel),
            };
        });
        var catalog = Catalog(("acme.gate", ["quest/consider"], "1.0.0"));

        for (var look = 0; look < 4; look++) await set.ReconcileAsync(catalog, CancellationToken.None);

        Assert.Equal(4, attempts);
        var lines = Lines();
        Assert.Equal(["plugin.failed", "plugin.failed", "plugin.started"], lines.Select(l => l.Event));
        Assert.Equal(("start", "unstartable"), (Text(lines[0], "where"), Text(lines[0], "kind")));
        Assert.Equal(("start", "unreadable"), (Text(lines[1], "where"), Text(lines[1], "kind")));
    }

    [Fact]
    public async Task A_process_found_gone_at_a_look_is_an_exit_and_it_is_started_again()
    {
        var first = new FakeChannel(["quest/consider"]);
        var second = new FakeChannel(["quest/consider"]);
        var channels = new Queue<FakeChannel>([first, second]);
        await using var set = Set(_ => Task.FromResult<IHookChannel>(channels.Dequeue()));
        var catalog = Catalog(("acme.gate", ["quest/consider"], "1.0.0"));
        await set.ReconcileAsync(catalog, CancellationToken.None);

        first.Alive = false;
        first.Code = 1;
        await set.ReconcileAsync(catalog, CancellationToken.None);

        var lines = Lines();
        Assert.Equal(["plugin.started", "plugin.failed", "plugin.started"], lines.Select(l => l.Event));
        Assert.Equal(("process", "exited", "loop"), (Text(lines[1], "where"), Text(lines[1], "kind"), Text(lines[1], "by")));
        Assert.Equal(1, lines[1].Data.GetProperty("code").GetInt32());
    }

    /// <summary>Why a process stopped: switched off, removed, updated to another version, its manifest changed, or the loop ended.</summary>
    [Fact]
    public async Task A_stop_says_why()
    {
        await using (var set = Set(plugin => Task.FromResult<IHookChannel>(new FakeChannel(plugin.Manifest.Hooks!.Points))))
        {
            await set.ReconcileAsync(Catalog(
                ("a.off", ["quest/consider"], "1.0.0"), ("b.removed", ["quest/consider"], "1.0.0"),
                ("c.updated", ["quest/consider"], "1.0.0"), ("d.changed", ["quest/consider"], "1.0.0"),
                ("e.ended", ["session/ended"], "1.0.0")), CancellationToken.None);

            PluginState.Disable(_home, "a.off");
            Directory.Delete(Path.Combine(_home, "plugins", "b.removed"), recursive: true);
            Catalog(("c.updated", ["quest/consider"], "1.1.0"), ("d.changed", ["quest/consider", "session/ended"], "1.0.0"));
            await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        }

        var whys = Lines().Where(l => l.Event == "plugin.stopped")
            .GroupBy(l => Text(l, "plugin")!)
            .ToDictionary(g => g.Key, g => g.Select(l => Text(l, "why")).ToList());
        Assert.Equal(["off"], whys["a.off"]);
        Assert.Equal(["removed"], whys["b.removed"]);
        // Each of these was stopped for what changed, started again, and ended with the loop.
        Assert.Equal(["updated", "ended"], whys["c.updated"]);
        Assert.Equal(["changed", "ended"], whys["d.changed"]);
        Assert.Equal(["ended"], whys["e.ended"]);
    }

    /// <summary>
    /// A stop a removal or an update asks for (<see cref="HookSet.StopAsync"/>) cannot say which it is when it
    /// happens, so its line is written at the loop's next look, which the route asks for at once.
    /// </summary>
    [Fact]
    public async Task A_stop_asked_for_before_a_removal_or_an_update_says_which_at_the_next_look()
    {
        await using var set = Set(plugin => Task.FromResult<IHookChannel>(new FakeChannel(plugin.Manifest.Hooks!.Points)));
        await set.ReconcileAsync(Catalog(("a.gone", ["quest/consider"], "1.0.0"), ("b.newer", ["quest/consider"], "1.0.0")), CancellationToken.None);

        Assert.True(await set.StopAsync("a.gone"));
        Assert.True(await set.StopAsync("b.newer"));
        Assert.DoesNotContain(Lines(), l => l.Event == "plugin.stopped");

        Directory.Delete(Path.Combine(_home, "plugins", "a.gone"), recursive: true);
        Catalog(("b.newer", ["quest/consider"], "2.0.0"));
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        var whys = Lines().Where(l => l.Event == "plugin.stopped").ToDictionary(l => Text(l, "plugin")!, l => Text(l, "why"));
        Assert.Equal("removed", whys["a.gone"]);
        Assert.Equal("updated", whys["b.newer"]);
    }

    // ——— the landing and the hand-off

    [Theory]
    [InlineData(true, "landing", "pushed")]
    [InlineData(false, "hand", "not-pushed")]
    public async Task A_landings_one_frame_is_started_called_and_stopped_by_its_door(bool pushed, string by, string answer)
    {
        Catalog(("acme.lands", ["work/land"], "1.0.0"));
        var plugins = new LandingPlugins(_home,
            start: (_, say, _) =>
            {
                say($"{Words} on stderr");
                return Task.FromResult<IHookChannel>(new FakeChannel(["work/land"],
                    land: _ => new PluginLanding("acme.lands", pushed, "https://example.test/pull/7?token=" + Words, $"{Words} said")));
            },
            log: _log);

        var said = by == "landing"
            ? await plugins.LandAsync("acme.lands", Landing())
            : await plugins.HandAsync("acme.lands", Landing());

        Assert.Equal(pushed, said.Pushed);
        var lines = Lines();
        Assert.Equal(["plugin.started", "plugin.called", "plugin.stopped"], lines.Select(l => l.Event));
        Assert.Equal(by, Text(lines[0], "by"));
        Assert.Equal(("work/land", answer), (Text(lines[1], "point"), Text(lines[1], "answer")));
        Assert.Equal(("ended", by), (Text(lines[2], "why"), Text(lines[2], "by")));
        // Never the pull request's address, the plugin's sentence or its stderr.
        Assert.DoesNotContain(Words, Raw());
        Assert.DoesNotContain("example.test", Raw());
    }

    [Fact]
    public async Task A_landing_plugin_that_fails_is_a_line_and_one_that_was_never_spoken_to_is_none()
    {
        var plugins = new LandingPlugins(_home,
            start: (plugin, _, _) => plugin.Manifest.Id switch
            {
                "a.late" => Task.FromResult<IHookChannel>(new FakeChannel(["work/land"])
                {
                    Throw = Mark(new DriverException($"plugin `a.late` did not answer `hook/work/land` within 120s. {Words}"), PluginEvents.Late),
                }),
                "b.deaf" => Task.FromResult<IHookChannel>(new FakeChannel([])),
                _ => throw new DriverException($"plugin `{plugin.Manifest.Id}`'s hook process could not start — {Words}"),
            },
            log: _log);
        Catalog(("a.late", ["work/land"], "1.0.0"), ("b.deaf", ["work/land"], "1.0.0"), ("c.broken", ["work/land"], "1.0.0"));

        Assert.True((await plugins.LandAsync("a.late", Landing())).Failed);
        Assert.True((await plugins.LandAsync("b.deaf", Landing())).Failed);
        Assert.True((await plugins.LandAsync("c.broken", Landing())).Failed);
        // Not installed: refused before anything is spoken to, so nothing happened to a plugin.
        Assert.True((await plugins.LandAsync("d.absent", Landing())).Failed);

        var failed = Lines().Where(l => l.Event == "plugin.failed").ToDictionary(l => Text(l, "plugin")!);
        Assert.Equal(("work/land", "late", "landing"), (Text(failed["a.late"], "where"), Text(failed["a.late"], "kind"), Text(failed["a.late"], "by")));
        Assert.Equal(("work/land", "unreadable"), (Text(failed["b.deaf"], "where"), Text(failed["b.deaf"], "kind")));
        Assert.Equal(("start", "unstartable"), (Text(failed["c.broken"], "where"), Text(failed["c.broken"], "kind")));
        Assert.DoesNotContain(Lines(), l => Text(l, "plugin") == "d.absent");
        Assert.DoesNotContain(Words, Raw());
    }

    // ——— served, tried, tested

    [Fact]
    public void Each_server_a_session_is_handed_or_withheld_is_a_line_naming_its_plugin()
    {
        Directory.CreateDirectory(Path.Combine(_home, "plugins", "acme.tools"));
        File.WriteAllText(Path.Combine(_home, "plugins", "acme.tools", "plugin.json"), $$"""
            { "id": "acme.tools", "servers": [
                { "name": "tickets", "command": ["node", "tickets.mjs"], "env": { "TICKETS_TOKEN": "{{Words}}" } },
                { "name": "browser", "command": ["node", "drive.mjs", "${browser}"] } ] }
            """);
        var catalog = PluginCatalog.Load(_home);
        var (handed, _) = InAppBrowserServers.Resolve(catalog.Servers, endpoint: null);

        new PluginLog(_log).Served(catalog, "s1a2b3c4", handed);

        var lines = Lines();
        Assert.All(lines, line => Assert.Equal(["plugin", "server", "session", "handed"], Keys(line)));
        Assert.Equal(
            [("tickets", true), ("browser", false)],
            lines.Select(l => (Text(l, "server"), l.Data.GetProperty("handed").GetBoolean())));
        Assert.All(lines, line => Assert.Equal(("acme.tools", "s1a2b3c4"), (Text(line, "plugin"), Text(line, "session"))));
        Assert.DoesNotContain(Words, Raw());
        Assert.DoesNotContain("tickets.mjs", Raw());
    }

    /// <summary>
    /// A conversation is handed the plugins' servers as a driven session is, and says so in the log (PLUGUI1e, D119
    /// §4.2): one line per server, handed or withheld, the browser's withheld where no shell answers (D78).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_conversation_is_handed_its_servers_with_a_line_for_each_handed_or_withheld(bool shell)
    {
        Directory.CreateDirectory(Path.Combine(_home, "plugins", "acme.tools"));
        File.WriteAllText(Path.Combine(_home, "plugins", "acme.tools", "plugin.json"), $$"""
            { "id": "acme.tools", "servers": [
                { "name": "tickets", "command": ["node", "tickets.mjs"], "env": { "TICKETS_TOKEN": "{{Words}}" } },
                { "name": "browser", "command": ["node", "drive.mjs", "${browser}"] } ] }
            """);

        var (servers, notice, drives) = await ChatRunner.HandServersAsync(
            _home, AdapterSet.Built().Names, shell ? new AnsweringBrowser("http://127.0.0.1:4810") : null,
            new PluginLog(_log), "s1a2b3c4", CancellationToken.None);

        Assert.Equal(shell ? ["tickets", "browser"] : ["tickets"], servers.Select(server => server.Name));
        Assert.Equal(shell, drives);
        Assert.Equal(shell, notice is null);
        var lines = Lines();
        Assert.Equal([("tickets", true), ("browser", shell)], lines.Select(l => (Text(l, "server"), l.Data.GetProperty("handed").GetBoolean())));
        Assert.All(lines, line => Assert.Equal(("plugin.served", "acme.tools", "s1a2b3c4"), (line.Event, Text(line, "plugin"), Text(line, "session"))));
        Assert.DoesNotContain(Words, Raw());
    }

    private sealed class AnsweringBrowser(string endpoint) : IInAppBrowser
    {
        public Task<string?> EnsureAsync(CancellationToken ct = default) => Task.FromResult<string?>(endpoint);

        public void Show() { }

        public void Open(string address) { }
    }

    [Fact]
    public void A_trial_and_a_test_run_are_one_line_each_from_their_door_warn_when_they_failed()
    {
        var log = new PluginLog(_log);
        log.Tried(new PluginTrial("acme.gate", "C:/x", ["node", "plugin.mjs"],
            [new TrialStep("handshake", true, "answered"), new TrialStep("quest/consider", false, Words)], [Words]), 1234, PluginEvents.Terminal);
        log.Tested("acme.gate", passed: true, code: 0, ms: 5000, PluginEvents.Screen);

        var lines = Lines();
        Assert.Equal(["plugin.tried", "plugin.tested"], lines.Select(l => l.Event));
        Assert.Equal("warn", lines[0].Level);
        Assert.Equal(["plugin", "passed", "checks", "failed", "ms", "door"], Keys(lines[0]));
        Assert.Equal((false, 2, 1, "terminal"),
            (lines[0].Data.GetProperty("passed").GetBoolean(), lines[0].Data.GetProperty("checks").GetInt32(),
             lines[0].Data.GetProperty("failed").GetInt32(), Text(lines[0], "door")));
        Assert.Equal("info", lines[1].Level);
        Assert.Equal(["plugin", "passed", "code", "ms", "door"], Keys(lines[1]));
        Assert.DoesNotContain(Words, Raw());
    }

    /// <summary>A name that is not one — a sentence where an id goes — is written as null, so no line can carry words.</summary>
    [Fact]
    public void A_value_that_is_not_a_name_is_null()
    {
        new PluginLog(_log).Called("acme.gate", $"quest/consider {Words}", "allow", 3);

        var line = Assert.Single(Lines());
        Assert.Equal(JsonValueKind.Null, line.Data.GetProperty("point").ValueKind);
        Assert.DoesNotContain(Words, Raw());
    }

    // ——— the wire marks each failure with its kind

    [Theory]
    [InlineData("late")]
    [InlineData("unreadable")]
    [InlineData("errored")]
    [InlineData("exited")]
    public async Task The_wire_marks_each_failure_with_its_kind(string kind)
    {
        var toHost = Channel.CreateUnbounded<string>();
        var plugin = new HandlerWriter(line =>
        {
            var frame = JsonDocument.Parse(line).RootElement;
            if (!frame.TryGetProperty("id", out var id)) return;
            string Answer(string rest) => "{\"jsonrpc\":\"2.0\",\"id\":" + id.GetRawText() + "," + rest + "}";
            if (frame.GetProperty("method").GetString() == "initialize")
            {
                toHost.Writer.TryWrite(Answer("""
                    "result":{"protocolVersion":1,"points":["quest/consider"]}
                    """));
                return;
            }

            switch (kind)
            {
                case "unreadable": toHost.Writer.TryWrite(Answer("""
                    "result":{"kind":"maybe"}
                    """)); break;
                case "errored": toHost.Writer.TryWrite(Answer("""
                    "error":{"code":1,"message":"no"}
                    """)); break;
                case "exited": toHost.Writer.TryComplete(); break;
            }
        });
        var peer = new HookPeer(new ChannelReader(toHost), plugin, "acme.gate", patience: TimeSpan.FromMilliseconds(kind == "late" ? 100 : 5000));
        // The late case asks with no handshake: a handshake bounded by the same short patience failed under the
        // suite's own load before the call it was there to test.
        if (kind != "late") await peer.InitializeAsync(_home, _home, ["quest/consider"], CancellationToken.None);

        var error = await Assert.ThrowsAsync<DriverException>(() => peer.ConsiderAsync(new { }, CancellationToken.None));

        Assert.Equal(kind, PluginFailures.KindOf(error, "none"));
        await peer.DisposeAsync();
    }

    // ——— helpers

    private HookSet Set(Func<PluginEntry, Task<IHookChannel>> start) =>
        new(_home, start: (plugin, _, _) => start(plugin), log: _log);

    private PluginCatalog Catalog(params (string Id, string[] Points, string Version)[] plugins)
    {
        foreach (var (id, points, version) in plugins)
        {
            var folder = Path.Combine(_home, "plugins", id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
            {
                id,
                version,
                hooks = new { command = new[] { "node", "hooks.mjs" }, points },
            }));
        }

        return PluginCatalog.Load(_home);
    }

    private static DriverException Mark(DriverException error, string kind) => PluginFailures.Mark(error, kind);

    private static Consideration Start(string id) => new(
        new QuestView(id, "game", "engine", "Expose a streaming budget", "World streaming needs a per-frame cap.", "Open"),
        StartVerdict.Start, "start", "D:/fam/engine", "default");

    private static SessionEnded Ended() =>
        new("s1", "engine", "completed", ByPerson: false, Quest: "q1", Adapter: "acp-stub");

    private static LandingFrame Landing() =>
        new("engine", "default", "D:/fam/engine", "feature/q1-fix", "main", "Fix", "q1", "s1", []);

    private List<LogLine> Lines() =>
        [.. MachineLogReader.Read(Path.Combine(_home, MachineLog.Folder), new LogFilter()).Lines];

    private string Raw()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        return Directory.Exists(folder) ? string.Join("\n", Directory.GetFiles(folder).Select(StubFile.Text)) : "";
    }

    private static string? Text(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> Keys(LogLine line) => [.. line.Data.EnumerateObject().Select(p => p.Name)];

    private sealed class FakeChannel(
        IReadOnlyList<string> points, Func<object, HookDecision>? consider = null, Func<object, PluginLanding>? land = null) : IHookChannel
    {
        public IReadOnlyList<string> Points => points;

        public bool Alive { get; set; } = true;

        public int? Code { get; set; }

        public int? ExitCode => Code;

        /// <summary>What every call throws, once set.</summary>
        public DriverException? Throw { get; set; }

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) =>
            Throw is { } error ? Task.FromException<HookDecision>(error) : Task.FromResult(consider?.Invoke(payload) ?? HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) =>
            Throw is { } error ? Task.FromException(error) : Task.CompletedTask;

        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) =>
            Throw is { } error
                ? Task.FromException<PluginLanding>(error)
                : Task.FromResult(land?.Invoke(payload) ?? new PluginLanding("acme", true, null, "pushed"));

        public ValueTask DisposeAsync()
        {
            Alive = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ChannelReader(Channel<string> channel) : TextReader
    {
        public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();

        public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
            await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false) && channel.Reader.TryRead(out var line) ? line : null;
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
