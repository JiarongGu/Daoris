using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work wire's supervision (FABRIC-PERFORM1a; the fabric events design §C4, §D6): the loop that holds the home's lease starts a plugin's
/// process when a unit first awaits one of its steps and stops it once none does; restarts an exit it did not ask for after 1, 5 and 15
/// minutes, three within an hour marking the plugin unsound until the person tries again; numbers each start with a generation, so an event
/// from a process since restarted is refused; and resyncs after any restart or drop.
/// </summary>
/// <remarks>Driven through fakes on the wire's channel, with the test's clock and lease: no process starts, and the class stays in the fast half.</remarks>
public sealed class WorkSupervisionTests : IDisposable
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private const string Deploy = "acme.deploy";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-work-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly MachineLog _log;

    private readonly List<WorkChannel> _started = [];

    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private bool _holds = true;

    public WorkSupervisionTests()
    {
        Directory.CreateDirectory(_home);
        _log = new MachineLog(_home, "desktop", () => _now);
    }

    public void Dispose()
    {
        _log.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A plugin's process as the wire's channel: what it is told, what it is asked, and the events it sends.</summary>
    private sealed class WorkChannel(IReadOnlyList<string> points, IReadOnlyList<string> capabilities) : IHookChannel
    {
        public IReadOnlyList<string> Points => points;

        public IReadOnlyList<string> Capabilities => capabilities;

        public bool Alive { get; set; } = true;

        public bool Disposed { get; private set; }

        public List<WorkWatchFrame> Told { get; } = [];

        public List<WorkPerformCall> Performed { get; } = [];

        public Action<JsonElement?>? Steps { get; private set; }

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => throw new DriverException("the loop never asks a plugin to land work");

        public void Listen(Action<JsonElement?>? steps) => Steps = steps;

        public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct)
        {
            lock (Told) Told.Add(frame);
            return Task.CompletedTask;
        }

        public Task PerformAsync(WorkPerformCall call, CancellationToken ct)
        {
            Performed.Add(call);
            return Task.CompletedTask;
        }

        /// <summary>An event as the process sends it: through whatever listener the loop gave it, if any.</summary>
        public void Send(string id) => Steps?.Invoke(Params(id));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            Alive = false;
            return ValueTask.CompletedTask;
        }
    }

    private static JsonElement Params(string id) => JsonDocument.Parse(WorkWire.Step(new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached),
        new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero), id, "engine", "quest/a", Commit)).ToJsonString()).RootElement.GetProperty("params").Clone();

    private HookSet Set(IReadOnlyList<string>? capabilities = null) => new(_home, start: (plugin, _, _) =>
    {
        var channel = new WorkChannel(plugin.Manifest.Hooks!.Points, capabilities ?? [WorkWire.Capability]);
        _started.Add(channel);
        return Task.FromResult<IHookChannel>(channel);
    }, log: _log, holdsHome: () => _holds, clock: () => _now);

    private PluginCatalog Catalog(params (string Id, string[] Points)[] plugins)
    {
        foreach (var (id, points) in plugins)
        {
            var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
            {
                id,
                hooks = new { command = new[] { "node", "hooks.mjs" }, points },
                // A performed step needs `work/perform` named (PluginSteps): one that does not speak it observes.
                steps = new[] { new { id = "deploy", reach = points.Contains(HookPoints.Perform) ? "performed" : "observed" } },
            }));
        }

        return PluginCatalog.Load(_home);
    }

    private PluginCatalog Performer() => Catalog((Deploy, [HookPoints.Watch, HookPoints.Step, HookPoints.Perform]));

    private static WatchedUnit Unit(string branch = "quest/a") => new("engine", "game", "C:/fam/engine", branch, Commit, "main", null, ["deploy"]);

    private static Dictionary<string, IReadOnlyList<WatchedUnit>> Awaiting(params WatchedUnit[] units) => new() { [Deploy] = units };

    private List<LogLine> Lines(string @event) =>
        [.. MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter()).Lines.Where(line => line.Event == @event)];

    private static string? Text(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Count(LogLine line) =>
        line.Data.TryGetProperty("count", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    // ——— who runs it, and when

    [Fact]
    public async Task No_work_process_starts_without_the_lease()
    {
        _holds = false;
        await using var set = Set();
        set.Watch(Awaiting(Unit()));

        await set.ReconcileAsync(Performer(), CancellationToken.None);
        Assert.Empty(_started);
        Assert.Null(set.GenerationOf(Deploy));

        // The lease held: the next look starts it.
        _holds = true;
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Equal([Deploy], set.Running);
        Assert.Equal(1, set.GenerationOf(Deploy));
    }

    /// <summary>A plugin that also speaks at a loop point is started by a loop beside the lease's for that point alone: never told, never heard.</summary>
    [Fact]
    public async Task A_loop_without_the_lease_neither_tells_nor_hears_a_plugin_it_runs_for_another_point()
    {
        _holds = false;
        await using var set = Set();
        set.Watch(Awaiting(Unit()));

        await set.ReconcileAsync(Catalog((Deploy, [HookPoints.QuestConsider, HookPoints.Watch, HookPoints.Step])), CancellationToken.None);

        var channel = Assert.Single(_started);
        Assert.Empty(channel.Told);
        Assert.Null(channel.Steps);
        Assert.Null(set.GenerationOf(Deploy));
    }

    [Fact]
    public async Task A_work_process_starts_when_a_unit_awaits_its_steps_is_told_each_change_and_stops_once_none_does()
    {
        await using var set = Set();
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        Assert.Empty(_started);

        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        var channel = Assert.Single(_started);
        var first = Assert.Single(channel.Told);
        Assert.Equal((1, true, 1), (first.Generation, first.Resync, first.Units.Count));

        // The same set again tells nothing; a changed one is told at once, whole.
        set.Watch(Awaiting(Unit()));
        set.Watch(Awaiting(Unit(), Unit("quest/b")));
        Assert.Equal(2, channel.Told.Count);
        Assert.Equal((1, false, 2), (channel.Told[1].Generation, channel.Told[1].Resync, channel.Told[1].Units.Count));

        set.Watch(new Dictionary<string, IReadOnlyList<WatchedUnit>>());
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.True(channel.Disposed);
        Assert.Empty(set.Running);
        Assert.Equal(PluginEvents.Idle, Text(Assert.Single(Lines(PluginEvents.Stopped)), "why"));
    }

    [Fact]
    public async Task Losing_the_lease_stops_the_work_process_and_its_events_are_no_longer_heard()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var channel = Assert.Single(_started);

        _holds = false;
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        channel.Send("late");

        Assert.True(channel.Disposed);
        Assert.Equal(PluginEvents.NotHeld, Text(Assert.Single(Lines(PluginEvents.Stopped)), "why"));
        Assert.Empty(set.TakeSteps());
    }

    // ——— restarts

    [Fact]
    public async Task An_unasked_exit_restarts_after_its_wait_with_a_new_generation_told_to_resync()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        _started[0].Alive = false;

        var lines = await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Contains(lines, line => line.Contains(Deploy, StringComparison.Ordinal) && line.Contains("1 minute", StringComparison.Ordinal));
        Assert.Single(_started);

        _now = _now.AddSeconds(59);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Single(_started);

        _now = _now.AddSeconds(1);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Equal(2, _started.Count);
        Assert.Equal(2, set.GenerationOf(Deploy));
        var told = Assert.Single(_started[1].Told);
        Assert.Equal((2, true), (told.Generation, told.Resync));
    }

    [Fact]
    public async Task Three_unasked_exits_within_an_hour_mark_the_plugin_unsound_until_it_is_tried_again()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);

        foreach (var wait in new[] { 1, 5 })
        {
            _started[^1].Alive = false;
            await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
            _now = _now.AddMinutes(wait);
            await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        }

        Assert.Equal(3, _started.Count);
        _started[^1].Alive = false;
        var lines = await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        _now = _now.AddHours(2);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        Assert.Equal(3, _started.Count);
        Assert.True(set.IsUnsound(Deploy));
        Assert.Contains(lines, line => line.Contains("try again", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Lines(PluginEvents.Failed), line => Text(line, "kind") == PluginEvents.Unsound && Text(line, "where") == PluginEvents.AtProcess);

        set.TryAgain(Deploy);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Equal(4, _started.Count);
        Assert.False(set.IsUnsound(Deploy));
        Assert.Equal(4, set.GenerationOf(Deploy));
    }

    [Fact]
    public async Task An_exit_after_Daoris_s_own_stop_never_counts()
    {
        await using var set = Set();
        for (var round = 0; round < 4; round++)
        {
            set.Watch(Awaiting(Unit()));
            await set.ReconcileAsync(Performer(), CancellationToken.None);
            set.Watch(new Dictionary<string, IReadOnlyList<WatchedUnit>>());
            await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        }

        // Stopped four times by the loop and started again at once each time: no wait, never unsound.
        Assert.Equal(4, _started.Count);
        Assert.All(_started, channel => Assert.True(channel.Disposed));
        Assert.False(set.IsUnsound(Deploy));
    }

    // ——— generations: a stale answer refused

    [Fact]
    public async Task An_event_from_a_process_since_restarted_is_refused_and_the_new_one_s_is_heard()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var first = _started[0];
        first.Send("e1");
        first.Alive = false;
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        _now = _now.AddMinutes(1);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        _started[1].Send("e2");
        first.Send("e3");
        var heard = set.TakeSteps();

        var only = Assert.Single(heard);
        Assert.Equal((Deploy, 2, "e2"), (only.Plugin, only.Generation, only.Step.Id));
        var refused = Assert.Single(Lines(PluginEvents.Step));
        Assert.Equal((Deploy, WorkRefusals.NotSender), (Text(refused, "plugin"), Text(refused, "code")));
    }

    // ——— the bounds, and the resync after a drop

    [Fact]
    public async Task Sixty_events_a_minute_are_heard_and_a_drop_is_answered_with_a_resync_a_minute_on()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var channel = _started[0];

        for (var at = 0; at <= WorkBounds.EventsPerMinute; at++) channel.Send($"e{at}");

        Assert.Equal(WorkBounds.EventsPerMinute, set.TakeSteps().Count);
        var refused = Assert.Single(Lines(PluginEvents.Step));
        Assert.Equal((WorkRefusals.TooFast, 1), (Text(refused, "code"), Count(refused)));

        // The start's resync was this minute: the drop's waits for the next.
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Single(channel.Told);
        _now = _now.AddMinutes(1);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Equal((2, true, 1), (channel.Told.Count, channel.Told[1].Resync, channel.Told[1].Generation));
    }

    [Fact]
    public async Task At_most_256_events_wait_and_the_oldest_goes_first()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var channel = _started[0];

        for (var minute = 0; minute < 5; minute++)
        {
            for (var at = 0; at < WorkBounds.EventsPerMinute; at++) channel.Send($"e{minute * WorkBounds.EventsPerMinute + at}");
            _now = _now.AddMinutes(1);
        }

        var heard = set.TakeSteps();
        Assert.Equal(WorkBounds.EventsWaiting, heard.Count);
        Assert.Equal("e44", heard[0].Step.Id);
        Assert.Equal("e299", heard[^1].Step.Id);
        Assert.Contains(Lines(PluginEvents.Step), line => Text(line, "code") == WorkRefusals.QueueFull);
    }

    [Fact]
    public async Task A_frame_dropped_unread_and_one_of_the_wrong_shape_are_counted_without_their_words()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var channel = _started[0];

        channel.Steps!(null);
        channel.Steps!(JsonDocument.Parse("""{"step":"deploy","state":"SECRET-WORDS"}""").RootElement.Clone());
        Assert.Empty(set.TakeSteps());

        var lines = Lines(PluginEvents.Step);
        Assert.Equal([WorkRefusals.TooLong, WorkRefusals.Shape], lines.Select(line => Text(line, "code")));
        Assert.All(lines, line => Assert.Equal(["plugin", "code", "count"], line.Data.EnumerateObject().Select(p => p.Name)));
        Assert.DoesNotContain(lines, line => line.Data.GetRawText().Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_refusal_held_within_its_minute_is_written_with_its_count_at_a_later_look()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var channel = _started[0];

        for (var at = 0; at < 3; at++) channel.Steps!(null);
        Assert.Single(Lines(PluginEvents.Step));

        _now = _now.AddMinutes(1);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        Assert.Equal([1, 2], Lines(PluginEvents.Step).Select(Count));
    }

    // ——— asking a performer

    [Fact]
    public async Task A_performer_is_asked_on_its_current_process_and_its_generation_named()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var call = new WorkPerformCall("u1", "acme.deploy/deploy", 1, "i1", "k1", "r1", [], _now.AddMinutes(10));

        Assert.Equal(1, await set.PerformAsync(Deploy, call, CancellationToken.None));
        Assert.Equal([call], _started[0].Performed);

        // Not running, or not this loop's: refused, and nothing sent.
        await Assert.ThrowsAsync<DriverException>(() => set.PerformAsync("acme.other", call, CancellationToken.None));
        _holds = false;
        await Assert.ThrowsAsync<DriverException>(() => set.PerformAsync(Deploy, call, CancellationToken.None));
        Assert.Single(_started[0].Performed);
    }

    [Fact]
    public async Task A_process_that_did_not_name_the_capability_is_never_asked_to_perform()
    {
        await using var set = Set(capabilities: []);
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<DriverException>(() =>
            set.PerformAsync(Deploy, new WorkPerformCall("u1", "acme.deploy/deploy", 1, "i1", "k1", "r1", [], _now), CancellationToken.None));

        Assert.Contains(WorkWire.Capability, error.Message, StringComparison.Ordinal);
        Assert.Empty(_started[0].Performed);
    }
}
