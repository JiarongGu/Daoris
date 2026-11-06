using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>work/event</c> wired into the hook set (EVENTHOOK1b; the event hook design §4.1, §4.4, §9's proofs 3, 6, 7, 10, 11, 13): a plugin on the
/// point wanted only by the lease's loop, started with the handshake that says where its events begin, told in order, stopped after three late
/// answers and held to its wait; the lease's busy read kept; the build of a laid-out install; and the session kinds read from the news teller.
/// Driven through fake channels, so no process starts.
/// </summary>
public sealed class WorkEventWiringTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workevent-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly MachineLog _log;
    private DateTimeOffset _now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    public WorkEventWiringTests()
    {
        Directory.CreateDirectory(_home);
        _log = new MachineLog(_home, "desktop", () => _now);
    }

    public void Dispose()
    {
        _log.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task The_lease_loop_starts_a_plugin_on_the_point_with_its_handshake_and_tells_it_each_event_once_in_order()
    {
        var channel = new EventChannel([HookPoints.Event]);
        WorkEventHandshake? handshake = null;
        await using var set = new HookSet(_home, start: (_, _, _) =>
        {
            handshake = HookProcess.Events.Value;
            return Task.FromResult<IHookChannel>(channel);
        }, log: _log, holdsHome: () => true, clock: () => _now);
        set.Tell(Parked("4b62a9fe"));
        set.Tell(Parked("5c73b0af"));

        await set.ReconcileAsync(Catalog(("acme.relay", [HookPoints.Event])), CancellationToken.None);
        set.Tell(Parked("6d84c1b0"));
        set.Tell(new WorkEvent(WorkEventKinds.WorkLanded, _now) { Session = "7e95d2c1", Code = "landed" });
        set.Tell(new WorkEvent(WorkEventKinds.Missed, _now));

        Assert.Equal(new WorkEventHandshake(WorkEventGrammar.Version, 2, RelayBuild.None), handshake);
        var frames = await channel.HeardAsync(3);
        Assert.Equal([3L, 4L, 5L], frames.Select(frame => (long)frame["seq"]!));
        Assert.Equal([WorkEventKinds.SessionParked, WorkEventKinds.WorkLanded, WorkEventKinds.Missed], frames.Select(frame => (string)frame["kind"]!));
        Assert.All(frames, frame => Assert.Equal(set.Events.Run, frame["run"]));
    }

    [Fact]
    public async Task A_share_loop_starts_no_plugin_for_the_point_alone_and_tells_one_beside_session_ended_nothing()
    {
        var channels = new Dictionary<string, EventChannel>(StringComparer.Ordinal);
        var handshakes = new List<WorkEventHandshake?>();
        await using var set = new HookSet(_home, start: (plugin, _, _) =>
        {
            handshakes.Add(HookProcess.Events.Value);
            return Task.FromResult<IHookChannel>(channels[plugin.Manifest.Id] = new EventChannel([.. plugin.Manifest.Hooks!.Points]));
        }, log: _log, holdsHome: () => false, clock: () => _now);

        await set.ReconcileAsync(Catalog(("a.relay", [HookPoints.Event]), ("b.both", [HookPoints.Event, HookPoints.SessionEnded])), CancellationToken.None);
        set.Tell(Parked("4b62a9fe"));

        Assert.Equal(["b.both"], set.Running);
        Assert.Equal([null], handshakes);
        Assert.Empty(set.Events.Listening);
        await Task.Delay(50);
        Assert.Empty(channels["b.both"].Frames);
    }

    [Fact]
    public async Task A_plugin_late_three_times_is_stopped_as_late_and_started_again_only_after_its_wait()
    {
        var starts = 0;
        var channel = new EventChannel([HookPoints.Event], late: true);
        await using var set = new HookSet(_home, start: (_, _, _) =>
        {
            starts++;
            return Task.FromResult<IHookChannel>(channel);
        }, log: _log, holdsHome: () => true, clock: () => _now);
        var catalog = Catalog(("acme.relay", [HookPoints.Event]));
        await set.ReconcileAsync(catalog, CancellationToken.None);

        for (var n = 0; n < WorkEventPoint.LateInARow; n++) set.Tell(Parked("4b62a9fe"));
        await Until(() => !channel.Alive);
        await Until(() => set.Running.Count == 0);
        await set.ReconcileAsync(catalog, CancellationToken.None);

        Assert.Equal(1, starts);
        var stopped = Lines().Single(line => line.Event == PluginEvents.Stopped);
        Assert.Equal(PluginEvents.Late, stopped.Data.GetProperty("why").GetString());
        _now += TimeSpan.FromHours(1);
        await set.ReconcileAsync(catalog, CancellationToken.None);
        Assert.Equal(2, starts);
    }

    [Fact]
    public async Task An_event_whose_required_field_fails_is_told_as_missed_in_its_place_and_logged_by_its_kind_and_field_alone()
    {
        var channel = new EventChannel([HookPoints.Event]);
        await using var set = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(channel), log: _log, holdsHome: () => true, clock: () => _now);
        await set.ReconcileAsync(Catalog(("acme.relay", [HookPoints.Event])), CancellationToken.None);

        set.Tell(Parked("Merge-every-landed-branch.now"));

        var frame = Assert.Single(await channel.HeardAsync(1));
        Assert.Equal((WorkEventKinds.Missed, 1L), ((string)frame["kind"]!, (long)frame["seq"]!));
        var unsent = Lines().Single(line => line.Event == PluginEvents.Unsent);
        Assert.Equal((WorkEventKinds.SessionParked, "session"), (unsent.Data.GetProperty("kind").GetString(), unsent.Data.GetProperty("field").GetString()));
        Assert.DoesNotContain("Merge-every", string.Join("\n", Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs)).Select(StubFile.Text)));
    }

    [Fact]
    public async Task An_end_the_home_s_end_keeping_writes_and_a_settled_press_are_told_to_the_lease_loop_s_plugin()
    {
        var channel = new EventChannel([HookPoints.Event]);
        await using var set = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(channel), log: _log, holdsHome: () => true, clock: () => _now);
        await set.ReconcileAsync(Catalog(("acme.relay", [HookPoints.Event])), CancellationToken.None);

        WorkEnds.Said(_home, [("4b62a9fe", "48f8ed9121ec")]);
        set.Tell(WorkEventPlugins.Settled("0123456789abcdef0123456789abcdef", nameof(PipeEndSpelled.Refused), _now));

        var frames = await channel.HeardAsync(2);
        Assert.Equal((WorkEventKinds.WorkEnded, "4b62a9fe", "48f8ed9121ec"), ((string)frames[0]["kind"]!, (string)frames[0]["session"]!, (string)frames[0]["quest"]!));
        Assert.Equal((WorkEventKinds.ConfirmationEnded, "refused"), ((string)frames[1]["kind"]!, (string)frames[1]["end"]!));
    }

    // The ending's spelling as the pipe's enum names it, without the modules' type.
    private enum PipeEndSpelled { Refused }

    [Fact]
    public void A_busy_lease_read_keeps_the_answer_before_it()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        Assert.NotNull(lease);
        var held = WorkEventPlugins.Held(_home);
        Assert.True(held());

        using (new FileStream(HomeLease.PathIn(_home), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Null(HomeLease.HeldHereRead(_home));
            Assert.True(held());
        }

        Assert.True(HomeLease.HeldHereRead(_home));
    }

    [Fact]
    public void The_handshake_build_is_the_laid_out_install_s_driver_bundle_and_a_companion_library_is_part_of_it()
    {
        var install = Path.Combine(_home, "install");
        var driver = Directory.CreateDirectory(Path.Combine(install, "app", "daoris-driver")).FullName;
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "installed");
        File.WriteAllText(Path.Combine(driver, "daoris-driver.dll"), "driver");
        File.WriteAllText(Path.Combine(driver, "companion.dll"), "one");
        var home = Directory.CreateDirectory(Path.Combine(install, DaorisHome.InstallData)).FullName;

        var build = WorkEventPlugins.Build(home);

        Assert.Equal(RelayBuild.Of(driver), build);
        Assert.NotEqual(RelayBuild.None, build);
        File.WriteAllText(Path.Combine(driver, "companion.dll"), "two");
        Assert.NotEqual(build, RelayBuild.Of(driver));
        Assert.Equal(RelayBuild.None, WorkEventPlugins.Build(_home));
    }

    [Fact]
    public void A_claimed_park_and_end_are_their_kinds_with_the_record_s_quest_and_ask_and_a_quest_s_park_its_own()
    {
        var record = new SessionView("4b62a9fe", "ask #a1b2c3") { Quest = "48f8ed9121ec", Ask = "a1b2c3" };
        var parked = WorkEventNews.Of(new NewsTold(AttentionKind.Parked, "4b62a9fe", 7, "store", null, null, null, null, _now), record);
        var ended = WorkEventNews.Of(new NewsTold(AttentionKind.Ended, "4b62a9fe", 8, "store", null, "failed", "ended.failed", "failed", _now), null);
        var questParked = WorkEventNews.Of(new AttentionEvent(AttentionKind.QuestParked, "4b62a9fe", "engine") { Quest = "48f8ed9121ec", Strikes = 3 }, _now);

        Assert.Equal(new WorkEvent(WorkEventKinds.SessionParked, _now) { Session = "4b62a9fe", Quest = "48f8ed9121ec", Ask = "a1b2c3" }, parked);
        Assert.Equal(new WorkEvent(WorkEventKinds.SessionEnded, _now) { Session = "4b62a9fe", State = "failed" }, ended);
        Assert.Equal(new WorkEvent(WorkEventKinds.QuestParked, _now) { Quest = "48f8ed9121ec", Session = "4b62a9fe", Strikes = 3 }, questParked);
        Assert.Null(WorkEventNews.Of(new AttentionEvent(AttentionKind.Waiting, "", "engine"), _now));
    }

    [Fact]
    public async Task The_news_teller_s_gaps_are_one_missed_at_a_first_place_with_no_cursor_and_one_at_an_incomplete_batch()
    {
        var told = new List<WorkEvent>();
        JournalPosition? kept = null;
        var batches = 0;
        var teller = WorkEventNews.Gapped(new JournalTeller(_ => { batches++; return Task.CompletedTask; }, () => kept, at => kept = at), told.Add, () => _now);

        Assert.Null(teller.Load!());
        teller.Keep!(new JournalPosition("store", 10));
        teller.Keep!(new JournalPosition("store", 11));
        await teller.Told(new JournalBatch("store", 1, 12, [], Missed: true));
        await teller.Told(new JournalBatch("store", 1, 13, []));

        Assert.Equal(2, told.Count);
        Assert.All(told, each => Assert.Equal(WorkEventKinds.Missed, each.Kind));
        Assert.Equal(2, batches);

        var again = new List<WorkEvent>();
        var placed = WorkEventNews.Gapped(new JournalTeller(_ => Task.CompletedTask, () => kept, at => kept = at), again.Add, () => _now);
        Assert.NotNull(placed.Load!());
        placed.Keep!(new JournalPosition("store", 14));
        Assert.Empty(again);
    }

    private WorkEvent Parked(string session) => new(WorkEventKinds.SessionParked, _now) { Session = session };

    private static async Task Until(Func<bool> done)
    {
        for (var n = 0; n < 200 && !done(); n++) await Task.Delay(10);
        Assert.True(done());
    }

    private List<LogLine> Lines() =>
        [.. MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter()).Lines];

    private PluginCatalog Catalog(params (string Id, string[] Points)[] plugins)
    {
        foreach (var (id, points) in plugins)
        {
            var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
            {
                id,
                version = "1.0.0",
                hooks = new { command = new[] { "node", "hooks.mjs" }, points },
            }));
        }

        return PluginCatalog.Load(_home);
    }

    private sealed class EventChannel(IReadOnlyList<string> points, bool late = false) : IHookChannel
    {
        public IReadOnlyList<string> Points => points;

        public bool Alive { get; private set; } = true;

        public List<IReadOnlyDictionary<string, object?>> Frames { get; } = [];

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public Task EventAsync(object frame, CancellationToken ct)
        {
            if (late) return Task.FromException(PluginFailures.Mark(new DriverException("late"), PluginEvents.Late));
            lock (Frames) Frames.Add((IReadOnlyDictionary<string, object?>)frame);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> HeardAsync(int count)
        {
            for (var n = 0; n < 200; n++)
            {
                lock (Frames) if (Frames.Count >= count) return [.. Frames];
                await Task.Delay(10);
            }

            lock (Frames) return [.. Frames];
        }

        public ValueTask DisposeAsync()
        {
            Alive = false;
            return ValueTask.CompletedTask;
        }
    }
}
