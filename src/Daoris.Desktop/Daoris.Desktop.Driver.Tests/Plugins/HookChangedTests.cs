using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin observes the driver's state as it changes (STATEHUB1g, D169 point 9): <c>state/changed</c>, an observation the shell's bus
/// feeds, told over the hook wire and contained, and never on the path of the press whose change it tells.
/// </summary>
/// <remarks>Fakes over in-memory streams only: no process starts here.</remarks>
public sealed class HookChangedTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-changed-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A plugin that speaks the wire from memory: it answers the handshake with its points and every hook with <c>{}</c>.</summary>
    private sealed class WirePlugin(params string[] points)
    {
        private readonly Channel<string> _toHost = Channel.CreateUnbounded<string>();

        public List<JsonElement> Heard { get; } = [];

        public TextReader Incoming => new Reader(_toHost);

        public TextWriter Outgoing => new Writer(line =>
        {
            var frame = JsonDocument.Parse(line).RootElement.Clone();
            lock (Heard) Heard.Add(frame);
            if (!frame.TryGetProperty("id", out var id)) return;
            var result = frame.GetProperty("method").GetString() == "initialize"
                ? JsonSerializer.Serialize(new { protocolVersion = HookPeer.ProtocolVersion, points })
                : "{}";
            _toHost.Writer.TryWrite($$"""{"jsonrpc":"2.0","id":{{id.GetRawText()}},"result":{{result}}}""");
        });

        public IReadOnlyList<JsonElement> Calls(string method)
        {
            lock (Heard) return [.. Heard.Where(frame => frame.TryGetProperty("method", out var m) && m.GetString() == method)];
        }

        private sealed class Reader(Channel<string> channel) : TextReader
        {
            public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();

            public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
                await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false) && channel.Reader.TryRead(out var line) ? line : null;
        }

        private sealed class Writer(Action<string> onLine) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override Task WriteLineAsync(string? value)
            {
                if (value is not null) onLine(value);
                return Task.CompletedTask;
            }
        }
    }

    /// <summary>A channel that hears each change only once the test lets it: a plugin as slow as the test wants.</summary>
    private sealed class HeldChannel(IReadOnlyList<string> points, Func<object, Task>? changed = null) : IHookChannel
    {
        public IReadOnlyList<string> Points => points;

        public bool Alive { get; private set; } = true;

        public List<object> Told { get; } = [];

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) =>
            throw new DriverException("the loop never asks a plugin to land work");

        public async Task ChangedAsync(object payload, CancellationToken ct)
        {
            if (changed is not null) await changed(payload).WaitAsync(ct);
            lock (Told) Told.Add(payload);
        }

        public ValueTask DisposeAsync()
        {
            Alive = false;
            return ValueTask.CompletedTask;
        }
    }

    private PluginCatalog Catalog(params (string Id, string[] Points)[] plugins)
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Plugins));
        foreach (var (id, points) in plugins)
        {
            var folder = DaorisHome.Entry(_home, HomeEntry.Plugins,id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
            {
                id,
                hooks = new { command = new[] { "node", "hooks.mjs" }, points },
            }));
        }

        return PluginCatalog.Load(_home);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        for (var waited = 0; !condition(); waited += 20)
        {
            Assert.True(waited < 5000, "what was waited for never happened");
            await Task.Delay(20);
        }
    }

    private static string Json(object frame) => JsonSerializer.SerializeToNode(frame)!.ToJsonString();

    [Fact]
    public void The_point_is_an_observation_added_after_the_others_and_kept_running_beside_the_loop()
    {
        Assert.Equal("state/changed", HookPoints.StateChanged);
        // A point is additive (PLUGHOOK1a): the earlier points keep their places.
        Assert.Equal([HookPoints.QuestConsider, HookPoints.SessionEnded, HookPoints.Land, HookPoints.State, HookPoints.StateChanged], HookPoints.All);
        // The loop's set keeps its process: the shell's bus tells it, and a landing never does.
        Assert.Contains(HookPoints.StateChanged, HookPoints.Loop);
        var kit = PluginKit.Find(HookPoints.StateChanged)!;
        Assert.Equal("observation", kit.Kind);
        Assert.Equal(HookPeer.DefaultPatience, kit.Patience);
    }

    [Fact]
    public void The_frame_names_what_moved_and_the_sessions_it_was_about_and_nothing_else()
    {
        Assert.Equal(
            """{"scopes":["sessions","landing","diff","handOff","sweep","treesSync"],"sessions":["s1a2b3c4"]}""",
            Json(HookFrames.Changed(PluginKit.SampleChange)));
        // A change about no one session names none: any.
        Assert.Equal("""{"scopes":["sweep"],"sessions":null}""", Json(HookFrames.Changed(new StateChangedFrame(["sweep"], null))));
        // The kit's sample is the driver's frame.
        Assert.Equal(Json(HookFrames.Changed(PluginKit.SampleChange)), PluginKit.Find(HookPoints.StateChanged)!.Frame.ToJsonString());
    }

    [Fact]
    public async Task A_sample_plugin_receives_the_kit_s_frame_on_the_wire_and_its_answer_is_not_read()
    {
        var plugin = new WirePlugin(HookPoints.StateChanged);
        await using var set = new HookSet(_home, start: async (entry, onLine, ct) =>
        {
            var peer = new HookPeer(plugin.Incoming, plugin.Outgoing, entry.Manifest.Id, onLine, Patience);
            await peer.InitializeAsync(_home, entry.Data, entry.Manifest.Hooks!.Points, ct);
            return peer;
        });
        await set.ReconcileAsync(Catalog(("acme.watch", [HookPoints.StateChanged])), CancellationToken.None);
        Assert.Equal(["acme.watch"], set.Running);

        set.Tell(PluginKit.SampleChange);

        await UntilAsync(() => plugin.Calls("hook/state/changed").Count == 1);
        var call = Assert.Single(plugin.Calls("hook/state/changed"));
        Assert.Equal(PluginKit.Find(HookPoints.StateChanged)!.Frame.ToJsonString(), call.GetProperty("params").GetRawText());
    }

    [Fact]
    public async Task Telling_returns_at_once_while_a_plugin_is_slow_and_each_change_reaches_it_in_order()
    {
        var let = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new HeldChannel([HookPoints.StateChanged], _ => let.Task);
        await using var set = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(slow));
        await set.ReconcileAsync(Catalog(("acme.slow", [HookPoints.StateChanged])), CancellationToken.None);

        // Each returns while the plugin has not answered the first: had a tell waited on it, this would never get past it.
        set.Tell(new StateChangedFrame(["sessions"], ["s1"]));
        set.Tell(new StateChangedFrame(["landing"], null));
        set.Tell(new StateChangedFrame(["sweep"], ["s2"]));
        Assert.Empty(slow.Told);

        let.SetResult();
        await UntilAsync(() => { lock (slow.Told) return slow.Told.Count == 3; });
        Assert.Equal(
            ["""{"scopes":["sessions"],"sessions":["s1"]}""", """{"scopes":["landing"],"sessions":null}""", """{"scopes":["sweep"],"sessions":["s2"]}"""],
            slow.Told.Select(Json));
    }

    [Fact]
    public async Task A_failing_plugin_is_contained_named_and_another_still_hears()
    {
        var fine = new HeldChannel([HookPoints.StateChanged]);
        var broken = new HeldChannel([HookPoints.StateChanged], _ => throw new DriverException("plugin `b.broken` stopped answering"));
        var listening = new HeldChannel([HookPoints.SessionEnded]);
        var channels = new Dictionary<string, IHookChannel> { ["a.fine"] = fine, ["b.broken"] = broken, ["c.ended"] = listening };
        await using var set = new HookSet(_home, start: (entry, _, _) => Task.FromResult(channels[entry.Manifest.Id]));
        await set.ReconcileAsync(
            Catalog(("a.fine", [HookPoints.StateChanged]), ("b.broken", [HookPoints.StateChanged]), ("c.ended", [HookPoints.SessionEnded])),
            CancellationToken.None);

        var lines = await set.ChangedAsync(PluginKit.SampleChange, CancellationToken.None);

        Assert.Single(fine.Told);
        Assert.Empty(listening.Told);
        var line = Assert.Single(lines);
        Assert.Contains("b.broken", line);
        Assert.Contains("stopped answering", line);
        Assert.Equal(["a.fine", "b.broken", "c.ended"], set.Running);
    }

    [Fact]
    public async Task A_set_that_has_gone_hears_nothing_more_and_a_tell_to_it_is_not_an_error()
    {
        var watch = new HeldChannel([HookPoints.StateChanged]);
        var set = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(watch));
        await set.ReconcileAsync(Catalog(("acme.watch", [HookPoints.StateChanged])), CancellationToken.None);
        await set.DisposeAsync();

        set.Tell(PluginKit.SampleChange);

        await Task.Delay(100);
        Assert.Empty(watch.Told);
    }
}
