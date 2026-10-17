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

        /// <summary>Where set, every answer waits for it: an answer read from the process but not yet handed back.</summary>
        public TaskCompletionSource? Holding { get; set; }

        public async Task PerformAsync(WorkPerformCall call, CancellationToken ct)
        {
            Performed.Add(call);
            if (Holding is { } holding) await holding.Task;
        }

        public async Task<WorkReconcileAnswer> ReconcileStepAsync(WorkReconcileCall call, CancellationToken ct)
        {
            if (Holding is { } holding) await holding.Task;
            return new WorkReconcileAnswer(ReconcileStates.Running);
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

    /// <summary>FABRIC-PERFORM1a, round 2: choice 7's promise kept — an update (a new version or install) starts an unsound plugin's record afresh.</summary>
    [Fact]
    public async Task Updating_an_unsound_plugin_starts_it_again_with_its_record_afresh()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        foreach (var wait in new[] { 1, 5, 0 })
        {
            _started[^1].Alive = false;
            await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
            _now = _now.AddMinutes(wait);
            await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        }

        Assert.True(set.IsUnsound(Deploy));
        Assert.Equal(3, _started.Count);

        // The update: the same plugin, a new version in its manifest.
        var manifest = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Plugins, Deploy), "plugin.json");
        File.WriteAllText(manifest, "{\"version\":\"2.0.0\"," + File.ReadAllText(manifest)[1..]);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        var updated = PluginCatalog.Load(_home).Plugins.Single();
        Assert.True(updated.Contributes, updated.Problem);
        Assert.Equal("2.0.0", updated.Manifest.Version);
        Assert.False(set.IsUnsound(Deploy));
        Assert.Equal(4, _started.Count);
    }

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: a plugin speaking a loop point keeps its process while the lease is elsewhere, neither told nor heard; once
    /// the lease is this loop's again, that same process is heard and told the set with a resync, on its own generation.
    /// </summary>
    [Fact]
    public async Task A_hybrid_is_heard_and_told_again_once_the_lease_returns()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        var hybrid = Catalog((Deploy, [HookPoints.QuestConsider, HookPoints.Watch, HookPoints.Step]));
        await set.ReconcileAsync(hybrid, CancellationToken.None);
        var channel = Assert.Single(_started);

        _holds = false;
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Null(channel.Steps);

        _holds = true;
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        Assert.Single(_started);
        Assert.Equal(1, set.GenerationOf(Deploy));
        Assert.Equal((2, true, 1), (channel.Told.Count, channel.Told[^1].Resync, channel.Told[^1].Generation));
        channel.Send("e1");
        Assert.Equal("e1", Assert.Single(set.TakeSteps()).Step.Id);
    }

    /// <summary>The frame bound a plugin is read under (FABRIC-PERFORM1a, round 2): 64 KB where it speaks a work point, else the answers' bound.</summary>
    [Fact]
    public void A_plugin_is_read_under_the_work_wire_s_bound_only_where_it_speaks_a_work_point()
    {
        var catalog = Catalog(("acme.lands", [HookPoints.Land, HookPoints.State]), (Deploy, [HookPoints.Land, HookPoints.Watch, HookPoints.Step]));

        Assert.Equal(HookPeer.AnswerBytes, HookProcess.FrameLimit(catalog.Contributing.Single(p => p.Manifest.Id == "acme.lands")));
        Assert.Equal(WorkBounds.FrameBytes, HookProcess.FrameLimit(catalog.Contributing.Single(p => p.Manifest.Id == Deploy)));
        Assert.True(HookPeer.AnswerBytes >= 64 * WorkBounds.FrameBytes);
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

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: an answer read from a process is handed back only while that process is still the plugin's current
    /// generation. One that completes after its process was stopped and the next generation started is refused, never returned as the new one's.
    /// </summary>
    [Fact]
    public async Task An_answer_from_a_generation_since_replaced_is_refused_when_it_is_handed_back()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var first = _started[0];
        first.Holding = new TaskCompletionSource();
        var performing = set.PerformAsync(Deploy, new WorkPerformCall("u1", "acme.deploy/deploy", 1, "i1", "k1", "r1", [], _now.AddMinutes(10)), CancellationToken.None);
        var reconciling = set.ReconcileStepAsync(Deploy, new WorkReconcileCall("u1", "acme.deploy/deploy", "i1", "k1", "r1"), CancellationToken.None);

        // The lease goes and comes back: generation 1 stopped, generation 2 started; then generation 1's answers are handed back.
        _holds = false;
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        _holds = true;
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Equal(2, set.GenerationOf(Deploy));
        first.Holding.SetResult();

        Assert.Contains("generation 1", (await Assert.ThrowsAsync<DriverException>(() => performing)).Message, StringComparison.Ordinal);
        Assert.Contains("generation 1", (await Assert.ThrowsAsync<DriverException>(() => reconciling)).Message, StringComparison.Ordinal);

        // The current generation's are handed back with it.
        Assert.Equal(2, await set.PerformAsync(Deploy, new WorkPerformCall("u1", "acme.deploy/deploy", 1, "i2", "k1", "r1", [], _now.AddMinutes(10)), CancellationToken.None));
    }

    // ——— the bounds, and the resync after a drop

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: a resync's replay is admitted whole. A plugin replaying one event per awaited step in the same order each
    /// time had its first 60 taken and the rest dropped at every resync, for ever; the minute after a resync now takes one more per awaited
    /// step, and as many more wait.
    /// </summary>
    [Theory]
    [InlineData(61)]
    [InlineData(300)]
    public async Task Every_awaited_step_replayed_at_a_resync_is_heard_however_many_there_are(int steps)
    {
        await using var set = Set();
        set.Watch(Awaiting([.. Enumerable.Range(0, steps).Select(n => Unit($"quest/{n}"))]));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var channel = _started[0];

        for (var resync = 1; resync <= 3; resync++)
        {
            Assert.Equal((resync, true), (channel.Told.Count, channel.Told[^1].Resync));
            for (var n = 0; n < steps; n++) channel.Send($"r{resync}-{n}");

            var heard = set.TakeSteps();
            Assert.Equal(steps, heard.Count);
            Assert.Equal($"r{resync}-{steps - 1}", heard[^1].Step.Id);

            // A frame dropped asks for the next resync, a minute on.
            channel.Steps!(null);
            _now = _now.AddMinutes(1);
            await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        }
    }

    [Fact]
    public async Task Sixty_events_a_minute_are_heard_and_a_drop_is_answered_with_a_resync_a_minute_on()
    {
        await using var set = Set();
        set.Watch(Awaiting(Unit()));
        await set.ReconcileAsync(Performer(), CancellationToken.None);
        var channel = _started[0];

        // The minute of the start's resync takes one more beside its 60, for the one awaited step it named (round 2).
        const int replay = 1;
        for (var at = 0; at <= WorkBounds.EventsPerMinute + replay; at++) channel.Send($"e{at}");

        Assert.Equal(WorkBounds.EventsPerMinute + replay, set.TakeSteps().Count);
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

        // Beside the 256, room for the replay of the one awaited step the last resync named (round 2).
        var heard = set.TakeSteps();
        Assert.Equal(WorkBounds.EventsWaiting + 1, heard.Count);
        Assert.Equal("e43", heard[0].Step.Id);
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
