using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The change watch (FABRIC-EVENT2, the design §A3 and §A4.1) on its fake source, stepped by hand on a clock that stands still:
/// the design's canaries 1-3. A hint is debounced into its topic's queue and judged once per subject, never twice at once; a
/// silenced source still judges by the topic's deadline; an overflow, a lost handle, a replaced or absent folder and a resume
/// each judge their root at once and reopen it, and a callback from an old generation is dropped.
/// </summary>
public sealed class ChangeWatchTests : IDisposable
{
    private const string Folder = "requests-folder";

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-change-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeChangeSource _source = new();

    private readonly List<ChangeWake> _judged = [];

    private DateTimeOffset _now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    public ChangeWatchTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private ChangeWake[] Judged()
    {
        lock (_judged) return [.. _judged];
    }

    private Task<int> Record(ChangeWake wake, CancellationToken ct)
    {
        lock (_judged) _judged.Add(wake);
        return Task.FromResult(0);
    }

    /// <summary>A watch on one folder whose <c>.json</c> files it hears, never their deletion, opened and not pumping.</summary>
    private ChangeWatch Watch(
        Func<ChangeWake, CancellationToken, Task<int>>? judge = null, TimeSpan? deadline = null, MachineLog? log = null, int atOnce = 1,
        params string[] more)
    {
        var watch = new ChangeWatch(_source, () => _now) { Log = log };
        watch.Topic("requests", deadline ?? Deadline, judge ?? Record, atOnce);
        foreach (var folder in (string[])[Folder, .. more])
        {
            watch.Watch("requests", folder == Folder ? "requests" : folder, folder, Hears);
        }

        watch.Open();
        return watch;
    }

    private static bool Hears(string name, ChangeKind kind) => kind != ChangeKind.Deleted && name.EndsWith(".json", StringComparison.Ordinal);

    /// <summary>Time passes, then one step of the watch, and every judgement it began is waited for.</summary>
    private async Task Step(ChangeWatch watch, TimeSpan by)
    {
        _now += by;
        watch.Pump();
        await watch.Settled();
    }

    private MachineLog Log() => new(_home, "driver", () => _now);

    private JsonElement[] Lines(string @event)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.jsonl")
                .SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == @event),
        ];
    }

    /// <summary>Canary 3: a burst of hints for one subject is one judgement, once the folder has been quiet for the window.</summary>
    [Fact]
    public async Task A_burst_of_hints_for_one_subject_is_one_judgement()
    {
        await using var watch = Watch();
        var handle = _source.On(Folder)!;

        for (var i = 0; i < 5; i++)
        {
            handle.Raise($"s{i}.json");
            await Step(watch, TimeSpan.FromMilliseconds(100));
        }

        Assert.Empty(Judged());
        await Step(watch, ChangeWatch.Quiet);

        Assert.Equal(new ChangeWake("requests", "requests", ChangeWhy.Hint), Assert.Single(Judged()));
    }

    /// <summary>The window coalesces, and is capped: a steady stream still wakes its judge two seconds after its first hint.</summary>
    [Fact]
    public async Task A_steady_stream_of_hints_still_wakes_at_the_cap()
    {
        await using var watch = Watch();
        var handle = _source.On(Folder)!;

        var first = _now;
        while (_now - first < ChangeWatch.Cap)
        {
            handle.Raise("s1.json");
            await Step(watch, TimeSpan.FromMilliseconds(200));
            if (_now - first < ChangeWatch.Cap) Assert.Empty(Judged());
        }

        Assert.Equal(ChangeWhy.Hint, Assert.Single(Judged()).Why);
    }

    /// <summary>
    /// Canary 3: a hint that arrives while its subject is judged is kept for the next judgement, never lost and never judged
    /// beside the one running.
    /// </summary>
    [Fact]
    public async Task A_hint_during_a_judgement_is_judged_after_it_never_beside_it()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var most = 0;
        var judgements = 0;
        await using var watch = Watch(async (wake, ct) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            Interlocked.Increment(ref judgements);
            await release.Task.WaitAsync(ct);
            Interlocked.Decrement(ref running);
            return 1;
        });
        var handle = _source.On(Folder)!;

        handle.Raise("s1.json");
        _now += ChangeWatch.Quiet;
        watch.Pump();
        await Poll.Until(() => Volatile.Read(ref judgements) == 1);

        handle.Raise("s2.json");
        _now += ChangeWatch.Quiet;
        watch.Pump();
        _now += Deadline;
        watch.Pump();
        Assert.Equal(1, Volatile.Read(ref judgements));

        release.SetResult();
        await watch.Settled();

        Assert.Equal(2, Volatile.Read(ref judgements));
        Assert.Equal(1, Volatile.Read(ref most));
    }

    /// <summary>A topic judges its subjects at once up to its bound, and each subject once.</summary>
    [Fact]
    public async Task A_topic_judges_its_subjects_up_to_its_bound_at_once_and_each_once()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var most = 0;
        await using var watch = Watch(
            async (wake, ct) =>
            {
                InterlockedMax(ref most, Interlocked.Increment(ref running));
                lock (_judged) _judged.Add(wake);
                await release.Task.WaitAsync(ct);
                Interlocked.Decrement(ref running);
                return 0;
            },
            atOnce: 2, more: ["b", "c"]);

        foreach (var folder in (string[])[Folder, "b", "c"]) _source.On(folder)!.Raise("x.json");
        _now += ChangeWatch.Quiet;
        watch.Pump();
        await Poll.Until(() => Volatile.Read(ref running) == 2);
        release.SetResult();
        await watch.Settled();

        Assert.Equal(2, most);
        Assert.Equal(["b", "c", "requests"], Judged().Select(wake => wake.Subject).Order());
    }

    /// <summary>Canary 1: with the source silenced, a subject is judged by its topic's deadline, and at every deadline after.</summary>
    [Fact]
    public async Task With_the_source_silenced_a_subject_is_judged_at_its_deadline()
    {
        await using var watch = Watch();

        await Step(watch, Deadline - TimeSpan.FromMilliseconds(1));
        Assert.Empty(Judged());
        await Step(watch, TimeSpan.FromMilliseconds(1));
        Assert.Equal(ChangeWhy.Deadline, Assert.Single(Judged()).Why);
        await Step(watch, Deadline);

        Assert.Equal([ChangeWhy.Deadline, ChangeWhy.Deadline], Judged().Select(wake => wake.Why));
    }

    /// <summary>A move the root does not hear wakes nothing: a deletion, a temporary's name, another kind of file.</summary>
    [Fact]
    public async Task A_move_the_root_does_not_hear_wakes_nothing()
    {
        await using var watch = Watch();
        var handle = _source.On(Folder)!;

        handle.Raise("s1.json", ChangeKind.Deleted);
        handle.Raise("s1.json.0a1b2c3d.taking", ChangeKind.Renamed);
        handle.Raise("note.told", ChangeKind.Created);
        await Step(watch, ChangeWatch.Cap);

        Assert.Empty(Judged());
    }

    /// <summary>
    /// Canary 2: an overflow judges its root at once and reopens it under a new generation; what the old watcher still says is
    /// dropped, and the new one is heard.
    /// </summary>
    [Fact]
    public async Task An_overflow_judges_the_root_at_once_and_reopens_it_and_the_old_generation_is_dropped()
    {
        await using var watch = Watch();
        var old = _source.On(Folder)!;

        old.Lose();
        await Step(watch, TimeSpan.Zero);

        Assert.Equal(ChangeWhy.Lost, Assert.Single(Judged()).Why);
        Assert.True(old.Disposed);
        var fresh = _source.On(Folder);
        Assert.NotNull(fresh);
        Assert.NotSame(old, fresh);

        old.Raise("late.json");
        await Step(watch, ChangeWatch.Cap);
        Assert.Single(Judged());
        Assert.Equal(1, watch.Dropped);

        fresh.Raise("s1.json");
        await Step(watch, ChangeWatch.Cap);
        Assert.Equal([ChangeWhy.Lost, ChangeWhy.Hint], Judged().Select(wake => wake.Why));
    }

    /// <summary>Canary 2: after a resume every root is reopened and judged at once.</summary>
    [Fact]
    public async Task A_resume_reopens_every_root_and_judges_it_at_once()
    {
        await using var watch = Watch(more: ["b"]);
        var before = _source.Opened.ToList();

        watch.Resumed();
        await Step(watch, TimeSpan.Zero);

        Assert.All(before, handle => Assert.True(handle.Disposed));
        Assert.NotNull(_source.On(Folder));
        Assert.NotNull(_source.On("b"));
        Assert.Equal(["b", "requests"], Judged().Where(wake => wake.Why == ChangeWhy.Resumed).Select(wake => wake.Subject).Order());
    }

    /// <summary>Canary 2: a folder replaced at its path, which no watcher reports, is found at the reconciliation and reopened.</summary>
    [Fact]
    public async Task A_folder_replaced_at_its_path_is_reopened_at_the_reconciliation()
    {
        await using var watch = Watch();
        var old = _source.On(Folder)!;

        old.Same = false;
        await Step(watch, Deadline);

        Assert.True(old.Disposed);
        Assert.NotNull(_source.On(Folder));
        Assert.Equal(ChangeWhy.Reopened, Assert.Single(Judged()).Why);
    }

    /// <summary>An absent folder is tried again at each reconciliation, never in a loop, and judged at once when it opens.</summary>
    [Fact]
    public async Task An_absent_root_is_tried_again_at_each_reconciliation_never_in_a_loop()
    {
        _source.Absent(Folder);
        await using var watch = Watch();
        Assert.Equal(1, _source.Asked);

        for (var i = 0; i < 5; i++) await Step(watch, TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, _source.Asked);

        await Step(watch, Deadline);
        Assert.Equal(2, _source.Asked);
        Assert.Equal(ChangeWhy.Deadline, Assert.Single(Judged()).Why);

        _source.Absent(Folder, absent: false);
        await Step(watch, Deadline);

        Assert.NotNull(_source.On(Folder));
        Assert.Equal([ChangeWhy.Deadline, ChangeWhy.Opened], Judged().Select(wake => wake.Why));
    }

    /// <summary>
    /// A root that overflows again within a minute is reopened with a back-off, and left to its deadline after three tries, said
    /// once in the machine log; a reconciliation a minute after its last loss opens it again.
    /// </summary>
    [Fact]
    public async Task A_root_lost_again_within_a_minute_backs_off_and_is_left_to_its_deadline_after_three_tries()
    {
        using var log = Log();
        await using var watch = Watch(deadline: TimeSpan.FromMinutes(5), log: log);

        for (var tries = 1; tries <= ChangeWatch.Tries; tries++)
        {
            _source.On(Folder)!.Lose();
            await Step(watch, TimeSpan.Zero);
            var reopensAt = ChangeWatch.Backoff[tries - 1];
            if (reopensAt > TimeSpan.Zero)
            {
                Assert.Null(_source.On(Folder));
                await Step(watch, reopensAt - TimeSpan.FromMilliseconds(1));
                Assert.Null(_source.On(Folder));
                await Step(watch, TimeSpan.FromMilliseconds(1));
            }

            Assert.NotNull(_source.On(Folder));
        }

        _source.On(Folder)!.Lose();
        await Step(watch, TimeSpan.Zero);
        await Step(watch, TimeSpan.FromSeconds(50));
        Assert.Null(_source.On(Folder));
        Assert.Single(Lines("change.unwatched"));

        await Step(watch, TimeSpan.FromMinutes(5));
        Assert.NotNull(_source.On(Folder));
        Assert.Single(Lines("change.unwatched"));
        Assert.Equal(ChangeWatch.Tries + 1, Judged().Count(wake => wake.Why == ChangeWhy.Lost));
    }

    /// <summary>A judgement that fails leaves its subject unknown, tried again with a back-off inside its deadline.</summary>
    [Fact]
    public async Task A_judgement_that_fails_is_tried_again_inside_its_deadline()
    {
        using var log = Log();
        var calls = 0;
        await using var watch = Watch(
            (wake, ct) =>
            {
                lock (_judged) _judged.Add(wake);
                if (Interlocked.Increment(ref calls) == 1) throw new IOException("held");
                return Task.FromResult(1);
            },
            deadline: TimeSpan.FromMinutes(1), log: log);

        _source.On(Folder)!.Raise("s1.json");
        await Step(watch, ChangeWatch.Quiet);
        await Step(watch, ChangeWatch.Retry);

        Assert.Equal([ChangeWhy.Hint, ChangeWhy.Retry], Judged().Select(wake => wake.Why));
        Assert.Single(Lines("error"));
    }

    /// <summary>
    /// What the watch judged goes to the machine log with its ms and what woke it; an idle deadline that found nothing writes
    /// nothing, and one that found something says so, since a watcher then missed it.
    /// </summary>
    [Fact]
    public async Task A_judgement_is_logged_with_its_ms_and_what_woke_it_and_an_idle_deadline_is_not()
    {
        using var log = Log();
        var acts = 0;
        await using var watch = Watch((wake, ct) => Task.FromResult(Volatile.Read(ref acts)), log: log);

        _source.On(Folder)!.Raise("s1.json");
        await Step(watch, ChangeWatch.Quiet);
        await Step(watch, Deadline);
        Volatile.Write(ref acts, 1);
        await Step(watch, Deadline);

        var lines = Lines("change.judged").Select(line => line.GetProperty("data")).ToArray();
        Assert.Equal([ChangeWhy.Hint, ChangeWhy.Deadline], lines.Select(data => data.GetProperty("why").GetString()));
        Assert.All(lines, data =>
        {
            Assert.Equal("requests", data.GetProperty("topic").GetString());
            Assert.Equal("requests", data.GetProperty("subject").GetString());
            Assert.True(data.GetProperty("ms").GetInt64() >= 0);
        });
        Assert.Equal([0, 1], lines.Select(data => data.GetProperty("acted").GetInt32()));
        Assert.Equal(250, lines[0].GetProperty("waitedMs").GetInt64());
    }

    /// <summary>At most 32 roots are watched; the rest stay on their deadline, said once.</summary>
    [Fact]
    public async Task At_most_32_roots_are_watched_and_the_rest_stay_on_their_deadline()
    {
        using var log = Log();
        await using var watch = Watch(log: log, more: [.. Enumerable.Range(1, ChangeWatch.MostRoots).Select(i => $"r{i}")]);

        Assert.Equal(ChangeWatch.MostRoots, _source.Opened.Count);
        Assert.Null(_source.On($"r{ChangeWatch.MostRoots}"));
        Assert.Single(Lines("change.capped"));

        await Step(watch, Deadline);
        Assert.Contains(Judged(), wake => wake.Subject == $"r{ChangeWatch.MostRoots}" && wake.Why == ChangeWhy.Deadline);
        Assert.Single(Lines("change.capped"));
    }

    private static void InterlockedMax(ref int most, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref most)) && Interlocked.CompareExchange(ref most, value, seen) != seen)
        {
        }
    }
}
