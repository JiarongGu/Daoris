using System.Diagnostics;
using Daoris.Driver;
using Lyntai.Processes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The layer's runner, Daoris's launcher: a start through Platform's agent route, every raw line heard by the
/// watch before any reader takes it, the lines outside a frame and the stderr kept as the harness's own, what is written on stdin recorded,
/// no clock on a turn whatever window it is handed, a read bounded by its patience, and a stop Daoris's kill of the tree. Real node
/// processes over the tests' scenario program.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class LauncherRunnerProcessTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("layer-runner");

    private readonly List<string> _outside = [];

    private readonly List<string> _errors = [];

    private readonly List<Process> _started = [];

    private int _heard;

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private LauncherRunner Turn(TimeSpan? patience = null, Action<string>? error = null, Action<string>? outside = null) => new(
        new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null)
        {
            Started = _started.Add,
            Heard = () => Interlocked.Increment(ref _heard),
            Outside = outside ?? (line => { lock (_outside) _outside.Add(line); }),
            Error = error ?? (line => { lock (_errors) _errors.Add(line); }),
        },
        patience,
        exists: _ => true);

    private static async Task<List<string>> All(IAsyncEnumerable<string> lines)
    {
        var read = new List<string>();
        await foreach (var line in lines) read.Add(line);
        return read;
    }

    private IAsyncEnumerable<string> Lines(LauncherRunner runner, object spec, TimeSpan? window = null, string? stdin = null)
    {
        var command = LayerTestProvider.Scenario(_scratch, spec);
        return runner.StreamLinesAsync(command[0], [.. command.Skip(1)], stdin: stdin, inactivityTimeout: window);
    }

    [Fact]
    public async Task A_turn_s_lines_are_yielded_each_heard_and_its_own_words_kept_apart()
    {
        var runner = Turn();

        var lines = await All(Lines(runner, new { err = new[] { "its stderr" }, @out = new object[] { "plain words", new { t = "text", text = "hi" } } }));

        Assert.Equal(["plain words", """{"t":"text","text":"hi"}"""], lines);
        Assert.Equal(["plain words"], _outside);
        Assert.Equal(["its stderr"], _errors);
        Assert.Equal(3, _heard);
        Assert.Same(runner.Process, Assert.Single(_started));
        Assert.Equal(0, runner.Exit);
    }

    [Fact]
    public async Task What_is_written_on_stdin_is_recorded()
    {
        var runner = Turn();

        var lines = await All(Lines(runner, new { echo = true }, stdin: "the prompt"));

        Assert.Equal(["heard the prompt"], lines);
        Assert.Equal(["the prompt"], runner.Written);
    }

    [Fact]
    public async Task A_wrote_callback_that_throws_still_closes_the_childs_stdin_so_it_does_not_wait_on_its_prompt()
    {
        var runner = new LauncherRunner(
            new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null)
            {
                Wrote = () => throw new InvalidOperationException("the event is gone"),
            },
            TimeSpan.FromSeconds(8),
            exists: _ => true);

        var clock = Stopwatch.StartNew();
        var failed = await Assert.ThrowsAsync<InvalidOperationException>(async () => await All(Lines(runner, new { echo = true }, stdin: "the prompt")));

        Assert.Equal("the event is gone", failed.Message);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(6), "the child waited on its stdin until the read's patience ended");
    }

    [Fact]
    public async Task A_turn_arms_no_clock_whatever_window_the_layer_hands_it()
    {
        var runner = Turn();

        var lines = await All(Lines(runner, new { wait = 1500, after = new[] { "done" } }, window: TimeSpan.FromMilliseconds(200)));

        Assert.Equal(["done"], lines);
    }

    [Fact]
    public async Task A_read_is_bounded_by_its_patience()
    {
        var runner = Turn(patience: TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAsync<ProcessTimeoutException>(() => All(Lines(runner, new { hang = true })));
        Assert.Null(runner.Exit);
    }

    [Fact]
    public async Task A_failed_exit_throws_with_the_stderr_tail_after_its_lines()
    {
        var runner = Turn();
        var read = new List<string>();

        var failed = await Assert.ThrowsAsync<ProcessRunException>(async () =>
        {
            await foreach (var line in Lines(runner, new { err = new[] { "it broke" }, @out = new[] { "before" }, exit = 3 })) read.Add(line);
        });

        Assert.Equal(["before"], read);
        Assert.Equal(3, failed.ExitCode);
        Assert.Contains("it broke", failed.Message);
        Assert.Equal(3, runner.Exit);
    }

    [Fact]
    public async Task A_stderr_line_that_cannot_be_kept_ends_no_read_and_is_thrown_after_the_turn_s_lines()
    {
        // More than a pipe holds, so a child whose stderr is no longer read blocks before it reaches its stdout.
        var runner = Turn(error: _ => throw new InvalidOperationException("its transcript is gone"));
        var read = new List<string>();

        var failed = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var line in Lines(runner, new { err = Enumerable.Repeat(new string('x', 200), 2000).ToArray(), @out = new[] { "after" } })) read.Add(line);
        }).WaitAsync(Poll.Patience);

        Assert.Equal("its transcript is gone", failed.Message);
        Assert.Equal(["after"], read);
    }

    [Fact]
    public async Task A_stdout_line_that_cannot_be_kept_still_yields_every_line_and_is_thrown_after_them()
    {
        var runner = Turn(outside: line => { if (line == "plain") throw new InvalidOperationException("its transcript is gone"); });
        var read = new List<string>();

        var failed = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var line in Lines(runner, new { @out = new object[] { "plain", new { t = "text", text = "hi" } } })) read.Add(line);
        });

        Assert.Equal("its transcript is gone", failed.Message);
        Assert.Equal(["plain", """{"t":"text","text":"hi"}"""], read);
        Assert.Equal(0, runner.Exit);
    }

    [Fact]
    public async Task A_stop_kills_the_tree_and_is_kept_as_a_stop()
    {
        var runner = Turn();
        var read = new List<string>();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in Lines(runner, new { @out = new[] { "up" }, hang = true })) read.Add(line);
            }
            catch (ProcessRunException)
            {
            }
        });

        while (Volatile.Read(ref _heard) == 0) await Task.Delay(20);
        runner.Stop();
        await reading.WaitAsync(Poll.Patience);

        Assert.True(runner.Stopped);
        Assert.True(runner.Process!.HasExited);
        Assert.Equal(["up"], read);
    }
}
