using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LOOKWAIT1 (D181 point 6): each stage of a look, and each piece of its work beside it, timed with its subject, so a slow look
/// names its slow part in the machine log: folded into the next <c>watch.looked</c>'s <c>stages</c>, compact, and a stage still
/// running past the threshold written at once as <c>look.slow</c>, so a hung one is named while it hangs.
/// </summary>
public sealed class LookStagesTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-stages-" + Guid.NewGuid().ToString("N")[..8]);

    // The clock the spans read, in milliseconds, moved by hand so a span's length is the test's to say.
    private long _now;

    public LookStagesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private LookStages Stages(MachineLog? log = null, TimeSpan? slowAfter = null) => new(log, slowAfter, () => Volatile.Read(ref _now));

    private void Pass(long ms) => Interlocked.Add(ref _now, ms);

    [Fact]
    public void A_stage_that_ended_is_said_once_with_its_milliseconds()
    {
        var stages = Stages();
        using (stages.Begin("snapshot")) Pass(340);

        Assert.Equal("snapshot=340", stages.Take());
        Assert.Null(stages.Take());
    }

    [Fact]
    public void A_stage_of_several_subjects_is_summed_and_names_its_slowest()
    {
        var stages = Stages();
        using (stages.Begin("start", "#q1")) Pass(1200);
        using (stages.Begin("start", "#q2")) Pass(4000);
        using (stages.Begin("start", "#q3")) Pass(200);

        Assert.Equal("start=5400x3@#q2", stages.Take());
    }

    [Fact]
    public void Stages_are_said_in_the_order_they_began_and_one_under_the_floor_is_left_out()
    {
        var stages = Stages();
        using (stages.Begin("plugins")) Pass(3);
        using (stages.Begin("snapshot")) Pass(50);
        using (stages.Begin("plan")) Pass(LookStages.Floor - 1);
        using (stages.Begin("consider", "#q1")) Pass(800);

        Assert.Equal("snapshot=50 consider=800@#q1", stages.Take());
    }

    [Fact]
    public void A_gate_s_queue_is_said_apart_from_its_hold()
    {
        var stages = Stages();
        using (var sync = stages.Begin("sync"))
        {
            Pass(3000);
            sync.Queued(TimeSpan.FromMilliseconds(3000));
            Pass(400);
        }

        Assert.Equal("sync=400 sync.queue=3000", stages.Take());
    }

    /// <summary>
    /// LOOKWAIT1b: a stage's starts waiting for a place are its queue, apart from its hold. A fan-out's waits overlap, so the queue is
    /// the time any of them waited, never their sum, and never longer than the stage.
    /// </summary>
    [Fact]
    public void A_stage_s_waits_for_a_place_are_its_queue_and_waits_that_overlap_count_once()
    {
        var stages = Stages();
        using (var start = stages.Begin("start", "#q1"))
        {
            var first = start.Waiting();
            Pass(1000);
            var second = start.Waiting();
            Pass(1500);
            first.Dispose();
            Pass(500);
            second.Dispose();
            second.Dispose();
            Pass(300);
        }

        Assert.Equal("start=300@#q1 start.queue=3000", stages.Take());
    }

    /// <summary>A wait still open as its stage ends is no wait the stage's work made it wait on: said nowhere, never late.</summary>
    [Fact]
    public void A_wait_still_open_as_its_stage_ends_is_said_nowhere()
    {
        var stages = Stages();
        IDisposable outlived;
        using (var start = stages.Begin("start", "#q1"))
        {
            outlived = start.Waiting();
            Pass(800);
        }

        Pass(2000);
        outlived.Dispose();

        Assert.Equal("start=800@#q1", stages.Take());
        Assert.Null(stages.Take());
    }

    /// <summary>The remote sync's own gate and its git's waits for a place are one queue, said once.</summary>
    [Fact]
    public void A_gate_s_queue_and_its_starts_waits_are_one_queue()
    {
        var stages = Stages();
        using (var sync = stages.Begin("sync"))
        {
            Pass(1000);
            sync.Queued(TimeSpan.FromMilliseconds(1000));
            using (sync.Waiting()) Pass(500);
            Pass(200);
        }

        Assert.Equal("sync=200 sync.queue=1500", stages.Take());
    }

    [Fact]
    public void Work_beside_the_look_is_marked_apart_from_the_look_s_own()
    {
        var stages = Stages();
        using (stages.Begin("sync", beside: true)) Pass(2500);
        using (stages.Begin("snapshot")) Pass(40);

        Assert.Equal("~sync=2500 snapshot=40", stages.Take());
    }

    [Fact]
    public void A_stage_still_running_waits_for_the_line_after_it_ends()
    {
        var stages = Stages();
        var start = stages.Begin("start", "#q1", beside: true);
        using (stages.Begin("snapshot")) Pass(40);

        Assert.Equal("snapshot=40", stages.Take());

        Pass(6000);
        start.Dispose();
        Assert.Equal("~start=6040@#q1", stages.Take());
    }

    [Fact]
    public async Task Timed_work_is_a_span_ended_however_the_work_ends()
    {
        var stages = Stages();
        Assert.Equal(7, await stages.TimeAsync("hooks", () => { Pass(90); return Task.FromResult(7); }, "s1"));
        await Assert.ThrowsAsync<IOException>(() => stages.TimeAsync("freeze", () => { Pass(30); return Task.FromException(new IOException("gone")); }, "b1"));

        Assert.Equal("hooks=90@s1 freeze=30@b1", stages.Take());
    }

    /// <summary>🔴 The row's own case: a stage that hangs is named in the log while it hangs, once, with its subject.</summary>
    [Fact]
    public async Task A_stage_running_past_the_threshold_is_written_while_it_runs_naming_its_subject()
    {
        using (var log = new MachineLog(_home, "driver"))
        {
            var stages = Stages(log, TimeSpan.FromMilliseconds(50));
            var hung = stages.Begin("start", "#q1", beside: true);
            await Poll.Until(() => Slow().Length == 1, () => $"{Slow().Length} slow line(s)");

            await Task.Delay(200);
            hung.Dispose();
            using (stages.Begin("snapshot")) Pass(1);
        }

        var slow = Assert.Single(Slow());
        Assert.Equal(("start", "#q1", true), (slow.GetProperty("stage").GetString(), slow.GetProperty("subject").GetString(), slow.GetProperty("beside").GetBoolean()));
        Assert.True(slow.GetProperty("ms").GetInt64() >= 0);
    }

    /// <summary>LOOKSLOW2: the slow line says its queue so far, a wait still open counted, a wait ended counted, none as 0.</summary>
    [Fact]
    public async Task A_slow_line_says_its_queue_so_far_counting_a_wait_still_open()
    {
        using (var log = new MachineLog(_home, "driver"))
        {
            var stages = Stages(log, TimeSpan.FromMilliseconds(200));
            var open = stages.Begin("start", "#open");
            var ended = stages.Begin("start", "#ended");
            var idle = stages.Begin("start", "#idle");
            using (ended.Waiting()) Pass(100);
            var wait = open.Waiting();
            Pass(300);
            await Poll.Until(() => Slow().Length == 3, () => $"{Slow().Length} slow line(s)");
            wait.Dispose();
            open.Dispose();
            ended.Dispose();
            idle.Dispose();
        }

        var queued = Slow().ToDictionary(line => line.GetProperty("subject").GetString()!, line => line.GetProperty("queued").GetInt64());
        Assert.Equal(300, queued["#open"]);
        Assert.Equal(100, queued["#ended"]);
        Assert.Equal(0, queued["#idle"]);
    }

    [Fact]
    public async Task A_stage_that_ends_before_the_threshold_writes_no_slow_line()
    {
        using (var log = new MachineLog(_home, "driver"))
        {
            var stages = Stages(log, TimeSpan.FromMilliseconds(300));
            using (stages.Begin("snapshot")) Pass(5);
            await Task.Delay(600);
        }

        Assert.Empty(Slow());
    }

    private JsonElement[] Slow()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.EnumerateFiles(folder, "*.jsonl")
            .SelectMany(file =>
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == "look.slow")
            .Select(line => line.GetProperty("data"))];
    }
}
