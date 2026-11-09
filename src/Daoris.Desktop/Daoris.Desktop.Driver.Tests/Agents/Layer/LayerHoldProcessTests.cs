using System.Collections.Concurrent;
using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A held <see cref="LayerRun"/> : the steps today's driven run takes around each process, through its executor's host
/// (<see cref="LayerHold"/>): the downloads kept, each process's lineage token on its environment and handed the process the layer started,
/// the watch told each line, its prompt's words written as the write returns, the wait under the start's bound and the watch's quiet (whose
/// kill its readings say nothing of), a go-on with held words, and the native resume's one retry. Real node processes over the tests' provider.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LayerHoldProcessTests : IDisposable
{
    private const string Token = "DAORIS_LAYER_TEST_PROC";

    private readonly string _scratch = ConformanceScratch.New("layer-hold");

    private readonly SessionProcesses _processes;

    private readonly CancellationTokenSource _bound = new(Poll.Patience);

    public LayerHoldProcessTests() => _processes = new SessionProcesses(Path.Combine(_scratch, "sessions"));

    public void Dispose()
    {
        _bound.Dispose();
        ConformanceScratch.Remove(_scratch);
    }

    private static readonly object[] Answered = [new { t = "start", id = "conv-1" }, new { t = "text", text = "done" }, new { t = "end", subtype = "success" }];

    private (LayerRun Run, HeldHost Host, List<string> Said) Held(object spec, Func<TransportStart, TransportStart>? completed = null)
    {
        var host = new HeldHost(_processes);
        var start = new TransportStart(new ProcessStartInfo("unused"), StartClass.Background)
        {
            Target = new SessionTarget("abc123", "Fix the flaky gate", "It fails.", "Platform", "Game", _scratch, "http://127.0.0.1:9"),
            Selection = new HarnessSelection(null),
        };
        var run = new LayerRun(
            LayerTestProvider.Registration(LayerTestProvider.Scenario(_scratch, spec)),
            new LayerStart(LayerTestProvider.Id, "", "", TimeSpan.FromHours(1)),
            new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null),
            exists: _ => true,
            new LayerHold(host, start));
        var said = new List<string>();
        run.HandSink(ReadingSinks.On<WireReading.Shown>(shown => { lock (said) said.Add(shown.Text); }));
        run.HandStart((completed ?? (each => each))(run.Start with
        {
            Session = "s1", Transcript = Path.Combine(_scratch, "s1.log"), Prompt = "the prompt", Own = new HarnessWords(),
        }));
        return (run, host, said);
    }

    [Fact]
    public async Task A_start_keeps_its_downloads_and_runs_under_its_own_lineage_token_its_words_written_as_the_write_returns()
    {
        var written = 0;
        var (run, host, said) = Held(new { echo = true, env = Token, @out = Answered }, start => start with { Written = () => written++ });
        await using var _ = run;

        await run.SpawnAsync("s1", _bound.Token);
        await run.OpenAsync(_bound.Token);
        var end = await run.EndAsync(_bound.Token);

        Assert.Equal(["keep s1", "bracket p1", "spawn p1", "wait p1 bounded", "drain p1", "ended p1 0"], host.Asked);
        Assert.Contains("heard the prompt", said);
        Assert.Contains("env p1", said);
        Assert.Equal(1, written);
        Assert.Equal((0, true, false), (end.Exit, end.Named, end.Ending.Failed));
        Assert.True(host.Heard > 0);
        Assert.Contains("heard the prompt", File.ReadAllLines(Path.Combine(_scratch, "s1.log")));
    }

    [Fact]
    public async Task A_leg_that_fails_to_set_up_lets_its_token_go()
    {
        // A transcript folder that is a file cannot be opened, after the token is issued.
        var blocked = Path.Combine(_scratch, "blocked");
        File.WriteAllText(blocked, "a file");
        var (run, host, _) = Held(new { @out = Answered }, start => start with { Transcript = Path.Combine(blocked, "s1.log") });
        await using var _ = run;

        await run.SpawnAsync("s1", _bound.Token);
        await Assert.ThrowsAnyAsync<Exception>(() => run.OpenAsync(_bound.Token));

        Assert.Equal(["keep s1", "bracket p1", "spawn p1"], host.Asked);
    }

    [Fact]
    public async Task A_legs_opening_readings_carry_its_own_lineage_token()
    {
        var (run, _, _) = Held(new { @out = Answered }, start => start with { Preamble = "the preamble" });
        await using var _ = run;
        var readings = new List<WireReading>();
        run.HandSink(ReadingSinks.On<WireReading>(reading => { lock (readings) readings.Add(reading); }));

        await run.SpawnAsync("s1", _bound.Token);
        await run.OpenAsync(_bound.Token);
        await run.EndAsync(_bound.Token);

        Assert.Equal("the preamble", Assert.IsType<WireReading.Shown>(readings[0]).Text);
        Assert.All(readings, reading => Assert.Equal("p1", reading.Process));
    }

    [Fact]
    public async Task A_go_on_resumes_its_conversation_with_the_held_words_under_a_token_of_its_own_and_no_bound()
    {
        var (run, host, said) = Held(new { echo = true, env = Token, @out = Answered });
        await using var _ = run;
        await run.OpenAsync(_bound.Token);
        await run.EndAsync(_bound.Token);

        var wrote = false;
        run.Input.Hold(new DoorWord(["w1"], "and the tests", null, () => wrote = true));
        await run.ResumeAsync("conv-1", _bound.Token);
        var end = await run.EndAsync(_bound.Token);

        Assert.True(run.GoesOn);
        Assert.Equal(["resumed conv-1", "heard and the tests", "env p2"], said.Where(each => each.StartsWith("resumed") || each.StartsWith("heard and") || each == "env p2"));
        Assert.True(wrote);
        Assert.Contains("wait p2 unbounded", host.Asked);
        Assert.Equal((0, true), (end.Exit, end.Named));
    }

    [Fact]
    public async Task A_resume_ending_failed_before_it_named_its_conversation_is_tried_once_more_with_its_words()
    {
        var (run, host, said) = Held(new { echo = true, @out = Array.Empty<object>(), exit = 3 });
        await using var _ = run;
        var state = new ResumeState();

        await run.ResumeAsync("conv-1", _bound.Token, state);
        var end = await run.EndAsync(_bound.Token);

        Assert.Equal(1, state.Again);
        Assert.Equal(2, said.Count(each => each == "heard the prompt"));
        Assert.Contains("— its harness ended before it reopened its conversation, so the same resume is tried once more.", said);
        Assert.Equal(["ended p1 3", "ended p2 3"], host.Asked.Where(each => each.StartsWith("ended")));
        Assert.Equal(3, end.Exit);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_process_its_bound_or_the_watch_s_quiet_ended_says_nothing_of_a_failed_turn(bool timedOut)
    {
        using var quiet = new CancellationTokenSource();
        var told = 0;
        var (run, host, said) = Held(new { hang = true, @out = new object[] { new { t = "start", id = "conv-1" } } }, start => timedOut
            ? start with { Minutes = 1, TimingOut = _ => told++ }
            : start with { Quiet = quiet.Token, QuietEnding = () => told++ });
        await using var _ = run;
        await run.OpenAsync(_bound.Token);
        if (!timedOut) await quiet.CancelAsync();

        var end = await run.EndAsync(_bound.Token);

        Assert.Equal(1, told);
        Assert.Null(end.Exit);
        Assert.Null(run.Failure);
        Assert.DoesNotContain(said, each => each.StartsWith("— the turn", StringComparison.Ordinal));
        Assert.Contains("ended p1 ", host.Asked);
    }

    /// <summary>
    /// Where the driver carries the layer, Claude Code's driven start is held on the layer's run, the program its start resolved in the door's
    /// command's place and the plugin folders it composed carried; a partner's piece stays on today's transport.
    /// </summary>
    [Fact]
    public void Claude_Code_s_driven_start_is_held_on_the_layer_s_run_and_a_piece_on_today_s()
    {
        var info = new ProcessStartInfo("node") { ArgumentList = { "claude.js", "-p", "--plugin-dir", "D:/plugins/one" } };
        var request = new SpawnRequest("engine", _scratch, "http://127.0.0.1:9");
        var host = new HeldHost(_processes);

        var run = DrivenRuns.Of(ClaudeDrivenLayer.Door, host, new TransportStart(info, StartClass.Background), request);
        var piece = DrivenRuns.Of(ClaudeDrivenLayer.Door, host, new TransportStart(info, StartClass.Background) { Piece = "piece-1" }, request);

        Assert.IsType<LayerRun>(run);
        Assert.Null(piece);
        Assert.Null(DrivenRuns.Of("codex", host, new TransportStart(info, StartClass.Background), request));
    }

    private sealed class ResumeState : IResumeState
    {
        public int Again { get; private set; }

        public bool Named { get; private set; }

        public bool Refused => false;

        public bool Retried => Again > 0;

        public void Opened() => Named = true;

        void IResumeState.Again() => Again++;
    }

    /// <summary>The host's steps, recorded: each bracket's token on the start's environment, the wait killing on a bound or the watch's quiet.</summary>
    private sealed class HeldHost(SessionProcesses processes) : ITransportHost
    {
        private int _tokens;

        private int _heard;

        public ConcurrentQueue<string> Steps { get; } = new();

        public IReadOnlyList<string> Asked => [.. Steps];

        public int Heard => _heard;

        public string Home => "unused";

        public SessionProcesses Processes => processes;

        public MachineLog? Log => null;

        public TimeSpan LingerBound => TimeSpan.Zero;

        public TextReader Hearing(TextReader reader, string sessionId) => reader;

        void ITransportHost.Heard(string sessionId) => Interlocked.Increment(ref _heard);

        public Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct)
        {
            Steps.Enqueue($"keep {sessionId}");
            return Task.CompletedTask;
        }

        public string Bracketed(string sessionId, ProcessStartInfo info, string? moved)
        {
            var proc = $"p{Interlocked.Increment(ref _tokens)}";
            info.Environment[Token] = proc;
            Steps.Enqueue($"bracket {proc}");
            return proc;
        }

        public async Task<Process?> SpawnedAsync(string proc, Func<Task<Process?>> spawn)
        {
            Steps.Enqueue($"spawn {proc}");
            return await spawn();
        }

        public async Task<int?> WaitAsync(
            string sessionId, string proc, Process process, CancellationToken ct, int? minutes, Action<int>? timingOut, CancellationToken quiet,
            Action? quietEnding)
        {
            Steps.Enqueue($"wait {proc} {(timingOut is null ? "unbounded" : "bounded")}");
            if (minutes is { } bound)
            {
                timingOut?.Invoke(bound);
                return Killed(process);
            }

            try
            {
                await process.WaitForExitAsync(quiet.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(ct, quiet).Token : ct);
                return process.ExitCode;
            }
            catch (OperationCanceledException) when (quiet.IsCancellationRequested)
            {
                quietEnding?.Invoke();
                return Killed(process);
            }
        }

        public async Task<bool> DrainedAsync(string sessionId, string proc, Task capture)
        {
            Steps.Enqueue($"drain {proc}");
            await capture.WaitAsync(Poll.Patience);
            return true;
        }

        public void Ended(string sessionId, string proc, int? exit) => Steps.Enqueue($"ended {proc} {exit}");

        public Task AbandonedAsync(string sessionId, string proc, Process process, Task capture) => Task.CompletedTask;

        public Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending) => Task.FromResult(false);

        private static int? Killed(Process process)
        {
            SessionProcesses.EndIfRunning(process);
            process.WaitForExit();
            return null;
        }
    }
}
