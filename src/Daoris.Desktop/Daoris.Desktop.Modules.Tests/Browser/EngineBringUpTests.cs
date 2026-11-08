using System.ComponentModel;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The engine's bring-up as the shell runs it, held apart from the window: a browser that opens no window in its time, stops as it
/// starts, or is cancelled is ended and forgotten, so the next press starts a browser and does not wait ten seconds on a dead one.
/// </summary>
public sealed class EngineBringUpTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan Going = TimeSpan.FromMilliseconds(200);

    private sealed class Child : IBrowserChild
    {
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HasExited => _exited.Task.IsCompleted;

        public int ExitCode { get; private set; }

        public int Ended { get; private set; }

        public Task WaitForExitAsync(CancellationToken ct) => _exited.Task.WaitAsync(ct);

        public void End()
        {
            Ended++;
            Exit(1);
        }

        public void Exit(int code)
        {
            ExitCode = code;
            _exited.TrySetResult();
        }
    }

    private sealed class Harness
    {
        public List<Child> Started { get; } = [];

        public bool Window { get; set; }

        public int Activated { get; private set; }

        public Action<Child>? OnStart { get; set; }

        public EngineBringUp Make() => new(
            start: () =>
            {
                var child = new Child();
                Started.Add(child);
                OnStart?.Invoke(child);
                return child;
            },
            windowOpen: _ => Task.FromResult(Window),
            bringForward: _ =>
            {
                Activated++;
                return Task.CompletedTask;
            },
            exitHint: "Its log is browser.log.",
            Limit,
            Going);
    }

    [Fact]
    public async Task A_browser_that_opens_a_window_is_kept_and_asked_again_it_is_not_started_twice()
    {
        var harness = new Harness { Window = true };
        var bringUp = harness.Make();

        await bringUp.EnsureAsync(activate: false, CancellationToken.None);
        await bringUp.EnsureAsync(activate: true, CancellationToken.None);

        Assert.Single(harness.Started);
        Assert.True(bringUp.Running);
        Assert.Equal(1, harness.Activated);
        Assert.Equal(0, harness.Started[0].Ended);
    }

    [Fact]
    public async Task A_bring_up_that_times_out_ends_the_process_and_the_next_press_starts_another_at_once()
    {
        var harness = new Harness();
        var bringUp = harness.Make();

        await Assert.ThrowsAsync<TimeoutException>(() => bringUp.EnsureAsync(activate: false, CancellationToken.None));

        Assert.Equal(1, harness.Started[0].Ended);
        Assert.False(bringUp.Running);

        harness.Window = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await bringUp.EnsureAsync(activate: false, CancellationToken.None);

        Assert.Equal(2, harness.Started.Count);
        Assert.True(watch.Elapsed < Going, "the next press waited on the process the timed-out one left");
        Assert.True(bringUp.Running);
    }

    [Fact]
    public async Task A_cancelled_bring_up_ends_the_process_it_started()
    {
        var harness = new Harness();
        using var cancel = new CancellationTokenSource();
        var bringUp = harness.Make();
        harness.OnStart = _ => cancel.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bringUp.EnsureAsync(activate: false, cancel.Token));

        Assert.Equal(1, harness.Started[0].Ended);
        Assert.False(bringUp.Running);
    }

    [Fact]
    public async Task A_browser_that_stops_as_it_starts_says_its_exit_and_where_its_log_is()
    {
        var harness = new Harness { OnStart = child => child.Exit(7) };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Make().EnsureAsync(activate: false, CancellationToken.None));

        Assert.Contains("exit 7", error.Message);
        Assert.Contains("Its log is browser.log.", error.Message);
    }

    [Fact]
    public async Task A_start_the_system_refuses_is_a_sentence_for_the_person()
    {
        var bringUp = new EngineBringUp(
            () => throw new Win32Exception("The system cannot find the file specified."),
            _ => Task.FromResult(false), _ => Task.CompletedTask, "", Limit, Going);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => bringUp.EnsureAsync(activate: false, CancellationToken.None));

        Assert.Contains("did not start", error.Message);
        Assert.False(bringUp.Running);
    }

    [Fact]
    public async Task A_browser_whose_last_window_closed_is_waited_for_then_started_again()
    {
        var harness = new Harness { Window = true };
        var bringUp = harness.Make();
        await bringUp.EnsureAsync(activate: false, CancellationToken.None);

        harness.Window = false;
        harness.OnStart = _ => harness.Window = true;
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            harness.Started[0].Exit(0);
        });
        await bringUp.EnsureAsync(activate: false, CancellationToken.None);

        Assert.Equal(2, harness.Started.Count);
        Assert.Equal(0, harness.Started[0].Ended);
    }

    [Fact]
    public async Task A_browser_that_will_not_go_after_its_last_window_is_ended_and_started_again()
    {
        var harness = new Harness { Window = true };
        var bringUp = harness.Make();
        await bringUp.EnsureAsync(activate: false, CancellationToken.None);
        harness.Window = false;
        harness.OnStart = _ => harness.Window = true;

        await bringUp.EnsureAsync(activate: false, CancellationToken.None);

        Assert.Equal(1, harness.Started[0].Ended);
        Assert.Equal(2, harness.Started.Count);
    }
}
