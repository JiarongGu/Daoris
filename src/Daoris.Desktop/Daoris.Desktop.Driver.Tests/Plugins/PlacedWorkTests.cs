using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin with a pull-request step is started per place: each repository and workspace that awaits it has a process of its
/// own, handed the token of the identity serving that place's origin and no other's, and a place whose hand is refused has none. The
/// runtime still sees one plugin, its generations unique across its places. Driven through fakes on the channel: no process starts.
/// </summary>
public sealed class PlacedWorkTests : IDisposable
{
    private const string Pr = "acme-pull-request";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-placed-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly DateTimeOffset _now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
    private readonly List<(WorkChannel Channel, string? Token)> _started = [];
    private readonly List<string> _said = [];

    private readonly MachineLog _log;

    public PlacedWorkTests()
    {
        Directory.CreateDirectory(_home);
        _log = new MachineLog(_home, "desktop", () => _now);
    }

    public void Dispose()
    {
        _log.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class WorkChannel : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Watch, HookPoints.Step, HookPoints.Perform];

        public IReadOnlyList<string> Capabilities => [WorkWire.Capability];

        public bool Alive { get; private set; } = true;

        public List<WorkWatchFrame> Told { get; } = [];

        public List<WorkPerformCall> Performed { get; } = [];

        public Action<JsonElement?>? Steps { get; private set; }

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public void Listen(Action<JsonElement?>? steps) => Steps = steps;

        public bool Fails { get; set; }

        public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct)
        {
            if (Fails) return Task.FromException(new DriverException("the pipe broke"));
            lock (Told) Told.Add(frame);
            return Task.CompletedTask;
        }

        public Task PerformAsync(WorkPerformCall call, CancellationToken ct)
        {
            Performed.Add(call);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Alive = false;
            return ValueTask.CompletedTask;
        }
    }

    private string? _handed;

    private HookSet Set(Func<PluginEntry, WorkPlace, CancellationToken, Task<PlaceHand>>? hand)
    {
        return new HookSet(_home, say: (_, line) => _said.Add(line), holdsHome: () => true, clock: () => _now, log: _log,
            hand: hand is null ? null : (plugin, place, ct) => { _handed = place.Repository; return hand(plugin, place, ct); },
            start: (_, _, _) =>
            {
                var channel = new WorkChannel();
                _started.Add((channel, _handed is null ? null : "token-of-" + _handed));
                return Task.FromResult<IHookChannel>(channel);
            });
    }

    private static Func<PluginEntry, WorkPlace, CancellationToken, Task<PlaceHand>> Tokens(params string[] refused) =>
        (_, place, _) => Task.FromResult(refused.Contains(place.Repository)
            ? new PlaceHand($"this repository's git access denies writes to origin at {place.Repository}, so the plugin was not started")
            : new PlaceHand(null));

    private PluginCatalog Catalog(string step = "pull-request")
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, Pr);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = Pr,
            hooks = new { command = new[] { "node", "plugin.mjs" }, points = new[] { HookPoints.Watch, HookPoints.Step, HookPoints.Perform } },
            steps = new[] { new { id = step, reach = "performed" } },
        }));
        return PluginCatalog.Load(_home);
    }

    private static WatchedUnit Unit(string repository, string unit) =>
        new(repository, "game", "C:/fam/" + repository, "quest/a", Commit, "main", null, ["pull-request"]) { Unit = unit };

    private static Dictionary<string, IReadOnlyList<WatchedUnit>> Awaiting(params WatchedUnit[] units) => new() { [Pr] = units };

    private static WorkPerformCall Call(string unit) => new(unit, Pr + "/pull-request", 1, "i-" + unit, "k-" + unit, Commit, [], new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Each_place_awaiting_the_plugin_has_its_own_process_handed_its_own_token()
    {
        await using var set = Set(Tokens());
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));

        await set.ReconcileAsync(Catalog(), CancellationToken.None);

        Assert.Equal(2, _started.Count);
        Assert.Equal(["token-of-engine", "token-of-tools"], _started.Select(each => each.Token).Order().ToArray());
        Assert.Equal([Pr], set.Running);
        foreach (var (channel, token) in _started)
        {
            var told = Assert.Single(Assert.Single(channel.Told).Units);
            Assert.Equal("token-of-" + told.Repository, token);
        }
    }

    private List<LogLine> Lines(string @event) =>
        [.. MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter()).Lines.Where(line => line.Event == @event)];

    private static string? Text(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [Fact]
    public async Task A_refusal_of_a_placed_plugin_is_one_step_line_naming_the_plugin()
    {
        await using var set = Set(Tokens());
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));
        await set.ReconcileAsync(Catalog(), CancellationToken.None);

        set.Refused(Pr, WorkRefusals.Stale);

        var line = Assert.Single(Lines(PluginEvents.Step));
        Assert.Equal((Pr, WorkRefusals.Stale), (Text(line, "plugin"), Text(line, "code")));
    }

    [Fact]
    public async Task A_division_of_the_awaited_set_is_applied_before_the_next_is_divided()
    {
        var placed = new PlacedWork();
        var applied = new List<string>();
        string Branch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> told) => Assert.Single(Assert.Single(told).Value).Branch;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        placed.Await(Awaiting(Unit("engine", "u1") with { Branch = "quest/old" }));
        var first = Task.Run(() => placed.Apply(told =>
        {
            entered.Set();
            release.Wait();
            lock (applied) applied.Add(Branch(told));
        }));
        entered.Wait();
        placed.Await(Awaiting(Unit("engine", "u1") with { Branch = "quest/new" }));
        var second = Task.Run(() => placed.Apply(told => { lock (applied) applied.Add(Branch(told)); }));
        await Task.WhenAny(second, Task.Delay(200));
        release.Set();
        await Task.WhenAll(first, second);

        Assert.Equal(["quest/old", "quest/new"], applied);
    }

    [Fact]
    public async Task A_watch_that_failed_to_reach_a_place_is_a_failed_line_naming_the_plugin()
    {
        await using var set = Set(Tokens());
        set.Watch(Awaiting(Unit("engine", "u1")));
        await set.ReconcileAsync(Catalog(), CancellationToken.None);
        _started[0].Channel.Fails = true;

        set.Watch(Awaiting(Unit("engine", "u1"), Unit("engine", "u3")));

        for (var wait = 0; wait < 200 && Lines(PluginEvents.Failed).Count == 0; wait++) await Task.Delay(10);
        var line = Assert.Single(Lines(PluginEvents.Failed));
        Assert.Equal((Pr, HookPoints.Watch), (Text(line, "plugin"), Text(line, "where")));
    }

    [Fact]
    public async Task An_attempt_goes_to_the_process_of_its_units_place_and_its_generation_names_that_process_alone()
    {
        await using var set = Set(Tokens());
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));
        await set.ReconcileAsync(Catalog(), CancellationToken.None);

        var first = await set.PerformAsync(Pr, Call("u1"), CancellationToken.None);
        var second = await set.PerformAsync(Pr, Call("u2"), CancellationToken.None);

        Assert.NotEqual(first, second);
        var engine = _started.Single(each => each.Channel.Told[0].Units[0].Repository == "engine").Channel;
        var tools = _started.Single(each => each.Channel.Told[0].Units[0].Repository == "tools").Channel;
        Assert.Equal("u1", Assert.Single(engine.Performed).Unit);
        Assert.Equal("u2", Assert.Single(tools.Performed).Unit);
        Assert.Equal(first, set.GenerationOf(Pr, first));
        Assert.Equal(second, set.GenerationOf(Pr, second));
        await Assert.ThrowsAsync<DriverException>(() => set.PerformAsync(Pr, Call("u-elsewhere"), CancellationToken.None));
    }

    [Fact]
    public async Task A_place_whose_hand_is_refused_has_no_process_and_the_other_place_is_served()
    {
        await using var set = Set(Tokens("tools"));
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));

        await set.ReconcileAsync(Catalog(), CancellationToken.None);

        var served = Assert.Single(_started);
        Assert.Equal("token-of-engine", served.Token);
        Assert.Contains(_said, line => line.Contains("denies writes to origin at tools", StringComparison.Ordinal));
        Assert.DoesNotContain(_said, line => line.Contains("token-of-", StringComparison.Ordinal));
        await Assert.ThrowsAsync<DriverException>(() => set.PerformAsync(Pr, Call("u2"), CancellationToken.None));
    }

    [Fact]
    public async Task A_place_no_unit_awaits_any_longer_stops_its_process_and_the_other_keeps_its_own()
    {
        await using var set = Set(Tokens());
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));
        await set.ReconcileAsync(Catalog(), CancellationToken.None);

        set.Watch(Awaiting(Unit("engine", "u1")));
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        Assert.Equal(2, _started.Count);
        Assert.Single(_started, each => each.Channel.Alive);
        Assert.Equal("token-of-engine", _started.Single(each => each.Channel.Alive).Token);
    }

    [Fact]
    public async Task A_step_event_is_heard_under_the_plugin_with_the_generation_of_the_process_that_sent_it()
    {
        await using var set = Set(Tokens());
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));
        await set.ReconcileAsync(Catalog(), CancellationToken.None);
        var tools = _started.Single(each => each.Channel.Told[0].Units[0].Repository == "tools").Channel;
        var generation = await set.PerformAsync(Pr, Call("u2"), CancellationToken.None);

        tools.Steps!.Invoke(JsonDocument.Parse(WorkWire.Step(new WorkStepFrame("pull-request", new WorkOutcome(WorkStates.Reached), _now, "e1", "tools", "quest/a", Commit))
            .ToJsonString()).RootElement.GetProperty("params").Clone());

        var heard = Assert.Single(set.TakeSteps());
        Assert.Equal(Pr, heard.Plugin);
        Assert.Equal(generation, heard.Generation);
        Assert.Equal(generation, set.GenerationOf(Pr, heard.Generation));
    }

    [Fact]
    public async Task A_plugin_with_no_pull_request_step_is_one_process_and_is_handed_no_token()
    {
        await using var set = Set(Tokens());
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));

        await set.ReconcileAsync(Catalog(step: "deploy"), CancellationToken.None);

        var only = Assert.Single(_started);
        Assert.Null(only.Token);
        Assert.Equal(2, Assert.Single(only.Channel.Told).Units.Count);
    }

    [Fact]
    public async Task With_no_resolver_a_plugin_is_one_process_as_before()
    {
        await using var set = Set(null);
        set.Watch(Awaiting(Unit("engine", "u1"), Unit("tools", "u2")));

        await set.ReconcileAsync(Catalog(), CancellationToken.None);

        Assert.Null(Assert.Single(_started).Token);
    }
}
