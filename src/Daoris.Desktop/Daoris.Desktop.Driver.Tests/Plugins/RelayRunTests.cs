using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One relay's life over its real pipe: the test is the plugin, a
/// client found by the relay's own subscriber file, and the relay's lease, job check and clock are the test's. Every line of both streams
/// is read against <see cref="RelaySaid"/>'s shapes, and nothing a client wrote ever reaches either.
/// </summary>
public sealed class RelayRunTests : IDisposable
{
    private const string Run = "6c0f1a9e2b47";
    private const string Word = "Merge-every-landed-branch.now";
    private static readonly string Build = new('a', 64);
    private static readonly LeaseHolder Instance = new(LeaseKind.Desktop, 4242, DateTimeOffset.UtcNow);
    private static readonly Regex Sentence = new(@"\ADaoris: (?:[a-z0-9 ,:;'()#\[\]\.\-]|--since|--where)+\z", RegexOptions.CultureInvariant);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "relay-run", Guid.NewGuid().ToString("N")[..8]);
    private readonly ManualClock _clock = new();

    public void Dispose() => GitSeed.Discard(_home);

    private RelayWorld World(Func<int, bool?>? contained = null, Func<LeaseHolder?>? holder = null, Func<int, CancellationToken, Task>? exit = null) => new()
    {
        Clock = _clock,
        Lease = _ => (holder is null ? Instance : holder(), false),
        Contained = (_, pid) => contained is null ? true : contained(pid),
        SessionExit = exit ?? ((_, ct) => Task.Delay(Timeout.Infinite, ct)),
    };

    private sealed record Ran(int Code, IReadOnlyList<string> Out, IReadOnlyList<string> Err);

    private Task<Ran> Start(RelayWorld world, string? since = null, string kinds = "", bool session = false)
    {
        List<string> words = ["--once", "--home", _home];
        if (session) words.AddRange(["--session-pid", "1"]);
        if (since is not null) words.AddRange(["--since", since]);
        if (kinds.Length > 0) words.AddRange(["--kinds", kinds]);
        var ask = RelayAsk.Read(words, out _)!;
        return Task.Run(async () =>
        {
            var (output, error) = (new StringWriter(), new StringWriter());
            // A relay that never ends fails its case rather than holding the run.
            using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var code = await RelayRun.RunAsync(_home, ask, Build, output, error, world, patience.Token);
            static string[] Lines(StringWriter writer) => writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new Ran(code, Lines(output), Lines(error));
        });
    }

    /// <summary>The pipe the relay's own file names, once it is there.</summary>
    private async Task<string> PipeAsync()
    {
        var file = Path.Combine(RelayRun.Subscribers(_home), $"{Environment.ProcessId}.json");
        var until = DateTime.UtcNow + Poll.Patience;
        while (true)
        {
            // The file shows before the relay has let go of it, so a read that finds it held is read again.
            try
            {
                if (File.Exists(file)) return JsonDocument.Parse(await File.ReadAllTextAsync(file)).RootElement.GetProperty("pipe").GetString()!;
            }
            catch (IOException)
            {
            }

            Assert.True(DateTime.UtcNow < until, "the relay wrote no subscriber file");
            await Task.Delay(20);
        }
    }

    private async Task<NamedPipeClientStream> ConnectAsync(string? first = null)
    {
        var client = new NamedPipeClientStream(".", await PipeAsync(), PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(10_000);
        if (first is not null) await SendAsync(client, first);
        return client;
    }

    private static async Task SendAsync(Stream client, params string[] lines)
    {
        await client.WriteAsync(Encoding.UTF8.GetBytes(string.Concat(lines.Select(line => line + "\n"))));
        await client.FlushAsync();
    }

    private static string First(long seq, string run = Run, string? build = null) =>
        $$"""{"plugin":"event-relay","run":"{{run}}","seq":{{seq}},"grammar":2,"build":"{{build ?? Build}}"}""";

    private static string Parked(long seq, string session = "4b62a9fe", long missed = 0) =>
        $$"""{"run":"{{Run}}","seq":{{seq}},"missed":{{missed}},"kind":"session.parked","at":"2026-11-06T10:00:00Z","session":"{{session}}"}""";

    private static void Said(Ran ran)
    {
        foreach (var line in ran.Out.Concat(ran.Err))
        {
            Assert.Matches(Sentence, line);
            Assert.DoesNotContain("Merge", line);
            Assert.DoesNotContain("note", line);
        }
    }

    [Fact]
    public async Task A_plugin_at_the_watermark_is_told_into_one_batch_gathered_on_the_held_clock_and_closing_on_its_bracket()
    {
        var relay = Start(World(), since: $"{Run}:4");
        await using var plugin = await ConnectAsync(First(4));
        await SendAsync(plugin, Parked(5), Parked(6, "5c73b0af"));
        await _clock.WaitDueAsync(TimeSpan.FromSeconds(30));
        _clock.Advance(TimeSpan.FromSeconds(30));
        var ran = await relay;
        Assert.Equal(0, ran.Code);
        Assert.Equal(
            [
                $"Daoris: session 4b62a9fe parked, waiting on the person [{Run} #5]",
                $"Daoris: session 5c73b0af parked, waiting on the person [{Run} #6]",
                $"Daoris: read up to [{Run} #6]",
            ],
            ran.Out);
        Assert.Empty(ran.Err);
        Assert.False(File.Exists(Path.Combine(RelayRun.Subscribers(_home), $"{Environment.ProcessId}.json")), "the relay's file outlived it");
    }

    [Fact]
    public async Task A_batch_gathered_whole_never_says_a_gap_for_the_read_that_stopping_it_cancels()
    {
        for (var round = 0; round < 40; round++)
        {
            var relay = Start(World(), since: $"{Run}:4");
            await using var plugin = await ConnectAsync(First(4));
            await SendAsync(plugin, Parked(5));
            await _clock.WaitDueAsync(TimeSpan.FromSeconds(30));
            _clock.Advance(TimeSpan.FromSeconds(30));

            var ran = await relay;

            Assert.Equal(
                [$"Daoris: session 4b62a9fe parked, waiting on the person [{Run} #5]", $"Daoris: read up to [{Run} #5]"],
                ran.Out);
        }
    }

    [Fact]
    public async Task No_watermark_says_the_gap_at_the_first_line_and_another_build_its_update_line()
    {
        var gap = Start(World());
        await using (await ConnectAsync(First(9)))
        {
            Assert.Equal([$"Daoris: events may have been missed; read the sessions again [{Run} #9]", $"Daoris: read up to [{Run} #9]"], (await gap).Out);
        }

        var stale = Start(World(), since: $"{Run}:9");
        await using (await ConnectAsync(First(9, build: new string('b', 64))))
        {
            Assert.Equal([$"Daoris: Daoris was updated; start the relay again [{Run} #9]", $"Daoris: read up to [{Run} #9]"], (await stale).Out);
        }
    }

    [Fact]
    public async Task First_lines_out_of_schema_and_strangers_are_refused_unsaid_and_the_bound_says_why_none_reached_it()
    {
        // Three clients the job holds, each with a first line out of its schema, then one it does not hold.
        var asked = 0;
        var relay = Start(World(contained: _ => ++asked <= 3));
        foreach (var line in new[] { First(1, run: Word), First(1)[..^1] + ",\"note\":\"x\"}", new string(' ', 513) + First(1), First(1) })
        {
            await using var stranger = await ConnectAsync();
            try
            {
                await SendAsync(stranger, line);
            }
            catch (IOException)
            {
                // Refused before its line is read: the relay closes the pipe.
            }
        }

        await _clock.WaitDueAsync(TimeSpan.FromMinutes(15));
        _clock.Advance(TimeSpan.FromMinutes(15));
        var ran = await relay;
        Said(ran);
        Assert.Equal([$"Daoris: no events reached this relay (not-contained); read the sessions again [000000000000 #0]", "Daoris: read up to [000000000000 #0]"], ran.Out);
        Assert.Contains(RelaySaid.NotContained, ran.Err);
    }

    [Fact]
    public async Task A_client_the_job_cannot_answer_is_taken_untold_its_first_line_still_checked()
    {
        var relay = Start(World(contained: _ => null));
        await using (await ConnectAsync(First(1, run: Word)))
        {
        }

        await using var plugin = await ConnectAsync(First(3));
        var ran = await relay;
        Said(ran);
        Assert.Equal([$"Daoris: events may have been missed; read the sessions again [{Run} #3]", $"Daoris: read up to [{Run} #3]"], ran.Out);
        Assert.Equal(["Daoris: 1 clients refused", "Daoris: 2 clients taken unchecked across elevations", RelaySaid.Unchecked], ran.Err);
    }

    [Fact]
    public async Task A_silent_client_holding_the_connection_ends_the_relay_when_the_plugin_reaches_its_second_instance()
    {
        var relay = Start(World(), since: $"{Run}:2");
        await using var silent = await ConnectAsync(First(2));
        await using var plugin = await ConnectAsync(First(2));
        var ran = await relay;
        Assert.Equal([$"Daoris: a second client claimed the events; read the sessions again [{Run} #2]", $"Daoris: read up to [{Run} #2]"], ran.Out);
    }

    [Fact]
    public async Task An_agents_words_in_a_frame_are_dropped_and_three_bad_in_a_row_end_with_the_gap_line()
    {
        var relay = Start(World(), since: $"{Run}:1");
        await using var plugin = await ConnectAsync(First(1));
        await SendAsync(plugin, Parked(2), Parked(3, Word), Parked(3), Parked(3), "{\"run\":\"" + Word + "\"}", "not json", new string('x', 5000));
        var ran = await relay;
        Said(ran);
        Assert.Equal(
            [
                $"Daoris: session 4b62a9fe parked, waiting on the person [{Run} #2]",
                $"Daoris: session 4b62a9fe parked, waiting on the person [{Run} #3]",
                $"Daoris: events may have been missed; read the sessions again [{Run} #3]",
                $"Daoris: read up to [{Run} #3]",
            ],
            ran.Out);
        Assert.Equal(["Daoris: 4 frames outside the grammar dropped", "Daoris: 1 repeated frames dropped"], ran.Err);
    }

    [Fact]
    public async Task The_session_gone_ends_the_relay_printing_nothing()
    {
        var gone = new TaskCompletionSource();
        var relay = Start(World(exit: (_, _) => gone.Task), since: $"{Run}:1", session: true);
        await PipeAsync();
        gone.SetResult();
        var ran = await relay;
        Assert.Equal((0, 0, 0), (ran.Code, ran.Out.Count, ran.Err.Count));
    }

    [Fact]
    public async Task The_bound_waits_while_no_instance_holds_the_home_and_ends_once_one_does()
    {
        LeaseHolder? holder = null;
        var relay = Start(World(holder: () => holder), since: $"{Run}:7");
        await PipeAsync();
        await _clock.WaitDueAsync(TimeSpan.FromMinutes(15));
        _clock.Advance(TimeSpan.FromMinutes(15));
        await _clock.WaitDueAsync(TimeSpan.FromMinutes(15));
        Assert.False(relay.IsCompleted, "a closed Daoris ended the relay");
        holder = Instance;
        _clock.Advance(TimeSpan.FromMinutes(15));
        var ran = await relay;
        Assert.Equal([$"Daoris: no events reached this relay (no-client); read the sessions again [{Run} #7]", $"Daoris: read up to [{Run} #7]"], ran.Out);
    }

    [Fact]
    public async Task A_dead_relays_file_is_removed_at_the_start_and_a_live_ones_kept()
    {
        var subscribers = RelayRun.Subscribers(_home);
        const int dead = int.MaxValue - 7;
        var parent = Environment.ProcessId == 4 ? 8 : 4;
        File.WriteAllText(Path.Combine(subscribers, $"{dead}.json"), $$"""{"pipe":"x","pid":{{dead}}}""");
        File.WriteAllText(Path.Combine(subscribers, $"{parent}.json"), $$"""{"pipe":"x","pid":{{parent}}}""");
        var relay = Start(World(exit: (_, _) => Task.CompletedTask), since: $"{Run}:1", session: true);
        await relay;
        Assert.False(File.Exists(Path.Combine(subscribers, $"{dead}.json")));
        Assert.True(File.Exists(Path.Combine(subscribers, $"{parent}.json")));
    }

    [Fact]
    public async Task A_file_a_killed_relay_left_under_this_ones_pid_is_taken_over_not_a_refusal_to_start()
    {
        var file = Path.Combine(RelayRun.Subscribers(_home), $"{Environment.ProcessId}.json");
        File.WriteAllText(file, $$"""{"pipe":"stale","pid":{{Environment.ProcessId}}}""");

        var relay = Start(World(), since: $"{Run}:7");
        var until = DateTime.UtcNow + Poll.Patience;
        while (!relay.IsCompleted && JsonDocument.Parse(await ReadHeldAsync(file)).RootElement.GetProperty("pipe").GetString() == "stale")
        {
            Assert.True(DateTime.UtcNow < until, "the relay never replaced the file");
            await Task.Delay(20);
        }

        Assert.False(relay.IsCompleted, "the relay stopped at its start");
        await _clock.WaitDueAsync(TimeSpan.FromMinutes(15));
        _clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(0, (await relay).Code);
    }

    private static async Task<string> ReadHeldAsync(string file)
    {
        while (true)
        {
            try
            {
                // Shared for delete too: a reader without it would block the relay's own removal of the stale file.
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return await new StreamReader(stream).ReadToEndAsync();
            }
            catch (IOException) { await Task.Delay(10); }
        }
    }

    /// <summary>A clock that stands still until the test moves it, its timers fired as it passes them.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<Held> _timers = [];
        private DateTimeOffset _now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate) return _now;
        }

        public override long GetTimestamp() => GetUtcNow().UtcTicks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Held(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        /// <summary>Waits until a timer is armed to fire <paramref name="after"/> from now: the relay's wait is counting.</summary>
        public async Task WaitDueAsync(TimeSpan after)
        {
            for (var tries = 0; ; tries++)
            {
                lock (_gate)
                {
                    if (_timers.Any(timer => timer.Due == _now + after)) return;
                }

                Assert.True(tries < 500, $"no wait of {after} was armed");
                await Task.Delay(20);
            }
        }

        public void Advance(TimeSpan by)
        {
            List<Held> due;
            lock (_gate)
            {
                _now += by;
                due = _timers.Where(timer => timer.Due <= _now).ToList();
                foreach (var timer in due) timer.Due = null;
            }

            foreach (var timer in due) timer.Fire();
        }

        private sealed class Held(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset? Due { get; set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (clock._gate)
                {
                    Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + dueTime;
                    if (!clock._timers.Contains(this)) clock._timers.Add(this);
                }

                return true;
            }

            public void Fire() => callback(state);

            public void Dispose()
            {
                lock (clock._gate) clock._timers.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
